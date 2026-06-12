using System;
using System.Collections.Generic;
using ReringProject.Halcon.Models;

namespace ReringProject.UI
{
    /// <summary>
    /// 1회 검사 cycle 전체 결과를 담는 최상위 JSON 직렬화 DTO.
    /// 결과 리뷰어와 xlsx export 의 공통 단일 소스.
    /// </summary>
    public class CycleResultDto
    {
        // 검사 일시
        public DateTime InspectionTime { get; set; }

        public string RecipeName { get; set; }

        /// <summary>종합 판정. "OK" / "NG" / "DETECT_FAIL" (3-state hierarchy).</summary>
        public string OverallJudgement { get; set; }

        // cycle 폴더 절대 경로 (SaveAsync 에서 설정됨)
        public string CycleFolderPath { get; set; }

        // 측정 데이터 — Shot > FAI > Measurement 계층
        public List<ShotResultDto> Shots { get; set; } = new List<ShotResultDto>();
    }

    /// <summary>Shot 단위 결과 DTO.</summary>
    public class ShotResultDto
    {
        public string ShotName { get; set; }

        public string OwnerSequenceName { get; set; }

        /// <summary>
        /// 측정에 사용된 소스 이미지 절대 경로 (ShotConfig.GetLatestImagePath() = SimulImagePath).
        /// 리뷰어 재로드 및 xlsx 하이퍼링크에 사용된다.
        /// </summary>
        public string ResultImagePath { get; set; }

        public List<FaiResultDto> FAIs { get; set; } = new List<FaiResultDto>();
    }

    /// <summary>FAI 단위 결과 DTO.</summary>
    public class FaiResultDto
    {
        public string FAIName { get; set; }

        public bool IsPass { get; set; }

        /// <summary>true 이면 datum 검출 실패로 측정 미실행 ("DETECT_FAIL" 분기).</summary>
        public bool WasDatumSkipped { get; set; }

        /// <summary>원본 이미지 파일명. 예: origin_Top_FAI_A1_P1P2_153012345.png (경로 미포함).</summary>
        public string OriginImageFileName { get; set; }
        /// <summary>측정 오버레이 캡쳐 이미지 파일명. 예: capture_Top_FAI_A1_P1P2_153012345.png (경로 미포함).</summary>
        public string CaptureImageFileName { get; set; }

        public List<MeasurementResultDto> Measurements { get; set; } = new List<MeasurementResultDto>();

        /// <summary>overlay 기하.</summary>
        public List<EdgeInspectionOverlay> LastOverlays { get; set; } = new List<EdgeInspectionOverlay>();
    }

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

        /// <summary>0.0 도 정상 결과로 구분하기 위한 플래그. false 이면 측정 미실행.</summary>
        public bool LastHasResult { get; set; }

        /// <summary>null = 정상 측정, "DATUM_FAIL" = datum 검출 실패로 skip.</summary>
        public string LastSkipReason { get; set; }

        /// <summary>true = DualImage 측정(가로축/세로축 2장). 리뷰어가 전환 버튼을 노출하는 신호.</summary>
        public bool IsDualImage { get; set; }

        /// <summary>가로축 티칭 이미지 경로 (DualImage). 미설정 시 Shot 이미지로 fallback.</summary>
        public string HorizontalImagePath { get; set; }

        /// <summary>세로축 티칭 이미지 경로 (DualImage).</summary>
        public string VerticalImagePath { get; set; }
    }
}
