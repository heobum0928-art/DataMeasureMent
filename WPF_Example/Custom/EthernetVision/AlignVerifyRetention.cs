using System;
using System.IO;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject {

    /// <summary>
    /// Align 정합 검증 산출물(CSV + NG 증거 이미지)의 보관 상한 정리.
    /// 프로그램 시작 시 SystemHandler.Initialize() 에서 1회 호출된다.
    ///
    /// 이미지를 기존 검사 이미지 트리(Image\{yyMMdd}\{HHmm}\capture)에 섞지 않는 이유:
    /// 보관 정책을 검사 이미지와 독립적으로 걸기 위해서다. 섞이면 Align 증거만 골라 지울 수 없다.
    ///
    /// 이 클래스는 절대 throw 하지 않는다 — 시작 경로에서 호출되므로 실패가 초기화를 막으면 안 된다.
    /// </summary>
    public static class AlignVerifyRetention {

        /// <summary>{ResultSavePath}\AlignVerify\{yyMMdd}\ 의 루트 폴더명.</summary>
        public const string ALIGN_IMAGE_ROOT_FOLDER = "AlignVerify";

        private const string IMAGE_DATE_FORMAT = "yyMMdd";
        private const string CSV_SEARCH_PATTERN = "*.csv";

        /// <summary>NG 증거 이미지 저장 폴더. 75-03 이 이 헬퍼로 저장 경로를 만든다.</summary>
        public static string BuildAlignImageDirectory(DateTime ts) {
            return Path.Combine(SystemHandler.Handle.Setting.ResultSavePath,
                                ALIGN_IMAGE_ROOT_FOLDER, ts.ToString(IMAGE_DATE_FORMAT));
        }

        /// <summary>보관 일수를 넘긴 CSV 파일과 이미지 폴더를 지운다. 실패해도 throw 하지 않는다.</summary>
        public static void Cleanup() {
            int nCsvDeleted = 0;
            int nImageDirDeleted = 0;
            int nCsvKeepDays = 0;
            int nImageKeepDays = 0;

            try {
                nCsvKeepDays = SystemHandler.Handle.Setting.AlignVerifyKeepDays;
                nImageKeepDays = SystemHandler.Handle.Setting.AlignVerifyImageKeepDays;

                nCsvDeleted = CleanupCsv(nCsvKeepDays);
                nImageDirDeleted = CleanupImageDirectories(nImageKeepDays);

                Logging.PrintLog((int)ELogType.Trace,
                    "[AlignVerifyRetention] csv={0}건 image={1}폴더 삭제 (보관 {2}/{3}일)",
                    nCsvDeleted, nImageDirDeleted, nCsvKeepDays, nImageKeepDays);
            }
            catch (Exception ex) {
                try {
                    Logging.PrintErrLog((int)ELogType.Error,
                        "[AlignVerifyRetention] Cleanup failed(무시): " + ex.Message);
                }
                catch { }
            }
        }

        /// <summary>AlignVerifySavePath 안의 *.csv 중 보관 일수를 넘긴 것을 지운다.</summary>
        private static int CleanupCsv(int nKeepDays) {
            int nDeleted = 0;
            bool bKeepDaysInvalid = nKeepDays <= 0;
            if (bKeepDaysInvalid) {
                return nDeleted;
            }

            string szDir = SystemHandler.Handle.Setting.AlignVerifySavePath;
            if (string.IsNullOrEmpty(szDir)) {
                return nDeleted;
            }
            if (Directory.Exists(szDir) == false) {
                return nDeleted;
            }

            DateTime dtLimit = DateTime.Now.AddDays(-nKeepDays);
            DirectoryInfo dirInfo = new DirectoryInfo(szDir);
            foreach (FileInfo file in dirInfo.GetFiles(CSV_SEARCH_PATTERN)) {
                if (file.CreationTime < dtLimit) {
                    try {
                        file.Delete();
                        nDeleted = nDeleted + 1;
                    }
                    catch { }
                }
            }
            return nDeleted;
        }

        /// <summary>
        /// {ResultSavePath}\AlignVerify\ 아래 날짜 하위 디렉터리 중 보관 일수를 넘긴 것을 통째로 지운다.
        /// 삭제 대상은 하위 디렉터리로 한정한다 — 루트(AlignVerify) 자체는 절대 지우지 않는다.
        /// </summary>
        private static int CleanupImageDirectories(int nKeepDays) {
            int nDeleted = 0;
            bool bKeepDaysInvalid = nKeepDays <= 0;
            if (bKeepDaysInvalid) {
                return nDeleted;
            }

            string szResultRoot = SystemHandler.Handle.Setting.ResultSavePath;
            if (string.IsNullOrEmpty(szResultRoot)) {
                return nDeleted;
            }

            string szImageRoot = Path.Combine(szResultRoot, ALIGN_IMAGE_ROOT_FOLDER);
            if (Directory.Exists(szImageRoot) == false) {
                return nDeleted;
            }

            DateTime dtLimit = DateTime.Now.AddDays(-nKeepDays);
            DirectoryInfo rootInfo = new DirectoryInfo(szImageRoot);
            foreach (DirectoryInfo dir in rootInfo.GetDirectories()) {
                if (dir.CreationTime < dtLimit) {
                    try {
                        dir.Delete(true);
                        nDeleted = nDeleted + 1;
                    }
                    catch { }
                }
            }
            return nDeleted;
        }
    }
}
