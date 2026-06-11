using System.Collections.Generic;

namespace ReringProject.Halcon.Models
{
    /// <summary>
    /// 단일 FAI 에지 측정 결과를 담는 모델.
    /// FAIEdgeMeasurementService.TryMeasure() 에서 생성된다.
    /// </summary>
    public class FAIEdgeMeasurementResult
    {
        /// <summary>에지 1 위치 — 피팅 라인 1 중점 (이미지 픽셀 좌표)</summary>
        public double Edge1Row { get; set; }
        public double Edge1Column { get; set; }

        /// <summary>에지 2 위치 — 피팅 라인 2 중점 (이미지 픽셀 좌표, Both 모드 전용)</summary>
        public double Edge2Row { get; set; }
        public double Edge2Column { get; set; }

        // 피팅 라인 1 양 끝점
        public double Line1Row1 { get; set; }
        public double Line1Column1 { get; set; }
        public double Line1Row2 { get; set; }
        public double Line1Column2 { get; set; }

        // 피팅 라인 2 양 끝점 (Both 모드에서만 사용)
        public double Line2Row1 { get; set; }
        public double Line2Column1 { get; set; }
        public double Line2Row2 { get; set; }
        public double Line2Column2 { get; set; }

        // 트림 전 총 에지 포인트 수
        public int EdgePointCount { get; set; }

        /// <summary>에지 간 픽셀 거리</summary>
        public double DistancePixel { get; set; }

        /// <summary>에지 간 mm 거리 (PixelResolution 적용)</summary>
        public double DistanceMm { get; set; }

        /// <summary>캔버스 오버레이 (에지 마커 + 연결선)</summary>
        public List<EdgeInspectionOverlay> Overlays { get; set; } = new List<EdgeInspectionOverlay>();
    }
}
