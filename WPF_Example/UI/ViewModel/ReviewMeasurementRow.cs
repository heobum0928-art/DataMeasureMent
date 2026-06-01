//260601 hbk Phase 40 OUT-01 — ReviewerWindow DataGrid 행 DTO (D-05/D-09)
using ReringProject.UI;

namespace ReringProject.UI
{
    /// <summary>
    /// 리뷰어 DataGrid 행 DTO. CycleResultDto.MeasurementResultDto 를 그리드 표시용으로 래핑.
    /// Observable 상속 불필요 — 순수 직렬화 결과 DTO 래퍼, 일회성 바인딩.
    /// JudgeText: 3분기 (DATUM_FAIL/HasResult·OK·NG/미측정 '—') — MeasurementResultRow.cs:60 로직 답습.
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

        //260601 hbk Phase 40 CO-40-02 UAT — 행 클릭 시 해당 측정의 이미지/overlay 만 표시하기 위한 소유 객체 역참조.
        //  직렬화 결과 DTO 를 가리키는 in-memory 참조(직렬화 대상 아님) — 리뷰어 decluttering 용.
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
            ShotName = shot != null ? (shot.ShotName ?? "") : "";
            FAIName = fai != null ? (fai.FAIName ?? "") : "";
            MeasurementName = m.MeasurementName ?? "";
            NominalValue = m.NominalValue;
            TolerancePlus = m.TolerancePlus;
            ToleranceMinus = m.ToleranceMinus;

            // CO-23-01: 0.0 도 정상 결과 — HasResult 플래그로 판별 (MeasuredValue != 0 센티넬 금지)
            ResultDisplay = m.LastHasResult ? m.LastMeasuredValue.ToString("F4") : "—";

            // 3분기: DATUM_FAIL > HasResult 유무 > OK/NG
            if (m.LastSkipReason == "DATUM_FAIL")
            {
                JudgeText = "DETECT FAIL";
            }
            else if (m.LastHasResult)
            {
                JudgeText = m.LastJudgement ? "OK" : "NG";
            }
            else
            {
                JudgeText = "—";
            }
        }
    }
}
