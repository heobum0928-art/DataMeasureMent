using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using ReringProject.Sequence; //260710 hbk SkipReason 상수 참조용
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;

namespace ReringProject.Export
{
    /// <summary>
    /// 결과 xlsx 공용 헬퍼 — 판정 문구, 캡쳐 JPG 대기·셀 삽입. 반복도·일괄검사 export(RepeatExcelExportService)가 공유한다.
    /// </summary>
    public static class ExcelExportService
    {
        // 비동기 write 레이스 대기 파라미터 (CONTEXT 재량 범위 1~2초 안에서 선택)
        private const int CAPTURE_WAIT_TIMEOUT_MS = 1500;     // 파일 1개당 최대 대기
        private const int CAPTURE_WAIT_POLL_MS = 100;         // 폴링 간격

        // 셀 안 이미지 표시 박스(픽셀)
        private const int CAPTURE_BOX_WIDTH_PX = 160;
        private const int CAPTURE_BOX_HEIGHT_PX = 120;

        // 엑셀 단위 환산: 컬럼 폭은 문자수, 행 높이는 포인트(96dpi 기준 1px = 0.75pt)
        private const double EXCEL_PIXELS_PER_WIDTH_UNIT = 7.0;
        private const double EXCEL_POINTS_PER_PIXEL = 0.75;

        private const int JPEG_MIN_BYTES = 4;                 // SOI(2) + EOI(2) 최소 길이

        /// <summary>
        /// 캡쳐 JPG 를 바이트로 읽는다. 경로당 1회만 실제 대기/읽기 (결과가 null 이어도 캐시).
        /// swBudget/nBudgetMs 는 export 1회 전체 폴링 대기 상한 — 호출자가 데이터 규모에 맞춰 정한다.
        /// </summary>
        internal static byte[] LoadCaptureImageBytes(string szPath, Dictionary<string, byte[]> dicCache, Stopwatch swBudget, int nBudgetMs)
        {
            if (string.IsNullOrEmpty(szPath))
            {
                return null;
            }

            if (dicCache.ContainsKey(szPath))
            {
                return dicCache[szPath];
            }

            byte[] arrBytes = WaitForCaptureImage(szPath, swBudget, nBudgetMs);
            dicCache[szPath] = arrBytes;

            bool bLoadFailed = arrBytes == null;
            if (bLoadFailed)
            {
                try
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[ExcelExportService] capture image not ready, cell left blank: " + szPath);
                }
                catch { }
            }

            return arrBytes;
        }

        /// <summary>
        /// CaptureImageSaveService 워커가 JPG 를 비동기로 쓰므로 export 시점에 아직 없거나 쓰는 중일 수 있다.
        /// </summary>
        private static byte[] WaitForCaptureImage(string szPath, Stopwatch swBudget, int nBudgetMs)
        {
            int nWaitedMs = 0;
            while (true)
            {
                if (File.Exists(szPath))
                {
                    byte[] arrBytes = TryReadCompleteJpeg(szPath);
                    if (arrBytes != null)
                    {
                        return arrBytes;
                    }
                }

                bool bBudgetLeft = swBudget.ElapsedMilliseconds < nBudgetMs;
                bool bTimeLeft = nWaitedMs < CAPTURE_WAIT_TIMEOUT_MS;
                if (!bBudgetLeft || !bTimeLeft)
                {
                    return null;
                }

                Thread.Sleep(CAPTURE_WAIT_POLL_MS);
                nWaitedMs += CAPTURE_WAIT_POLL_MS;
            }
        }

