using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// E2 (CompoundAngle): Rect ROI 1개 → 공통 컨투어(canny → union_adjacent → LargestRect) → LargestRect 중심.
    /// 대각선(LargestRect 중심 ↔ DatumC 검출 원중심)과 DatumB 기준선 사이의 각도(degree)를 측정한다.
    /// DatumC 검출 원중심은 IDatumOriginConsumer 로 runtime 주입되며, 미주입(0,0)이면 error 로 안전 종결한다.
    /// </summary>
    public class CompoundAngleMeasurement : MeasurementBase, IDatumOriginConsumer
    {
        public override string TypeName { get { return "CompoundAngle"; } }

        // 공통 컨투어 알고리즘 입력 Rect ROI
        [Category("Rect|ROI")]
        public double Rect_Row { get; set; }
        public double Rect_Col { get; set; }
        public double Rect_Phi { get; set; }
        public double Rect_Length1 { get; set; }
        public double Rect_Length2 { get; set; }

        [Category("Edge")]
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        public int EdgeSampleCount { get; set; } = 20;
        public int EdgeTrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string EdgePolarity { get; set; } = "DarkToLight";
        [ItemsSourceProperty(nameof(EdgeDirectionList))]
        public string EdgeDirection { get; set; } = "TtoB";

        // PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }

        // 공통 컨투어 알고리즘 파라미터 (PropertyGrid 편집)
        [Category("Contour")]
        public double CannyAlpha { get; set; } = 1.0;
        public int CannyLow { get; set; } = 20;
        public int CannyHigh { get; set; } = 40;
        public double UnionDistance { get; set; } = 700.0; // union_adjacent_contours_xld 거리

        // datum 좌표 runtime 주입 전용 transient 필드 (직렬화/PropertyGrid 제외)
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumOriginRow { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumOriginCol { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumAngleRad { get; set; } // datum 1차(수평) 기준선 각도
        // IDatumOriginConsumer 2차 각도 — CompoundAngle 은 1차선 기준만 사용하나 인터페이스상 속성만 구현
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumAngle2Rad { get; set; }
        // DatumC 검출 원(B1 홀) 중심 — E2 가 대각선 끝점으로 실제 사용
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleRow { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleCol { get; set; }

        public CompoundAngleMeasurement(object owner) : base(owner) { }

        public override bool TryExecute(
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>();

            var svc = new VisionAlgorithmService();

            // (1) 공통 컨투어 알고리즘 → LargestRect 중심
            double centerRow, centerCol, phi, len1, len2;
            if (!svc.TryFindLargestContourRect(image,
                Rect_Row, Rect_Col, Rect_Phi, Rect_Length1, Rect_Length2,
                datumTransform,
                CannyAlpha, CannyLow, CannyHigh, UnionDistance,
                out centerRow, out centerCol, out phi, out len1, out len2, out error))
            {
                return false;
            }

            // (2) DatumC 검출 원중심 주입 검증 (CircleTwoHorizontal datum 전제)
            if (DatumDetectedCircleRow == 0.0 && DatumDetectedCircleCol == 0.0)
            {
                error = "DatumC detected circle center not injected (requires CircleTwoHorizontal datum)";
                return false;
            }

            // (3) DatumB 기준선 2점 (DatumOrigin 중심 ±200px, 방향 = DatumAngleRad)
            double sinT = System.Math.Sin(DatumAngleRad);
            double cosT = System.Math.Cos(DatumAngleRad);
            double daR1 = DatumOriginRow - 200.0 * sinT;
            double daC1 = DatumOriginCol - 200.0 * cosT;
            double daR2 = DatumOriginRow + 200.0 * sinT;
            double daC2 = DatumOriginCol + 200.0 * cosT;

            // (4) 대각선(LargestRect 중심 ↔ DatumC 검출 원중심) vs DatumB 기준선 각도
            resultValue = VisionAlgorithmService.AngleLineLine(
                centerRow, centerCol, DatumDetectedCircleRow, DatumDetectedCircleCol,
                daR1, daC1, daR2, daC2);

            // overlay 1: LargestRect 중심 점 마커 (Line 시작==끝). RoiId 는 다운스트림 녹/적 렌더 매칭 키.
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge1",
                LineRow1 = centerRow, LineColumn1 = centerCol,
                LineRow2 = centerRow, LineColumn2 = centerCol,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = centerRow, Column = centerCol }
                }
            });
            // overlay 2: 대각선 (LargestRect 중심 ↔ DatumC 검출 원중심), 양 끝점 마커
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-DiagLine",
                LineRow1 = centerRow, LineColumn1 = centerCol,
                LineRow2 = DatumDetectedCircleRow, LineColumn2 = DatumDetectedCircleCol,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = centerRow, Column = centerCol },
                    new EdgeInspectionPoint { Row = DatumDetectedCircleRow, Column = DatumDetectedCircleCol }
                }
            });

            return true;
        }
    }
}
