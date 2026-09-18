using ReringProject.Sequence; //260710 hbk SkipReason 상수 참조용
using ReringProject.UI;
using System.Collections.Generic;
using System.IO;
using ReringProject.Halcon.Models;

namespace ReringProject.UI
{
    /// <summary>
    /// 리뷰어 DataGrid 행 DTO. CycleResultDto.MeasurementResultDto 를 그리드 표시용으로 래핑.
    /// Observable 상속 불필요 — 순수 직렬화 결과 DTO 래퍼, 일회성 바인딩.
    /// JudgeText: 3분기 (DATUM_FAIL / HasResult·OK·NG / 미측정 '—').
    /// </summary>
    public class ReviewMeasurementRow
    {
        /// <summary>SkipReason.MEASURE_FAIL 일 때의 JudgeText 라벨. ExcelExportService/ReviewerListLabelBuilder 가 재사용한다.</summary>
        public const string JUDGE_MEASURE_FAIL = "측정실패";

        /// <summary>SkipReason.Z_RANGE_PENDING 일 때의 JudgeText 라벨 — 중간 z tick 대기 표시(Phase 77 SZF-04).</summary>
        public const string JUDGE_Z_RANGE_PENDING = "Z 범위 대기";

        public string ShotName { get; set; }

        public string FAIName { get; set; }

        public string MeasurementName { get; set; }

        public double NominalValue { get; set; }

        public double TolerancePlus { get; set; }

        public double ToleranceMinus { get; set; }

        /// <summary>LastHasResult ? LastMeasuredValue.ToString("F4") : "—" — 0.0 도 정상값으로 표시 (CO-23-01)</summary>
        public string ResultDisplay { get; set; }

        /// <summary>범위 Shot 에서 이 측정이 채택한 z 번호 표시("z5"), 범위 미적용이면 빈칸(Phase 77 SZF-04).</summary>
        public string SelectedZText { get; set; }

        /// <summary>사용 기준 표시(Phase 79 LSR-04) — 국부 / 국부실패→전역, 옵션 꺼짐·옛 cycle.json 은 빈칸.</summary>
        public string RefSourceText { get; set; }

        /// <summary>NG 원인 규칙 판정 결과(Phase 78 NGA-01). 3인자 생성자로 만든 행은 기본값(Empty).</summary>
        public NgCauseResult Cause { get; set; } = NgCauseResult.Empty();

        /// <summary>NG 원인 패널에 바인딩되는 표시 문자열 — NgCauseAnalyzer.BuildPanelText 결과(Phase 78 NGA-02).</summary>
        public string CausePanelText { get; set; } = "";

        /// <summary>
        /// LastSkipReason == "DATUM_FAIL" → "DETECT FAIL" (Phase 39 WF-01 datum 검출 실패 표기).
        /// LastHasResult ? (LastJudgement ? "OK" : "NG") : "—"
        /// </summary>
        public string JudgeText { get; set; }

        // 행 클릭 시 해당 측정의 이미지/overlay 만 표시하기 위한 소유 객체 역참조.
        // 직렬화 결과 DTO 를 가리키는 in-memory 참조(직렬화 대상 아님).
        /// <summary>이 측정이 속한 Shot DTO (이미지 경로 출처).</summary>
        public ShotResultDto OwnerShot { get; set; }

        /// <summary>이 측정이 속한 FAI DTO (overlay 출처).</summary>
        public FaiResultDto OwnerFai { get; set; }

        /// <summary>이 측정의 원본 DTO (DualImage 여부/측정별 이미지 경로 판별용).</summary>
        public MeasurementResultDto Source { get; set; }