        /// <summary>
        /// 워커가 쓰는 도중에 읽으면 잘린 JPEG 이 나온다 → EOI 마커(FF D9)로 파일 완결 여부를 확인한다.
        /// </summary>
        private static byte[] TryReadCompleteJpeg(string szPath)
        {
            try
            {
                byte[] arrBytes = File.ReadAllBytes(szPath);
                if (arrBytes.Length < JPEG_MIN_BYTES)
                {
                    return null;
                }

                bool bHasJpegEoi = arrBytes[arrBytes.Length - 2] == 0xFF && arrBytes[arrBytes.Length - 1] == 0xD9;
                if (!bHasJpegEoi)
                {
                    return null;
                }

                return arrBytes;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 종횡비를 유지한 채 셀 박스 안에 맞춰 그림을 넣는다. 실패해도 export 는 계속된다.
        /// </summary>
        internal static bool TryInsertCaptureImage(IXLWorksheet ws, int nRow, int nColumn, byte[] arrBytes)
        {
            bool bHasBytes = arrBytes != null && arrBytes.Length > 0;
            if (!bHasBytes)
            {
                return false;
            }

            try
            {
                IXLPicture pic;
                using (var ms = new MemoryStream(arrBytes))
                {
                    pic = ws.AddPicture(ms, XLPictureFormat.Jpeg);
                }

                int nOriginalWidth = pic.OriginalWidth;
                int nOriginalHeight = pic.OriginalHeight;
                bool bInvalidSize = nOriginalWidth <= 0 || nOriginalHeight <= 0;
                if (bInvalidSize)
                {
                    pic.Delete();
                    return false;
                }

                double dScaleWidth = (double)CAPTURE_BOX_WIDTH_PX / nOriginalWidth;
                double dScaleHeight = (double)CAPTURE_BOX_HEIGHT_PX / nOriginalHeight;
                double dScale = dScaleWidth;
                if (dScaleHeight < dScale)
                {
                    dScale = dScaleHeight;
                }
                if (dScale > 1.0)
                {
                    dScale = 1.0;   // 원본보다 키우지 않는다
                }

                int nTargetWidth = (int)Math.Round(nOriginalWidth * dScale);
                int nTargetHeight = (int)Math.Round(nOriginalHeight * dScale);
                if (nTargetWidth < 1)
                {
                    nTargetWidth = 1;
                }
                if (nTargetHeight < 1)
                {
                    nTargetHeight = 1;
                }

                pic.WithPlacement(XLPicturePlacement.Move);
                pic.WithSize(nTargetWidth, nTargetHeight);
                pic.MoveTo(ws.Cell(nRow, nColumn));

                ws.Row(nRow).Height = CAPTURE_BOX_HEIGHT_PX * EXCEL_POINTS_PER_PIXEL;

                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[ExcelExportService] capture image insert failed (row " + nRow + "): " + ex.Message);
                }
                catch { }
                return false;
            }
        }

        /// <summary>
        /// 캡쳐이미지 컬럼 폭을 이미지 박스 폭에 맞춘다. AdjustToContents 는 그림을 고려하지 않으므로 '그 뒤에' 불러야 한다.
        /// </summary>
        internal static void ApplyCaptureColumnWidth(IXLWorksheet ws, int nColumn)
        {
            ws.Column(nColumn).Width = CAPTURE_BOX_WIDTH_PX / EXCEL_PIXELS_PER_WIDTH_UNIT;
        }

        /// <summary>
        /// 측정 1건의 판정 표시 문자열. DATUM_FAIL > NO_IMAGE > CROSS_Z_INCOMPLETE > MEASURE_FAIL > OK/NG > "-" 순서.
        /// ReviewMeasurementRow 로직과 일치해야 하며, 일괄검사 상세 시트도 이 함수를 공유한다.
        /// </summary>
        internal static string BuildJudgementText(MeasurementResultDto m)
        {
            if (m == null)
            {
                return "-";
            }

            if (m.LastSkipReason == SkipReason.DATUM_FAIL) //260710 hbk 상수화
            {
                return "DETECT FAIL";
            }
            else if (m.LastSkipReason == SkipReason.NO_IMAGE) //260616 hbk NO_IMAGE 라벨 //260710 hbk 상수화
            {
                return "NO IMAGE";
            }
            else if (m.LastSkipReason == SkipReason.CROSS_Z_INCOMPLETE) //260729 hbk quick-fix(260729-e9q): 크로스-Z 미측정 라벨 (ReviewMeasurementRow 로직 일치)
            {
                return "CROSS-Z INCOMPLETE";
            }
            else if (m.LastSkipReason == SkipReason.MEASURE_FAIL)
            {
                return ReviewMeasurementRow.JUDGE_MEASURE_FAIL;
            }
            else if (m.LastHasResult)
            {
                if (m.LastJudgement)
                {
                    return "OK";
                }
                else
                {
                    return "NG";
                }
            }
            else
            {
                return "-";
            }
        }
    }

    /// <summary>누적 엑셀에 담을 대상 — 사용자가 리뷰어에서 고른다.</summary>
    public enum EAccumExportScope
    {
        NgOnly,
        OkOnly,
        All
    }

    /// <summary>NG 누적 엑셀 저장 1회 실행 결과 상태. 메시지·아이콘은 NgAccumulationExportService 가 이미 조립한다.</summary>
    public enum ENgAccumExportStatus
    {
        Added,          // 새 NG 행을 1건 이상 추가하고 저장 완료
        NothingNew,     // 전부 이미 들어 있어 추가 0건(저장 안 함)
        NoNg,           // 날짜 폴더에 NG 가 없음(파일 안 건드림)
        NoCycles,       // 날짜 폴더에 cycle.json 이 하나도 없음(파일 안 건드림)
        NoFolder,       // 날짜 폴더를 열지 않았거나 존재하지 않음(파일 안 건드림)
        FileLocked,     // 출력 xlsx 가 다른 프로그램에 열려 있어 쓰기 전에 중단(파일 안 건드림)
        Failed          // 그 밖의 예외 — Error 로그 확인
    }

    /// <summary>NG 누적 엑셀 저장 1회 실행 결과. 화면에 그대로 보여 줄 한국어 메시지·아이콘까지 담는다.</summary>
    public class NgAccumExportOutcome
    {
        public ENgAccumExportStatus Status { get; set; }

        public int AddedCount { get; set; }

        public int SkippedDuplicateCount { get; set; }

        public int UnreadableCycleCount { get; set; }

