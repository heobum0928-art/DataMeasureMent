using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HalconDotNet;
using ReringProject.Device;
using ReringProject.Network;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.Sequence
{
    //261006 hbk Side Datum 초점 학습 사진 수집(임시 도구).
    //  수집 모드가 켜져 있으면 Side 지그로 들어온 z 번호를 "높이 단계"로 해석한다.
    //    가로 범위 z → Datum 가로 사진 촬영 후 가로 ROI A/B 주변만 잘라 저장
    //    세로 z      → Datum 세로 사진 촬영 후 세로 ROI 주변만 잘라 저장
    //  그 외 z 나 모드 꺼짐이면 평소 검사 그대로. Z 높이 값은 PLC 가 정하고, 비전은 받은 z 로 이름만 붙인다.
    //  원본 한 장이 약 127MB 라 통째 저장 대신 ROI 주변만 남긴다.
    public static class SideDatumTrainCapture
    {
        public const string SAVE_ROOT = @"D:\Data\AI_Train\SideDatum";

        public enum ESweepRole
        {
            None,
            Horizontal,
            Vertical,
        }

        //261006 hbk 재안착마다 위치가 조금씩 달라지므로 ROI 바깥으로 넉넉히 잘라 둔다
        private const int CROP_MARGIN_PX = 400;
        private const string ROI_INFO_FILE = "rois.txt";
        private const string PATTERN_HORIZONTAL = "*_A.bmp";
        private const string PATTERN_VERTICAL = "*_V.bmp";

        private static readonly object _saveLock = new object();

        //261006 hbk 화면에서 켜고 끈다. 창을 닫으면 꺼진다.
        public static volatile bool IsActive = false;
        public static int HorizontalStartZ = 1;
        public static int HorizontalEndZ = 12;
        //261006 hbk 세로는 찍지 않는 것이 기본(NO_VERTICAL). 화면 칸을 비우면 세로 없음.
        public const int NO_VERTICAL = -1;
        public static int VerticalZ = NO_VERTICAL;

        /// <summary>저장할 때마다 (Datum 이름, 가로 장수, 세로 장수, z, 자재번호) 로 알린다. 통신 스레드에서 불린다.</summary>
        public static event Action<string, int, int, int, int> Saved;

        public static bool IsSideSequence(string szSeqName)
        {
            bool bIsSide = szSeqName == SequenceHandler.SEQ_SIDE_1
                || szSeqName == SequenceHandler.SEQ_SIDE_2
                || szSeqName == SequenceHandler.SEQ_SIDE_3
                || szSeqName == SequenceHandler.SEQ_SIDE_4;
            return bIsSide;
        }

        public static ESweepRole MapZ(int nZ)
        {
            bool bHorizontal = nZ >= HorizontalStartZ && nZ <= HorizontalEndZ;
            if (bHorizontal)
            {
                return ESweepRole.Horizontal;
            }
            bool bVertical = VerticalZ != NO_VERTICAL && nZ == VerticalZ;
            if (bVertical)
            {
                return ESweepRole.Vertical;
            }
            return ESweepRole.None;
        }

        /// <summary>수집 모드에서 이 시퀀스·z 를 가로챌지. true 면 $PREP 조명은 건드리지 않는다(촬영 직전에 Datum 조명을 켠다).</summary>
        public static bool ShouldIntercept(string szSeqName, int nZ)
        {
            if (!IsActive)
            {
                return false;
            }
            if (!IsSideSequence(szSeqName))
            {
                return false;
            }
            return MapZ(nZ) != ESweepRole.None;
        }

        /// <summary>
        /// 수집 중인 z 의 응답 글자. 마지막 가로 z(HorizontalEndZ)면 P, 그 전은 B. 수집 대상이 아니면 false.
        /// </summary>
        //261006 hbk PLC 는 P/F 를 받으면 사이클을 끝낸다 — 평소 판정(지그 원래 마지막 z 에서 F)을 쓰면 z12 전에 끝나 버린다.
        public static bool TryGetSweepJudgement(string szSeqName, int nZ, out bool bIsLast)
        {
            bIsLast = false;
            bool bIntercepted = ShouldIntercept(szSeqName, nZ);
            if (!bIntercepted)
            {
                return false;
            }
            bIsLast = nZ >= HorizontalEndZ;
            return true;
        }

        /// <summary>
        /// 수집 모드에서 $TEST 를 처리한다. 가로챘으면 true(사진 저장 + PLC 응답까지 끝냄), 아니면 false(평소 검사로 진행).
        /// </summary>
        public static bool TryHandleTest(InspectionSequence seq, TestPacket packet, int nZ)
        {
            if (seq == null || packet == null)
            {
                return false;
            }
            if (!ShouldIntercept(seq.Name, nZ))
            {
                return false;
            }
            if (seq.State != EContextState.Idle)
            {
                return false;   // 평소 경로와 같은 거부 처리로 넘긴다
            }

            ESweepRole role = MapZ(nZ);
            int nMaterialNo = packet.IndexNumber;
            CaptureAndSave(seq, role, nZ, nMaterialNo);

            // 검사 Action 없이 바로 끝내고 평소 형식의 응답을 보낸다 — PLC 가 다음 z 로 넘어가게
            seq.StartEmptyScope(packet);
            return true;
        }

        private static void CaptureAndSave(InspectionSequence seq, ESweepRole role, int nZ, int nMaterialNo)
        {
            if (seq.DatumConfigs.Count == 0)
            {
                Logging.PrintErrLog((int)ELogType.Error, "[SWEEP] " + seq.Name + " 에 Datum 이 없음");
                return;
            }
            DatumConfig datum = seq.DatumConfigs[0];
            ShotConfig shot = SystemHandler.Handle.Sequences.RecipeManager.Shots
                .Where(s => s.OwnerSequenceName == seq.Name)
                .OrderBy(s => s.ZIndex)
                .FirstOrDefault();
            if (shot == null)
            {
                Logging.PrintErrLog((int)ELogType.Error, "[SWEEP] " + seq.Name + " 에 촬영용 Shot 이 없음");
                return;
            }

            HImage hImage = null;
            try
            {
                seq.ApplyDatumLights(datum);
                LightHandler.Handle.WaitForLightsSettled();
                hImage = GrabDatumImage(shot, datum, role);
                seq.TurnOffLightsAfterManualGrab();

                if (hImage == null)
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[SWEEP] " + datum.DatumName + " z" + nZ + " 촬영 실패");
                    return;
                }
                Save(datum, hImage, role, nZ, nMaterialNo);
            }
            catch (Exception ex)
            {
                Logging.PrintErrLog((int)ELogType.Error, "[SWEEP] " + datum.DatumName + " z" + nZ + " 처리 실패: " + ex.Message);
            }
            finally
            {
                if (hImage != null)
                {
                    try { hImage.Dispose(); } catch { }
                }
            }
        }

        private static HImage GrabDatumImage(ShotConfig shot, DatumConfig datum, ESweepRole role)
        {
#if SIMUL_MODE
            // 카메라 없는 PC: 티칭 사진으로 대신한다(흐름 확인용)
            string szPath = datum.TeachingImagePath;
            if (role == ESweepRole.Vertical)
            {
                szPath = datum.TeachingImagePath_Vertical;
            }
            if (string.IsNullOrEmpty(szPath) || !File.Exists(szPath))
            {
                return null;
            }
            return new HImage(szPath);
#else
            string szRoleId = DeviceHandler.BuildGrabRoleIdentifier(shot.DeviceName, datum.MirrorX, datum.MirrorY);
            return SystemHandler.Handle.Devices.GrabHalconImage(shot, szRoleId);
#endif
        }

        private static void Save(DatumConfig datum, HImage hImage, ESweepRole role, int nZ, int nMaterialNo)
        {
            int nHorizontalCount;
            int nVerticalCount;
            lock (_saveLock)
            {
                int nWidth;
                int nHeight;
                hImage.GetImageSize(out nWidth, out nHeight);

                string szDir = Path.Combine(SAVE_ROOT, datum.DatumName);
                Directory.CreateDirectory(szDir);

                if (role == ESweepRole.Horizontal)
                {
                    int nNo = CountInDir(szDir, PATTERN_HORIZONTAL) + 1;
                    string szBase = string.Format("{0:0000}_z{1:00}_M{2}", nNo, nZ, nMaterialNo);
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
                else
                {
                    int nNo = CountInDir(szDir, PATTERN_VERTICAL) + 1;
                    string szBase = string.Format("{0:0000}_z{1:00}_M{2}", nNo, nZ, nMaterialNo);
                    SaveOneCrop(hImage, nWidth, nHeight,
                        datum.Vertical_Row, datum.Vertical_Col,
                        datum.Vertical_Length1, datum.Vertical_Length2,
                        Path.Combine(szDir, szBase + "_V.bmp"));
                }

                nHorizontalCount = CountInDir(szDir, PATTERN_HORIZONTAL);
                nVerticalCount = CountInDir(szDir, PATTERN_VERTICAL);
            }
            Logging.PrintLog((int)ELogType.Trace, "[SWEEP] " + datum.DatumName + " z" + nZ + " 저장 (가로 "
                + nHorizontalCount + " / 세로 " + nVerticalCount + ", 자재 " + nMaterialNo + ")");

            Action<string, int, int, int, int> handler = Saved;
            if (handler != null)
            {
                try
                {
                    handler(datum.DatumName, nHorizontalCount, nVerticalCount, nZ, nMaterialNo);
                }
                catch
                {
                    // 표시 창 오류가 통신을 막지 않게 한다
                }
            }
        }

        /// <summary>Datum 폴더에 저장된 (가로, 세로) 장수.</summary>
        public static void CountSaved(string szDatumName, out int nHorizontal, out int nVertical)
        {
            string szDir = Path.Combine(SAVE_ROOT, szDatumName);
            nHorizontal = CountInDir(szDir, PATTERN_HORIZONTAL);
            nVertical = CountInDir(szDir, PATTERN_VERTICAL);
        }

        private static int CountInDir(string szDir, string szPattern)
        {
            if (!Directory.Exists(szDir))
            {
                return 0;
            }
            return Directory.GetFiles(szDir, szPattern).Length;
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