        /// <summary>ReviewerWindow 가 Shot/FAI 순회 시 소유 Shot/FAI DTO 를 주입한다.</summary>
        public ReviewMeasurementRow(ShotResultDto shot, FaiResultDto fai, MeasurementResultDto m)
        {
            OwnerShot = shot;
            OwnerFai = fai;
            Source = m;
            string tShotName;
            if (shot != null)
            {
                tShotName = shot.ShotName;
                if (tShotName == null) tShotName = "";
            }
            else
            {
                tShotName = "";
            }
            ShotName = tShotName;
            string tFaiName;
            if (fai != null)
            {
                tFaiName = fai.FAIName;
                if (tFaiName == null) tFaiName = "";
            }
            else
            {
                tFaiName = "";
            }
            FAIName = tFaiName;
            string tMeasName = m.MeasurementName;
            if (tMeasName == null) tMeasName = "";
            MeasurementName = tMeasName;
            NominalValue = m.NominalValue;
            TolerancePlus = m.TolerancePlus;
            ToleranceMinus = m.ToleranceMinus;

            // 0.0 도 정상 결과 — HasResult 플래그로 판별 (MeasuredValue != 0 센티넬 금지)
            if (m.LastHasResult) ResultDisplay = m.LastMeasuredValue.ToString("F4"); else ResultDisplay = "—";
            SelectedZText = MeasurementBase.FormatSelectedZ(m.SelectedZIndex);
            RefSourceText = MeasurementBase.FormatRefSource(m.RefSource);

            // 3분기: DATUM_FAIL > HasResult 유무 > OK/NG
            if (m.LastSkipReason == SkipReason.DATUM_FAIL) //260710 hbk 상수화
            {
                JudgeText = "DETECT FAIL";
            }
            else if (m.LastSkipReason == SkipReason.NO_IMAGE) //260616 hbk NO_IMAGE 라벨 //260710 hbk 상수화
            {
                JudgeText = "NO IMAGE";
            }
            else if (m.LastSkipReason == SkipReason.CROSS_Z_INCOMPLETE) //260729 hbk quick-fix(260729-e9q): 비프로토콜 실행 크로스-Z 미측정 — 일반 대기 표시와 반드시 구분
            {
                JudgeText = "CROSS-Z INCOMPLETE";
            }
            else if (m.LastSkipReason == SkipReason.Z_RANGE_PENDING)
            {
                JudgeText = JUDGE_Z_RANGE_PENDING;
            }
            else if (m.LastSkipReason == SkipReason.MEASURE_FAIL)
            {
                JudgeText = JUDGE_MEASURE_FAIL;
            }
            else if (m.LastHasResult)
            {
                if (m.LastJudgement) JudgeText = "OK"; else JudgeText = "NG";
            }
            else
            {
                JudgeText = "—";
            }
        }

        /// <summary>Phase 78 NGA-01: cycle/history 를 받아 NG 원인 판정까지 채우는 생성자 — 리뷰어 배선 전용.</summary>
        public ReviewMeasurementRow(ShotResultDto shot, FaiResultDto fai, MeasurementResultDto m, CycleResultDto cycle, NgCauseHistory history) : this(shot, fai, m)
        {
            Cause = NgCauseAnalyzer.Analyze(cycle, shot, fai, m, history);
            CausePanelText = NgCauseAnalyzer.BuildPanelText(Cause);
        }
    }

    /// <summary>
    /// 리뷰어가 띄울 사진 경로 — 그 검사의 실제 촬영 원본 우선, D-78-07. 순수 로직 + 파일 존재 확인만 한다.
    /// </summary>
    public static class ReviewerImagePathResolver
    {
        /// <summary>0바이트 = 워커가 막 만든 파일.</summary>
        public const long MIN_IMAGE_FILE_BYTES = 1;