        public int TotalDataRows { get; set; }

        public string Message { get; set; } = "";

        public MessageBoxImage Icon { get; set; }
    }

    /// <summary>
    /// 리뷰어가 연 날짜 폴더의 NG 측정만 한 xlsx 파일(NG_분석_누적.xlsx, 시트 "NG 누적")에 중복 없이 계속 누적한다(D-78-02).
    /// 원인·근거·확인할 일·함께 의심·원인 코드는 화면 패널과 같은 NgCauseAnalyzer.Analyze 결과를 그대로 쓴다(D-78-04).
    /// 이 클래스는 이 날짜 폴더의 cycle.json·사진을 읽기만 한다 — 쓰기는 출력 xlsx(와 같은 폴더 임시 xlsx) 뿐이다(PR-5).
    /// </summary>
    public static class NgAccumulationExportService
    {
        public const string OUTPUT_FILE_NAME = "NG_분석_누적.xlsx";
        public const string SHEET_NAME = "NG 누적";

        /// <summary>OK 측정을 쌓는 시트 — 같은 파일 안에서 NG 와 나눠 쌓는다.</summary>
        public const string SHEET_NAME_OK = "OK 누적";
        public const string MESSAGE_TITLE = "누적 엑셀";

        private const string TEMP_FILE_SUFFIX = "_저장중.xlsx";
        private const string CYCLE_JSON_NAME = "cycle.json";

        private const int HEADER_ROW = 1;
        private const int FIRST_DATA_ROW = 2;

        private const int COL_TIME = 1;
        private const int COL_KIND = 2;
        private const int COL_RECIPE = 3;
        private const int COL_MATERIAL = 4;
        private const int COL_SHOT = 5;
        private const int COL_FAI = 6;
        private const int COL_MEASUREMENT = 7;
        private const int COL_VALUE = 8;
        private const int COL_NOMINAL = 9;
        private const int COL_TOL_PLUS = 10;
        private const int COL_TOL_MINUS = 11;
        private const int COL_JUDGE = 12;
        private const int COL_SELECTED_Z = 13;
        private const int COL_CAUSE = 14;
        private const int COL_EVIDENCE = 15;
        private const int COL_ACTION = 16;
        private const int COL_SUSPECT = 17;
        private const int COL_CAUSE_CODE = 18;
        private const int COL_IMAGE_PATH = 19;
        private const int COL_CYCLE_FOLDER = 20;

        private const string TIME_FORMAT = "yyyy-MM-dd HH:mm:ss";
        private const string AUTO_TEXT = "자동";
        private const string MANUAL_TEXT = "수동";
        private const string NO_VALUE_TEXT = "-";
        private const string KEY_SEPARATOR = "";
        private const int LARGE_FILE_ROW_WARNING = 100000;

        private const string MSG_NO_FOLDER = "먼저 '날짜 폴더 열기' 로 날짜 폴더를 여세요.";
        private const string MSG_NO_CYCLES = "이 폴더에는 검사 결과(cycle.json)가 없습니다. 날짜 폴더(예: 20260916)를 여세요.";
        private const string MSG_NO_NG = "이 날짜 폴더에는 저장할 측정이 없습니다 (추가 0건).";
        private const string MSG_NOTHING_NEW_FORMAT = "새로 추가할 측정이 없습니다 — 이미 들어 있는 {0}건은 건너뛰었습니다.";
        private const string MSG_ADDED_FORMAT = "{0}건을 추가했습니다 (이미 있던 {1}건 건너뜀).";
        private const string MSG_FILE_LOCKED = "누적 엑셀 파일이 열려 있어 저장하지 못했습니다. 엑셀을 닫고 다시 누르세요.";
        private const string MSG_FAILED = "누적 엑셀 저장에 실패했습니다 (Error 로그 확인).";
        private const string MSG_UNREADABLE_FORMAT = "읽지 못한 검사 결과 {0}건 — 저장 중이었거나 손상된 파일입니다. 잠시 후 다시 누르면 추가됩니다.";
        private const string MSG_LARGE_FILE_FORMAT = "파일이 커졌습니다({0}행). 파일 이름을 바꿔 보관하면 다음부터 새 파일로 시작합니다.";

        private static readonly string[] HEADER_TEXTS = new string[]
        {
            "검사시각", "검사구분", "레시피", "자재번호", "Shot", "FAI", "측정명", "측정값",
            "공칭", "공차+", "공차-", "판정", "사용 Z", "추정 원인", "근거", "확인할 일",
            "함께 의심", "원인 코드", "사진 경로", "사이클 폴더"
        };

        // 날짜 폴더에서 읽은 cycle.json 1건(DTO + 실제 스캔한 하위 폴더 경로).
        private class LoadedCycle
        {
            public CycleResultDto Dto;
            public string FolderPath;
        }

        // xlsx 한 행에 들어갈 값(규격 표 20열)과 중복 판정용 키.
        private class NgRowData
        {
            /// <summary>true = NG 행("NG 누적" 시트), false = OK 행("OK 누적" 시트).</summary>
            public bool IsNg = true;

