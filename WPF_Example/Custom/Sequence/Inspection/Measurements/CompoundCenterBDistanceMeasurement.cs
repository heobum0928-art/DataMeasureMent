using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Rect ROI 1개 → 공통 컨투어 알고리즘(canny→union_adjacent→LargestRect) → LargestRect 중심 추출.
    /// CompoundCenterBDistance — LargestRect 중심 → Datum B Y 방향 거리(mm).
    /// CompoundCenterCDistanceMeasurement 와 구조 완전 동일.
    /// 차이: TypeName="CompoundCenterBDistance", MeasureAxis 기본값 "Y" (Datum B Y 방향).
    /// </summary>
    public class CompoundCenterBDistanceMeasurement : MeasurementBase, IDatumOriginConsumer
    {
        public override string TypeName { get { return "CompoundCenterBDistance"; } }

        // ── Rect ROI ─────────────────────────────────────────────────────────────────
        // 공통 컨투어 알고리즘 입력 Rect ROI
        [Category("Rect|ROI")]
        public double Rect_Row { get; set; }
        public double Rect_Col { get; set; }
        public double Rect_Phi { get; set; }
        public double Rect_Length1 { get; set; }
        public double Rect_Length2 { get; set; }

        // ── Edge 파라미터 ─────────────────────────────────────────────────────────────
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

        // ── Contour 파라미터 (PropertyGrid 사용자 편집) ───────────────────────────────
        [Category("Contour")]
        public double CannyAlpha { get; set; } = 1.0; // edges_sub_pix canny alpha
        public int CannyLow { get; set; } = 20; // canny low threshold
        public int CannyHigh { get; set; } = 40; // canny high threshold
        public double UnionDistance { get; set; } = 700.0; // union_adjacent_contours_xld 거리

        // ── MeasureAxis ───────────────────────────────────────────────────────────────
        // Datum B Y 방향이므로 기본값 "Y"
        [Category("Edge")]
        [ItemsSourceProperty(nameof(MeasureAxisList))]
        public string MeasureAxis { get; set; } = "Y";
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> MeasureAxisList { get { return new List<string> { "Y", "X" }; } }

        // ── IDatumOriginConsumer transient 필드 ──────────────────────────────────────
        // datum 교점 좌표 runtime 주입 전용. ArcEdgeDistanceMeasurement 동일 패턴.
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
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumAngle2Rad { get; set; } // datum 2차(수직) 기준선 각도. X축 측정 기준.
        // IDatumOriginConsumer 확장. 본 타입 미사용 — 주입만 받음.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleRow { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleCol { get; set; }

        public CompoundCenterBDistanceMeasurement(object owner) : base(owner) { }

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

            // (2) LargestRect 중심 → Datum 거리. X측정=2차(수직)선, Y측정=1차(수평)선
            double measureLineAngle;
            if (MeasureAxis == "X")
                measureLineAngle = DatumAngle2Rad;
            else
                measureLineAngle = DatumAngleRad;
            double footRow, footCol; // FAI-DistLine 수선의 발 좌표
            bool footOk;
            resultValue = VisionAlgorithmService.ComputeProjectionDistance(
                centerRow, centerCol,
                DatumOriginRow, DatumOriginCol, measureLineAngle,
                pixelResolution, MeasureAxis,
                out footRow, out footCol, out footOk); // foot 반환 오버로드 (수치 결과 동일)

            // overlay — 이미 계산한 변수만 재사용. HALCON 재호출 없음.
            // 1) FAI-Edge1 = LargestRect 중심 점 마커
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge1",
                LineRow1 = centerRow, LineColumn1 = centerCol,
                LineRow2 = centerRow, LineColumn2 = centerCol, // 점 마커
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = centerRow, Column = centerCol }
                }
            });
            // 2) FAI-DistLine = LargestRect 중심 → datum 기준선 수선의 발 (수직 드롭선, cyan)
            if (footOk) // projection 실패 시 수치 0 이지만 라인은 skip
            {
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "FAI-DistLine",
                    LineRow1 = footRow, LineColumn1 = footCol, // 수선의 발 (datum 기준선 위)
                    LineRow2 = centerRow, LineColumn2 = centerCol, // LargestRect 중심
                    Points = new List<EdgeInspectionPoint> // 양 끝점 X마커
                    {
                        new EdgeInspectionPoint { Row = footRow, Column = footCol },
                        new EdgeInspectionPoint { Row = centerRow, Column = centerCol }
                    }
                });
            }

            return true;
        }
    }
}
