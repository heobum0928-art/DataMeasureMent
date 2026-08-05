using ReringProject.Setting;

namespace ReringProject.Utility {

    /// <summary>
    /// 흐름 로그(Flow) 단일 진입점. 검사 사이클 1회당 요약 1줄만 남긴다.
    /// 공개 메서드는 CycleEnd 하나뿐이며, 검사 스레드를 보호하기 위해 절대 예외를 던지지 않는다.
    /// </summary>
    public static class FlowLog {

        // 검사 사이클 1회 요약 — 이 한 줄이 흐름 로그의 전부다.
        // 출력 예: "■ [SIDE] 사이클 종료 — 종합판정 NG (측정 30개 중 6개 벗어남, 소요 4.2초)"
        public static void CycleEnd(string szSequenceName, bool bAllPass, int nMeasured, int nNg, double dElapsedSec) {
            try {
                string szName;
                if (string.IsNullOrEmpty(szSequenceName)) {
                    szName = "이름없음";
                } else {
                    szName = szSequenceName;
                }

                string szJudgement;
                if (bAllPass == true) {
                    szJudgement = "OK";
                } else {
                    szJudgement = "NG";
                }

                string szElapsed = dElapsedSec.ToString("F1");

                string szMsg = string.Format(
                    "■ [{0}] 사이클 종료 — 종합판정 {1} (측정 {2}개 중 {3}개 벗어남, 소요 {4}초)",
                    szName, szJudgement, nMeasured, nNg, szElapsed);

                Logging.PrintLog((int)ELogType.Flow, szMsg);
            } catch {
            }
        }
    }
}