            public DateTime InspectionTime;
            public string Kind;
            public string Recipe;
            public string Material;
            public string Shot;
            public string Fai;
            public string Measurement;
            public bool HasValue;
            public double Value;
            public double Nominal;
            public double TolPlus;
            public double TolMinus;
            public string Judge;
            public string SelectedZ;
            public string CauseText;
            public string EvidenceText;
            public string ActionText;
            public string SuspectText;
            public string CauseCode;
            public string ImagePath;
            public string CycleFolder;
            public string DupKey;
        }

        /// <summary>ResultSavePath 아래 NG_분석_누적.xlsx 절대 경로. 경로가 비어 있으면 빈 문자열.</summary>
        public static string BuildOutputPath(string szResultSavePath)
        {
            if (string.IsNullOrEmpty(szResultSavePath))
            {
                return "";
            }
            return Path.Combine(szResultSavePath, OUTPUT_FILE_NAME);
        }

        /// <summary>
        /// 날짜 폴더(szDateFolderPath)의 NG 측정만 szOutputPath 의 "NG 누적" 시트에 중복 없이 추가한다.
        /// 어떤 경우에도 예외를 밖으로 내보내지 않는다.
        /// </summary>
        public static NgAccumExportOutcome AppendDateFolder(string szDateFolderPath, string szOutputPath)
        {
            return AppendDateFolder(szDateFolderPath, szOutputPath, EAccumExportScope.NgOnly);
        }

        /// <summary>저장 대상(NG만·OK만·전체)을 골라 누적한다. 전체면 NG 는 "NG 누적", OK 는 "OK 누적" 시트로 나눠 쌓인다.</summary>
        public static NgAccumExportOutcome AppendDateFolder(string szDateFolderPath, string szOutputPath, EAccumExportScope scope)
        {
            try
            {
                return AppendDateFolderInternal(szDateFolderPath, szOutputPath, scope);
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
                return BuildOutcome(ENgAccumExportStatus.Failed, 0, 0, 0, 0, szOutputPath);
            }
        }

        private static NgAccumExportOutcome AppendDateFolderInternal(string szDateFolderPath, string szOutputPath, EAccumExportScope scope)
        {
            bool bFolderMissing = string.IsNullOrEmpty(szDateFolderPath) || !Directory.Exists(szDateFolderPath);
            if (bFolderMissing)
            {
                return BuildOutcome(ENgAccumExportStatus.NoFolder, 0, 0, 0, 0, szOutputPath);
            }

            if (string.IsNullOrEmpty(szOutputPath))
            {
                return BuildOutcome(ENgAccumExportStatus.Failed, 0, 0, 0, 0, szOutputPath);
            }

            int nUnreadable;
            List<LoadedCycle> lstCycles = LoadDateFolderCycles(szDateFolderPath, out nUnreadable);

            bool bNoCycles = lstCycles.Count == 0 && nUnreadable == 0;
            if (bNoCycles)
            {
                return BuildOutcome(ENgAccumExportStatus.NoCycles, 0, 0, nUnreadable, 0, szOutputPath);
            }

            NgCauseHistory history = new NgCauseHistory();
            for (int i = 0; i < lstCycles.Count; i++)
            {
                history.AddCycle(lstCycles[i].Dto);
            }

            List<NgRowData> lstRows = BuildNgRows(lstCycles, history, scope);
            if (lstRows.Count == 0)
            {
                return BuildOutcome(ENgAccumExportStatus.NoNg, 0, 0, nUnreadable, 0, szOutputPath);
            }

            bool bFileExists = File.Exists(szOutputPath);
            if (bFileExists)
            {
                bool bLocked = IsFileLocked(szOutputPath);
                if (bLocked)
                {
                    return BuildOutcome(ENgAccumExportStatus.FileLocked, 0, 0, nUnreadable, 0, szOutputPath);
                }
            }

            return WriteRows(lstRows, szOutputPath, bFileExists, nUnreadable);
        }

        // 하위 폴더를 이름 ordinal 정렬해 cycle.json 있는 것만 로드한다. 손상 파일은 nUnreadable 로 센다.
        private static List<LoadedCycle> LoadDateFolderCycles(string szDateFolderPath, out int nUnreadable)
        {
            nUnreadable = 0;
            List<LoadedCycle> lstResult = new List<LoadedCycle>();

            string[] arrDirs = Directory.GetDirectories(szDateFolderPath);
            Array.Sort(arrDirs, StringComparer.Ordinal);

            for (int i = 0; i < arrDirs.Length; i++)
            {
                string szDir = arrDirs[i];
                string szJsonPath = Path.Combine(szDir, CYCLE_JSON_NAME);
                bool bHasJson = File.Exists(szJsonPath);
                if (!bHasJson)
                {
                    continue;
                }

                CycleResultDto dto = CycleResultSerializer.Load(szJsonPath);
                if (dto == null)
                {
                    nUnreadable++;
                    continue;
                }

                LoadedCycle loaded = new LoadedCycle();
                loaded.Dto = dto;
                loaded.FolderPath = szDir;
                lstResult.Add(loaded);
            }

            lstResult.Sort(CompareLoadedCycle);
            return lstResult;
        }