        /// <summary>절대 경로·존재·크기 &gt; 0 을 전부 만족해야 사용 가능한 사진 파일이다.</summary>
        public static bool IsUsableImageFile(string szPath)
        {
            if (string.IsNullOrEmpty(szPath))
            {
                return false;
            }
            try
            {
                if (!Path.IsPathRooted(szPath))
                {
                    return false;
                }
                if (!File.Exists(szPath))
                {
                    return false;
                }
                FileInfo info = new FileInfo(szPath);
                if (info.Length < MIN_IMAGE_FILE_BYTES)
                {
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>기존 리뷰어 규칙 그대로 — ResultImagePath 폴백에만 쓴다(회귀 0).</summary>
        private static bool IsExistingFile(string szPath)
        {
            if (string.IsNullOrEmpty(szPath))
            {
                return false;
            }
            try
            {
                return File.Exists(szPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>이 FAI 가 측정 결과(또는 결과성 사유)를 가진 측정을 하나라도 가졌는지 — Z_RANGE_PENDING 뿐이면 false.</summary>
        private static bool FaiHasMeasuredResult(FaiResultDto fai)
        {
            if (fai == null || fai.Measurements == null)
            {
                return false;
            }
            foreach (var m in fai.Measurements)
            {
                if (m == null)
                {
                    continue;
                }
                if (m.LastHasResult)
                {
                    return true;
                }
                bool bHasPendingReason = string.IsNullOrEmpty(m.LastSkipReason) || m.LastSkipReason == SkipReason.Z_RANGE_PENDING;
                if (!bHasPendingReason)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>측정 행이 띄울 사진 — fai.OriginImageFileName 우선, 없으면 shot.ResultImagePath, 둘 다 없으면 null.</summary>
        public static string ResolveRowImagePath(ShotResultDto shot, FaiResultDto fai)
        {
            bool bOriginUsable = fai != null && IsUsableImageFile(fai.OriginImageFileName);
            if (bOriginUsable)
            {
                return fai.OriginImageFileName;
            }
            bool bFallbackUsable = shot != null && IsExistingFile(shot.ResultImagePath);
            if (bFallbackUsable)
            {
                return shot.ResultImagePath;
            }
            return null;
        }

        /// <summary>사이클 전체 보기가 띄울 사진 — 측정 결과가 있는 첫 FAI 의 원본, 없으면 첫 Shot 의 ResultImagePath.</summary>
        public static string ResolveCycleImagePath(CycleResultDto cycle)
        {
            if (cycle == null || cycle.Shots == null)
            {
                return null;
            }
            foreach (var shot in cycle.Shots)
            {
                string szOrigin = FindMeasuredOriginInShot(shot);
                if (!string.IsNullOrEmpty(szOrigin))
                {
                    return szOrigin;
                }
            }
            ShotResultDto firstShot = null;
            if (cycle.Shots.Count > 0)
            {
                firstShot = cycle.Shots[0];
            }
            if (firstShot == null)
            {
                return null;
            }
            if (IsExistingFile(firstShot.ResultImagePath))
            {
                return firstShot.ResultImagePath;
            }
            return null;
        }

        /// <summary>이 Shot 의 FAI 들 중 측정 결과가 있고 사용 가능한 원본을 가진 첫 FAI 의 원본 경로. 없으면 null.</summary>
        private static string FindMeasuredOriginInShot(ShotResultDto shot)
        {
            if (shot == null || shot.FAIs == null)
            {
                return null;
            }
            foreach (var fai in shot.FAIs)
            {
                if (fai == null)
                {
                    continue;
                }
                bool bMeasured = FaiHasMeasuredResult(fai);
                bool bUsable = bMeasured && IsUsableImageFile(fai.OriginImageFileName);
                if (bUsable)
                {
                    return fai.OriginImageFileName;
                }
            }
            return null;
        }

        /// <summary>Phase 80 함께 처리 1: 사이클 전체 보기가 띄울 사진 + 그 사진을 낸 Shot 을 함께 돌려준다
        /// (기존 오버로드와 같은 규칙) — 오버레이 겹침 버그 수정에 쓴다. 못 찾으면 ownerShot=null, 반환 null.</summary>
        public static string ResolveCycleImagePath(CycleResultDto cycle, out ShotResultDto ownerShot)
        {
            ownerShot = null;
            if (cycle == null || cycle.Shots == null)
            {
                return null;
            }
            foreach (var shot in cycle.Shots)
            {
                string szOrigin = FindMeasuredOriginInShot(shot);
                if (!string.IsNullOrEmpty(szOrigin))
                {
                    ownerShot = shot;
                    return szOrigin;
                }
            }
            ShotResultDto firstShot = null;
            if (cycle.Shots.Count > 0)
            {
                firstShot = cycle.Shots[0];
            }
            if (firstShot == null)
            {
                return null;
            }
            if (IsExistingFile(firstShot.ResultImagePath))
            {
                ownerShot = firstShot;
                return firstShot.ResultImagePath;
            }
            return null;
        }

        /// <summary>Phase 80 함께 처리 1: 이 Shot 의 FAI 선만 모은다 — 다른 Shot 선이 한 사진에 섞이지 않게.</summary>
        public static List<EdgeInspectionOverlay> CollectShotOverlays(ShotResultDto shot)
        {
            List<EdgeInspectionOverlay> lstResult = new List<EdgeInspectionOverlay>();
            bool bHasFais = shot != null && shot.FAIs != null;
            if (!bHasFais)
            {
                return lstResult;
            }
            foreach (var fai in shot.FAIs)
            {
                if (fai != null && fai.LastOverlays != null)
                {
                    lstResult.AddRange(fai.LastOverlays);
                }
            }
            return lstResult;
        }
    }
}
