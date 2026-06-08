//260601 hbk Phase 40 OUT-01/OUT-02 — cycle 결과 JSON 직렬화 DTO (D-01/D-02/D-03)
using System;
using System.Collections.Generic;
using ReringProject.Halcon.Models;

namespace ReringProject.UI
{
    //260601 hbk Phase 40 OUT-01/OUT-02
    /// <summary>
    /// 1회 검사 cycle 전체 결과를 담는 최상위 JSON 직렬화 DTO.
    /// OUT-01(결과 리뷰어)과 OUT-02(xlsx export)의 공통 단일 소스 (D-01).
    /// </summary>
    public class CycleResultDto
    {
        // D-02 메타: 검사 일시 + 레시피명 + 종합판정
        public DateTime InspectionTime { get; set; }

        public string RecipeName { get; set; }

        /// <summary>종합 판정. "OK" / "NG" / "DETECT_FAIL" — Phase 39 WF-02 3-state hierarchy 일치 (D-02)</summary>
        public string OverallJudgement { get; set; }

        // D-03: cycle 폴더 절대 경로 (SaveAsync 에서 설정됨)
        public string CycleFolderPath { get; set; }

        // D-01/D-05 측정 데이터 — Shot > FAI > Measurement 계층
        public List<ShotResultDto> Shots { get; set; } = new List<ShotResultDto>();
    }

    //260601 hbk Phase 40 OUT-01/OUT-02
    /// <summary>Shot 단위 결과 DTO.</summary>
    public class ShotResultDto
    {
        public string ShotName { get; set; }

        public string OwnerSequenceName { get; set; }

        /// <summary>
        /// 측정에 사용된 소스 이미지 절대 경로 (ShotConfig.GetLatestImagePath() = SimulImagePath).
        /// 리뷰어 재로드 및 xlsx 하이퍼링크(D-07)에 사용된다.
        /// </summary>
        public string ResultImagePath { get; set; }

        public List<FaiResultDto> FAIs { get; set; } = new List<FaiResultDto>();
    }

    //260601 hbk Phase 40 OUT-01/OUT-02
    /// <summary>FAI 단위 결과 DTO. LastOverlays 는 [JsonIgnore] 없이 직렬화 — DTO 계층이므로 INI 직렬화 무관.</summary>
    public class FaiResultDto
    {
        public string FAIName { get; set; }

        public bool IsPass { get; set; }

        /// <summary>true 이면 datum 검출 실패로 측정 미실행 (Phase 39 WF-01, "DETECT_FAIL" 분기).</summary>
        public bool WasDatumSkipped { get; set; }

        public List<MeasurementResultDto> Measurements { get; set; } = new List<MeasurementResultDto>();

        /// <summary>overlay 기하 — EdgeInspectionOverlay 전 필드가 CLR 타입이므로 직렬화 안전 (RESEARCH Pitfall 3 해소).</summary>
        public List<EdgeInspectionOverlay> LastOverlays { get; set; } = new List<EdgeInspectionOverlay>();
    }

    //260601 hbk Phase 40 OUT-01/OUT-02
    /// <summary>Measurement 단위 결과 DTO. MeasurementBase 의 runtime 결과 필드 전체를 복사한다.</summary>
    public class MeasurementResultDto
    {
        public string MeasurementName { get; set; }

        public string TypeName { get; set; }

        public double NominalValue { get; set; }

        public double TolerancePlus { get; set; }

        public double ToleranceMinus { get; set; }

        public double LastMeasuredValue { get; set; }

        /// <summary>true = OK. false = NG 또는 미측정.</summary>
        public bool LastJudgement { get; set; }

        /// <summary>CO-23-01: 0.0 도 정상 결과로 구분하기 위한 플래그. false 이면 측정 미실행.</summary>
        public bool LastHasResult { get; set; }

        /// <summary>null = 정상 측정, "DATUM_FAIL" = datum 검출 실패로 skip (Phase 39 WF-01 D-02).</summary>
        public string LastSkipReason { get; set; }

        //260601 hbk Phase 40 CO-40-03 UAT — DualImage 측정(가로축·세로축 2장) 리뷰어 표시용 경로.
        //  DualImageEdgeDistanceMeasurement 만 IsDualImage=true + 2장 경로 채움. 그 외 측정은 false (Shot 이미지 사용).
        /// <summary>true = DualImage 측정(가로축/세로축 2장). 리뷰어가 전환 버튼을 노출하는 신호.</summary>
        public bool IsDualImage { get; set; }

        /// <summary>가로축 티칭 이미지 경로 (DualImage). 미설정 시 Shot 이미지로 fallback (Phase 39.4 D-G1).</summary>
        public string HorizontalImagePath { get; set; }

        /// <summary>세로축 티칭 이미지 경로 (DualImage).</summary>
        public string VerticalImagePath { get; set; }
    }
}