        // 정렬 순서: InspectionTime 오름차순 → 사이클 폴더 ordinal.
        private static int CompareLoadedCycle(LoadedCycle a, LoadedCycle b)
        {
            int nTimeCompare = a.Dto.InspectionTime.CompareTo(b.Dto.InspectionTime);
            if (nTimeCompare != 0)
            {
                return nTimeCompare;
            }
            return string.CompareOrdinal(a.FolderPath, b.FolderPath);
        }

        // 사이클·Shot·FAI·측정 순서대로 저장 대상(scope)에 해당하는 것만 행 데이터로 만든다.
        //  NG 는 기존대로 원인 분석까지 하고, OK 는 원인 칸을 비운 채 값만 쌓는다(분석 대상이 아니다).
        private static List<NgRowData> BuildNgRows(List<LoadedCycle> lstCycles, NgCauseHistory history, EAccumExportScope scope)
        {
            List<NgRowData> lstRows = new List<NgRowData>();

            for (int c = 0; c < lstCycles.Count; c++)
            {
                LoadedCycle loaded = lstCycles[c];
                CycleResultDto dto = loaded.Dto;
                if (dto.Shots == null)
                {
                    continue;
                }

                foreach (var shot in dto.Shots)
                {
                    if (shot == null || shot.FAIs == null)
                    {
                        continue;
                    }

                    foreach (var fai in shot.FAIs)
                    {
                        if (fai == null || fai.Measurements == null)
                        {
                            continue;
                        }

                        foreach (var m in fai.Measurements)
                        {
                            if (m == null)
                            {
                                continue;
                            }

                            bool bIsNg = NgCauseAnalyzer.IsNgMeasurement(m);
                            bool bIsOk = !bIsNg && m.LastHasResult;
                            bool bWantNg = scope == EAccumExportScope.NgOnly || scope == EAccumExportScope.All;
                            bool bWantOk = scope == EAccumExportScope.OkOnly || scope == EAccumExportScope.All;
                            bool bTake;
                            if (bIsNg)
                            {
                                bTake = bWantNg;
                            }
                            else if (bIsOk)
                            {
                                bTake = bWantOk;
                            }
                            else
                            {
                                bTake = false; // 측정값 자체가 없는 행(미측정)은 누적하지 않는다.
                            }
                            if (!bTake)
                            {
                                continue;
                            }

                            NgCauseResult cause;
                            if (bIsNg)
                            {
                                cause = NgCauseAnalyzer.Analyze(dto, shot, fai, m, history);
                            }
                            else
                            {
                                cause = NgCauseResult.Empty();
                            }
                            NgRowData row = BuildRowData(dto, loaded.FolderPath, shot, fai, m, cause);
                            row.IsNg = bIsNg;
                            lstRows.Add(row);
                        }
                    }
                }
            }

            return lstRows;
        }

        // 규격 표 20열 그대로 채운다(P-14 사진 우선순위·CycleFolderPath 폴백 포함).
        private static NgRowData BuildRowData(CycleResultDto dto, string szScannedFolder, ShotResultDto shot, FaiResultDto fai, MeasurementResultDto m, NgCauseResult cause)
        {
            NgRowData row = new NgRowData();
            row.InspectionTime = dto.InspectionTime;

            string szKind;
            if (dto.IsProtocolDriven)
            {
                szKind = AUTO_TEXT;
            }
            else
            {
                szKind = MANUAL_TEXT;
            }
            row.Kind = szKind;

            string szRecipe = dto.RecipeName;
            if (szRecipe == null)
            {
                szRecipe = "";
            }
            row.Recipe = szRecipe;

            bool bHasMaterial = dto.IndexNumber >= 0;
            string szMaterial;
            if (bHasMaterial)
            {
                szMaterial = dto.IndexNumber.ToString();
            }
            else
            {
                szMaterial = NO_VALUE_TEXT;
            }
            row.Material = szMaterial;

            string szShotName = shot.ShotName;
            if (szShotName == null)
            {
                szShotName = "";
            }
            row.Shot = szShotName;

            string szFaiName = fai.FAIName;
            if (szFaiName == null)
            {
                szFaiName = "";
            }
            row.Fai = szFaiName;

            string szMeasName = m.MeasurementName;
            if (szMeasName == null)
            {
                szMeasName = "";
            }
            row.Measurement = szMeasName;

            row.HasValue = m.LastHasResult;
            row.Value = m.LastMeasuredValue;
            row.Nominal = m.NominalValue;
            row.TolPlus = m.TolerancePlus;
            row.TolMinus = m.ToleranceMinus;

            row.Judge = ExcelExportService.BuildJudgementText(m);
            row.SelectedZ = MeasurementBase.FormatSelectedZ(m.SelectedZIndex);

            row.CauseText = cause.CauseText;
            row.EvidenceText = cause.EvidenceText;
            row.ActionText = cause.ActionText;
            row.SuspectText = cause.SuspectText;
            row.CauseCode = cause.CauseCode;

            string szImagePath = fai.OriginImageFileName;
            bool bImageEmpty = string.IsNullOrEmpty(szImagePath);
            if (bImageEmpty)
            {
                szImagePath = shot.ResultImagePath;
            }
            row.ImagePath = szImagePath;

            string szCycleFolder = dto.CycleFolderPath;
            bool bCycleFolderEmpty = string.IsNullOrEmpty(szCycleFolder);
            if (bCycleFolderEmpty)
            {
                szCycleFolder = szScannedFolder;
            }
            row.CycleFolder = szCycleFolder;

            row.DupKey = BuildDupKey(row.CycleFolder, row.Shot, row.Fai, row.Measurement);

            return row;
        }

