using ReringProject.UI;

namespace ReringProject.UI
{
    /// <summary>
    /// 리뷰어 DataGrid 행 DTO. CycleResultDto.MeasurementResultDto 를 그리드 표시용으로 래핑.
    /// Observable 상속 불필요 — 순수 직렬화 결과 DTO 래퍼, 일회성 바인딩.
    /// JudgeText: 3분기 (DATUM_FAIL / HasResult·OK·NG / 미측정 '—').
    /// </summary>
    public class ReviewMeasurementRow
    {
        public string ShotName { get; set; }

        public string FAIName { get; set; }

        public string MeasurementName { get; set; }

        public double NominalValue { get; set; }

        public double TolerancePlus { get; set; }

        public double ToleranceMinus { get; set; }

        /// <summary>LastHasResult ? LastMeasuredValue.ToString("F4") : "—" — 0.0 도 정상값으로 표시 (CO-23-01)</summary>
        public string ResultDisplay { get; set; }

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

            // 3분기: DATUM_FAIL > HasResult 유무 > OK/NG
            if (m.LastSkipReason == "DATUM_FAIL")
            {
                JudgeText = "DETECT FAIL";
            }
            else if (m.LastSkipReason == "NO_IMAGE") //260616 hbk NO_IMAGE 라벨
            {
                JudgeText = "NO IMAGE";
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
    }
}
