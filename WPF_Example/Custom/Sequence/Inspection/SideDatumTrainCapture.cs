using System;
using System.Globalization;
using System.IO;
using System.Text;
using HalconDotNet;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.Sequence
{
    //261006 hbk Side Datum 초점 학습 사진 수집(임시 도구).
    //  자동 검사 중 Datum 가로 사진(크로스-Z role A)이 찍힐 때마다 가로 ROI A/B 주변만 잘라
    //  들어온 순서대로 번호를 붙여 저장한다. 몇 번·어느 높이로 찍을지는 PLC 가 정한다.
    //  원본 한 장이 약 127MB 라 통째 저장 대신 ROI 주변만 남긴다.
    public static class SideDatumTrainCapture
    {
        public const string SAVE_ROOT = @"D:\Data\AI_Train\SideDatum";

        //261006 hbk 화면 표시용 — 한 번 놓을 때 PLC 가 찍는 높이 수
        public const int Z_STEP_COUNT = 12;

        //261006 hbk 재안착마다 위치가 조금씩 달라지므로 ROI 바깥으로 넉넉히 잘라 둔다
        private const int CROP_MARGIN_PX = 400;
        private const string ROI_INFO_FILE = "rois.txt";
        private const string FILE_PATTERN_A = "*_A.bmp";

        private static readonly object _saveLock = new object();

        /// <summary>저장할 때마다 (Datum 이름, 총 장수, 자재번호) 로 알린다. 촬영 스레드에서 불린다.</summary>
        public static event Action<string, int, int> Saved;

        /// <summary>Datum 가로 사진에서 ROI A/B 주변을 잘라 저장한다. 사진은 호출자가 해제한다. 실패해도 검사는 계속된다.</summary>
        public static void SaveHorizontal(DatumConfig datum, HImage hImage, int nMaterialNo)
        {
            if (datum == null || hImage == null)
            {
                return;
            }
            bool bHasHorizontalRoi = datum.Horizontal_A_Length1 > 0 && datum.Horizontal_B_Length1 > 0;
            if (!bHasHorizontalRoi)
            {
                return;
            }

            int nCount;
            try
            {
                lock (_saveLock)
                {
                    int nWidth;
                    int nHeight;
                    hImage.GetImageSize(out nWidth, out nHeight);

                    string szDir = Path.Combine(SAVE_ROOT, datum.DatumName);
                    Directory.CreateDirectory(szDir);
                    nCount = CountSaved(szDir) + 1;
                    string szBase = string.Format("{0:0000}_M{1}", nCount, nMaterialNo);

                    int[] arrCropA = SaveOneCrop(hImage, nWidth, nHeight,
                        datum.Horizontal_A_Row, datum.Horizontal_A_Col,
                        datum.Horizontal_A_Length1, datum.Horizontal_A_Length2,
                        Path.Combine(szDir, szBase + "_A.bmp"));
                    int[] arrCropB = SaveOneCrop(hImage, nWidth, nHeight,
                        datum.Horizontal_B_Row, datum.Horizontal_B_Col,
                        datum.Horizontal_B_Length1, datum.Horizontal_B_Length2,
                        Path.Combine(szDir, szBase + "_B.bmp"));

                    WriteRoiInfo(Path.Combine(szDir, ROI_INFO_FILE), datum, nWidth, nHeight, arrCropA, arrCropB);
                }
                Logging.PrintLog((int)ELogType.Trace, "[SWEEP] " + datum.DatumName + " #" + nCount + " 저장 (자재 " + nMaterialNo + ")");
            }
            catch (Exception ex)
            {
                Logging.PrintErrLog((int)ELogType.Error, "[SWEEP] " + datum.DatumName + " 저장 실패: " + ex.Message);
                return;
            }

            Action<string, int, int> handler = Saved;
            if (handler != null)
            {
                try
                {
                    handler(datum.DatumName, nCount, nMaterialNo);
                }
                catch
                {
                    // 표시 창 오류가 검사를 막지 않게 한다
                }
            }
        }

        /// <summary>Datum 폴더에 지금까지 저장된 장수.</summary>
        public static int CountSaved(string szDatumName)
        {
            string szDir = Path.Combine(SAVE_ROOT, szDatumName);
            return CountSavedInDir(szDir);
        }

        private static int CountSavedInDir(string szDir)
        {
            if (!Directory.Exists(szDir))
            {
                return 0;
            }
            return Directory.GetFiles(szDir, FILE_PATTERN_A).Length;
        }

        // 반환: { row1, col1, row2, col2 } (원본 이미지 좌표)
        private static int[] SaveOneCrop(HImage hImage, int nWidth, int nHeight,
            double dRow, double dCol, double dLength1, double dLength2, string szPath)
        {
            double dHalf = Math.Max(dLength1, dLength2) + CROP_MARGIN_PX;
            int nRow1 = Clamp((int)Math.Floor(dRow - dHalf), 0, nHeight - 1);
            int nCol1 = Clamp((int)Math.Floor(dCol - dHalf), 0, nWidth - 1);
            int nRow2 = Clamp((int)Math.Ceiling(dRow + dHalf), 0, nHeight - 1);
            int nCol2 = Clamp((int)Math.Ceiling(dCol + dHalf), 0, nWidth - 1);

            HImage hCrop = null;
            try
            {
                hCrop = hImage.CropRectangle1(nRow1, nCol1, nRow2, nCol2);
                hCrop.WriteImage("bmp", 0, szPath);
            }
            finally
            {
                if (hCrop != null)
                {
                    try { hCrop.Dispose(); } catch { }
                }
            }
            return new int[] { nRow1, nCol1, nRow2, nCol2 };
        }

        private static int Clamp(int nValue, int nMin, int nMax)
        {
            if (nValue < nMin)
            {
                return nMin;
            }
            if (nValue > nMax)
            {
                return nMax;
            }
            return nValue;
        }

        //261006 hbk 정답(마스크) 자동 생성 때 잘라낸 위치와 ROI 를 되찾기 위한 기록
        private static void WriteRoiInfo(string szPath, DatumConfig datum, int nWidth, int nHeight, int[] arrCropA, int[] arrCropB)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Datum=" + datum.DatumName);
            sb.AppendLine("ImageWidth=" + nWidth.ToString(inv));
            sb.AppendLine("ImageHeight=" + nHeight.ToString(inv));
            AppendRoi(sb, "A", arrCropA, datum.Horizontal_A_Row, datum.Horizontal_A_Col,
                datum.Horizontal_A_Phi, datum.Horizontal_A_Length1, datum.Horizontal_A_Length2);
            AppendRoi(sb, "B", arrCropB, datum.Horizontal_B_Row, datum.Horizontal_B_Col,
                datum.Horizontal_B_Phi, datum.Horizontal_B_Length1, datum.Horizontal_B_Length2);
            File.WriteAllText(szPath, sb.ToString());
        }

        private static void AppendRoi(StringBuilder sb, string szKey, int[] arrCrop,
            double dRow, double dCol, double dPhi, double dLength1, double dLength2)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            sb.AppendLine(string.Format(inv, "{0}_Crop={1},{2},{3},{4}", szKey, arrCrop[0], arrCrop[1], arrCrop[2], arrCrop[3]));
            sb.AppendLine(string.Format(inv, "{0}_Roi={1},{2},{3},{4},{5}", szKey, dRow, dCol, dPhi, dLength1, dLength2));
        }
    }
}