        private static string BuildDupKey(string szCycleFolder, string szShot, string szFai, string szMeasurement)
        {
            return NormalizeFolderKey(szCycleFolder) + KEY_SEPARATOR + szShot + KEY_SEPARATOR + szFai + KEY_SEPARATOR + szMeasurement;
        }

        private static string NormalizeFolderKey(string szFolder)
        {
            if (szFolder == null)
            {
                return "";
            }
            return szFolder.TrimEnd('\\', '/').ToUpperInvariant();
        }

        // FileShare.None 으로 열어 보는 것만으로 잠금 여부를 판정한다(쓰기 전 선검사).
        private static bool IsFileLocked(string szPath)
        {
            try
            {
                using (FileStream fs = new FileStream(szPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        private static XLWorkbook OpenOrCreateWorkbook(bool bFileExists, string szOutputPath)
        {
            if (bFileExists)
            {
                return new XLWorkbook(szOutputPath);
            }
            return new XLWorkbook();
        }

        private static int ResolveNextRow(IXLWorksheet ws)
        {
            IXLRow usedRow = ws.LastRowUsed();
            int nLastRow = HEADER_ROW;
            bool bHasUsedRow = usedRow != null;
            if (bHasUsedRow)
            {
                nLastRow = usedRow.RowNumber();
            }

            int nNextRow = nLastRow + 1;
            if (nNextRow < FIRST_DATA_ROW)
            {
                nNextRow = FIRST_DATA_ROW;
            }
            return nNextRow;
        }

        private static void WriteHeader(IXLWorksheet ws)
        {
            for (int i = 0; i < HEADER_TEXTS.Length; i++)
            {
                ws.Cell(HEADER_ROW, i + 1).Value = HEADER_TEXTS[i];
            }
        }

        // 기존 행 키를 전부 읽어 온다. 이 행 키에 이번에 추가하는 행 키도 계속 더해져 같은 실행 안 중복도 막는다.
        private static HashSet<string> ReadExistingKeys(IXLWorksheet ws)
        {
            HashSet<string> setKeys = new HashSet<string>(StringComparer.Ordinal);

            IXLRow usedRow = ws.LastRowUsed();
            bool bHasData = usedRow != null && usedRow.RowNumber() >= FIRST_DATA_ROW;
            if (!bHasData)
            {
                return setKeys;
            }

            int nLastRow = usedRow.RowNumber();
            for (int r = FIRST_DATA_ROW; r <= nLastRow; r++)
            {
                string szShot = ws.Cell(r, COL_SHOT).GetString();
                string szFai = ws.Cell(r, COL_FAI).GetString();
                string szMeasurement = ws.Cell(r, COL_MEASUREMENT).GetString();
                string szCycleFolder = ws.Cell(r, COL_CYCLE_FOLDER).GetString();
                string szKey = BuildDupKey(szCycleFolder, szShot, szFai, szMeasurement);
                setKeys.Add(szKey);
            }
            return setKeys;
        }

        private static void WriteRowCells(IXLWorksheet ws, int nRow, NgRowData row)
        {
            ws.Cell(nRow, COL_TIME).Value = row.InspectionTime.ToString(TIME_FORMAT);
            ws.Cell(nRow, COL_KIND).Value = row.Kind;
            ws.Cell(nRow, COL_RECIPE).Value = row.Recipe;
            ws.Cell(nRow, COL_MATERIAL).Value = row.Material;
            ws.Cell(nRow, COL_SHOT).Value = row.Shot;
            ws.Cell(nRow, COL_FAI).Value = row.Fai;
            ws.Cell(nRow, COL_MEASUREMENT).Value = row.Measurement;

            if (row.HasValue)
            {
                ws.Cell(nRow, COL_VALUE).Value = row.Value;
            }
            else
            {
                ws.Cell(nRow, COL_VALUE).Value = NO_VALUE_TEXT;
            }

            ws.Cell(nRow, COL_NOMINAL).Value = row.Nominal;
            ws.Cell(nRow, COL_TOL_PLUS).Value = row.TolPlus;
            ws.Cell(nRow, COL_TOL_MINUS).Value = row.TolMinus;
            ws.Cell(nRow, COL_JUDGE).Value = row.Judge;
            ws.Cell(nRow, COL_SELECTED_Z).Value = row.SelectedZ;
            ws.Cell(nRow, COL_CAUSE).Value = row.CauseText;
            ws.Cell(nRow, COL_EVIDENCE).Value = row.EvidenceText;
            ws.Cell(nRow, COL_ACTION).Value = row.ActionText;
            ws.Cell(nRow, COL_SUSPECT).Value = row.SuspectText;
            ws.Cell(nRow, COL_CAUSE_CODE).Value = row.CauseCode;

            string szImagePath = row.ImagePath;
            if (szImagePath == null)
            {
                szImagePath = "";
            }
            ws.Cell(nRow, COL_IMAGE_PATH).Value = szImagePath;

            ws.Cell(nRow, COL_CYCLE_FOLDER).Value = row.CycleFolder;
        }

        // 시트 열기/새로 만들기 → 중복 제외 추가 → (추가 0 이면 저장 안 함) → 임시 xlsx SaveAs → File.Replace/Move.
        private static NgAccumExportOutcome WriteRows(List<NgRowData> lstRows, string szOutputPath, bool bFileExists, int nUnreadable)
        {
            int nAdded = 0;
            int nSkippedDuplicate = 0;
            int nTotalDataRows = 0;
            bool bHasNewRows;

            string szTempPath = BuildTempPath(szOutputPath);
            TryDeleteTempFile(szTempPath);

            using (XLWorkbook wb = OpenOrCreateWorkbook(bFileExists, szOutputPath))
            {
                // NG 행과 OK 행을 각자 시트에 쌓는다 — 시트마다 중복 판정·다음 행을 따로 본다.
                List<NgRowData> lstNg = new List<NgRowData>();
                List<NgRowData> lstOk = new List<NgRowData>();
                for (int i = 0; i < lstRows.Count; i++)
                {
                    if (lstRows[i].IsNg)
                    {
                        lstNg.Add(lstRows[i]);
                    }
                    else
                    {
                        lstOk.Add(lstRows[i]);
                    }
                }

                int nAddedNg = 0;
                int nTotalNg = 0;
                AppendRowsToSheet(wb, SHEET_NAME, lstNg, out nAddedNg, out nTotalNg);
                int nAddedOk = 0;
                int nTotalOk = 0;
                AppendRowsToSheet(wb, SHEET_NAME_OK, lstOk, out nAddedOk, out nTotalOk);

                nAdded = nAddedNg + nAddedOk;
                nSkippedDuplicate = lstRows.Count - nAdded;
                nTotalDataRows = nTotalNg + nTotalOk;

                bHasNewRows = nAdded > 0;
                if (bHasNewRows)
                {
                    wb.SaveAs(szTempPath);
                }
            }

            if (!bHasNewRows)
            {
                return BuildOutcome(ENgAccumExportStatus.NothingNew, 0, nSkippedDuplicate, nUnreadable, 0, szOutputPath);
            }

            try
            {
                bool bTargetExists = File.Exists(szOutputPath);
                if (bTargetExists)
                {
                    File.Replace(szTempPath, szOutputPath, null);
                }
                else
                {
                    File.Move(szTempPath, szOutputPath);
                }
            }
            catch (IOException)
            {
                TryDeleteTempFile(szTempPath);
                return BuildOutcome(ENgAccumExportStatus.FileLocked, nAdded, nSkippedDuplicate, nUnreadable, nTotalDataRows, szOutputPath);
            }
            catch (Exception ex)
            {
                TryDeleteTempFile(szTempPath);
                LogError(ex.Message);
                return BuildOutcome(ENgAccumExportStatus.Failed, nAdded, nSkippedDuplicate, nUnreadable, nTotalDataRows, szOutputPath);
            }

            return BuildOutcome(ENgAccumExportStatus.Added, nAdded, nSkippedDuplicate, nUnreadable, nTotalDataRows, szOutputPath);
        }

        // 시트 하나에 행을 쌓는다 — 없으면 머리글과 함께 만들고, 이미 있는 키는 건너뛴다.
        private static void AppendRowsToSheet(XLWorkbook wb, string szSheetName, List<NgRowData> lstRows, out int nAdded, out int nTotalDataRows)
        {
            nAdded = 0;
            nTotalDataRows = 0;
            if (lstRows.Count == 0)
            {
                IXLWorksheet wsExisting;
                bool bFound = wb.Worksheets.TryGetWorksheet(szSheetName, out wsExisting);
                if (bFound)
                {
                    nTotalDataRows = (ResolveNextRow(wsExisting) - 1) - HEADER_ROW;
                }
                return;
            }

            IXLWorksheet ws;
            bool bHasSheet = wb.Worksheets.TryGetWorksheet(szSheetName, out ws);
            bool bNewSheet = !bHasSheet;
            if (bNewSheet)
            {
                ws = wb.Worksheets.Add(szSheetName);
                WriteHeader(ws);
            }

            HashSet<string> setExistingKeys = ReadExistingKeys(ws);
            int nRow = ResolveNextRow(ws);

            for (int i = 0; i < lstRows.Count; i++)
            {
                NgRowData row = lstRows[i];
                bool bDuplicate = setExistingKeys.Contains(row.DupKey);
                if (bDuplicate)
                {
                    continue;
                }

                WriteRowCells(ws, nRow, row);
                setExistingKeys.Add(row.DupKey);
                nAdded++;
                nRow++;
            }

            nTotalDataRows = (nRow - 1) - HEADER_ROW;
            if (bNewSheet && nAdded > 0)
            {
                ws.Columns().AdjustToContents();
            }
        }

        private static string BuildTempPath(string szOutputPath)
        {
            string szDirectory = Path.GetDirectoryName(szOutputPath);
            string szFileNameNoExt = Path.GetFileNameWithoutExtension(szOutputPath);
            return Path.Combine(szDirectory, szFileNameNoExt + TEMP_FILE_SUFFIX);
        }

        // 임시 xlsx 삭제는 이 메서드 안 File.Delete 1곳뿐이다(PR-5).
        private static void TryDeleteTempFile(string szTempPath)
        {
            bool bExists = File.Exists(szTempPath);
            if (!bExists)
            {
                return;
            }
            try
            {
                File.Delete(szTempPath);
            }
            catch
            {
            }
        }

        private static void LogError(string szMessage)
        {
            try
            {
                Logging.PrintErrLog((int)ELogType.Error, "[NgAccumulationExportService] " + szMessage);
            }
            catch
            {
            }
        }

        // 상태별 한국어 메시지·아이콘 조립(규격 문구 그대로). 읽지 못한 건수·대용량 안내 줄을 덧붙인다.
        private static NgAccumExportOutcome BuildOutcome(ENgAccumExportStatus status, int nAdded, int nSkippedDuplicate, int nUnreadable, int nTotalDataRows, string szOutputPath)
        {
            NgAccumExportOutcome outcome = new NgAccumExportOutcome();
            outcome.Status = status;
            outcome.AddedCount = nAdded;
            outcome.SkippedDuplicateCount = nSkippedDuplicate;
            outcome.UnreadableCycleCount = nUnreadable;
            outcome.TotalDataRows = nTotalDataRows;

            string szMessage;
            MessageBoxImage icon;

            if (status == ENgAccumExportStatus.NoFolder)
            {
                szMessage = MSG_NO_FOLDER;
                icon = MessageBoxImage.Warning;
            }
            else if (status == ENgAccumExportStatus.NoCycles)
            {
                szMessage = MSG_NO_CYCLES + "\n" + szOutputPath;
                icon = MessageBoxImage.Warning;
            }
            else if (status == ENgAccumExportStatus.NoNg)
            {
                szMessage = MSG_NO_NG + "\n" + szOutputPath;
                icon = MessageBoxImage.Information;
            }
            else if (status == ENgAccumExportStatus.NothingNew)
            {
                szMessage = string.Format(MSG_NOTHING_NEW_FORMAT, nSkippedDuplicate) + "\n" + szOutputPath;
                icon = MessageBoxImage.Information;
            }
            else if (status == ENgAccumExportStatus.Added)
            {
                szMessage = string.Format(MSG_ADDED_FORMAT, nAdded, nSkippedDuplicate) + "\n" + szOutputPath;
                icon = MessageBoxImage.Information;
            }
            else if (status == ENgAccumExportStatus.FileLocked)
            {
                szMessage = MSG_FILE_LOCKED + "\n" + szOutputPath;
                icon = MessageBoxImage.Warning;
            }
            else
            {
                szMessage = MSG_FAILED + "\n" + szOutputPath;
                icon = MessageBoxImage.Error;
            }

            bool bStatusEligibleForUnreadableNote;
            if (status == ENgAccumExportStatus.Added)
            {
                bStatusEligibleForUnreadableNote = true;
            }
            else if (status == ENgAccumExportStatus.NothingNew)
            {
                bStatusEligibleForUnreadableNote = true;
            }
            else if (status == ENgAccumExportStatus.NoNg)
            {
                bStatusEligibleForUnreadableNote = true;
            }
            else
            {
                bStatusEligibleForUnreadableNote = false;
            }

            bool bAppendUnreadable = nUnreadable > 0 && bStatusEligibleForUnreadableNote;
            if (bAppendUnreadable)
            {
                szMessage = szMessage + "\n" + string.Format(MSG_UNREADABLE_FORMAT, nUnreadable);
            }

            bool bAppendLargeFile = status == ENgAccumExportStatus.Added && nTotalDataRows >= LARGE_FILE_ROW_WARNING;
            if (bAppendLargeFile)
            {
                szMessage = szMessage + "\n" + string.Format(MSG_LARGE_FILE_FORMAT, nTotalDataRows);
            }

            outcome.Message = szMessage;
            outcome.Icon = icon;
            return outcome;
        }
    }
}
