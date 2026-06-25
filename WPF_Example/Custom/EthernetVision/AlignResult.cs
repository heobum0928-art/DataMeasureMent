//260624 hbk Phase 59
namespace ReringProject {

    /// <summary>
    /// Shape matching align 결과 모델. AlignShapeMatchService.Run() 반환값.
    /// Phase 62 TCP 전송 소비(OffsetXmm/OffsetYmm/ThetaDeg). 순수 POCO — 로직/HALCON/IO 없음.
    /// </summary>
    public class AlignResult {

        /// <summary>매칭 성공 여부. false 면 나머지 필드 미사용.</summary>
        public bool Found { get; set; }

        /// <summary>매칭 점수 (0~1).</summary>
        public double Score { get; set; }

        /// <summary>X Offset(mm) = dCol × (EthernetPixelResolution/1000). Col↔X 규약 — UAT 에서 부호 확정.</summary>
        public double OffsetXmm { get; set; }

        /// <summary>Y Offset(mm) = dRow × (EthernetPixelResolution/1000). Row↔Y 규약.</summary>
        public double OffsetYmm { get; set; }

        /// <summary>Theta(deg) = curAngleDeg − refAngleDeg. Tray 모드에서는 0 / HasTheta=false.</summary>
        public double ThetaDeg { get; set; }

        /// <summary>true = Bottom 모드 (ThetaDeg 유효). false = Tray 모드 (ThetaDeg=0 무시).</summary>
        public bool HasTheta { get; set; }

        //260625 hbk Phase 61.1 — 시각화 필드 (ADDITIVE). 기존 6필드 무수정.

        /// <summary>검출 좌표 유효 여부 (시각화 게이트). Found=true + 검출 좌표 산출 완료 시 true.</summary>
        public bool HasDetection { get; set; }

        /// <summary>TL 패턴 검출 중심 Row (px, 이미지 좌표계).</summary>
        public double DetectedRow1 { get; set; }

        /// <summary>TL 패턴 검출 중심 Col (px, 이미지 좌표계).</summary>
        public double DetectedCol1 { get; set; }

        /// <summary>BR 패턴 검출 중심 Row (px, 이미지 좌표계).</summary>
        public double DetectedRow2 { get; set; }

        /// <summary>BR 패턴 검출 중심 Col (px, 이미지 좌표계).</summary>
        public double DetectedCol2 { get; set; }

        /// <summary>두 패턴 midpoint Row (px). 오프셋 산출 기준점.</summary>
        public double DetectedCenterRow { get; set; }

        /// <summary>두 패턴 midpoint Col (px). 오프셋 산출 기준점.</summary>
        public double DetectedCenterCol { get; set; }

        /// <summary>
        /// 검출 위치 기반 보정 ROI 박스 목록. 각 항목 = double[5] {row, col, phi, len1, len2}.
        /// MainResultViewerControl.RenderResultRoiBoxes 규약(길이 5, DispRectangle2 호환).
        /// </summary>
        public System.Collections.Generic.List<double[]> DetectedRoiBoxes { get; set; }
            = new System.Collections.Generic.List<double[]>();

        /// <summary>검출 모델 contour 점 Row 목록 (px). EdgeContourCols 와 인덱스 1:1 대응.</summary>
        public System.Collections.Generic.List<double> EdgeContourRows { get; set; }
            = new System.Collections.Generic.List<double>();

        /// <summary>검출 모델 contour 점 Col 목록 (px). EdgeContourRows 와 인덱스 1:1 대응.</summary>
        public System.Collections.Generic.List<double> EdgeContourCols { get; set; }
            = new System.Collections.Generic.List<double>();
    }
}
