using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Point ROI 에서 에지 라인을 피팅하고 라인 중점을 추출한 뒤,
    /// Datum C 기준선까지의 거리(mm)를 VisionAlgorithmService.ComputeProjectionDistance 로 산출한다.
    /// MeasureAxis="X": Datum C 수직선(y축)까지 거리 — G 시리즈 기본 방향(+X 오른쪽 양수).
    /// MeasureAxis="Y": Datum 수평선(x축)까지 수직거리.
    /// ArcEdgeDistance(G 시리즈) = EdgeToLineDistance 와 알고리즘 동일, MeasureAxis 기본값 "X" 만 다름.
    /// </summary>
    public class ArcEdgeDistanceMeasurement : MeasurementBase, IDatumOriginConsumer
    {
        public override string TypeName { get { return "ArcEdgeDistance"; } }

        [Category("Point|ROI")]
        public double Point_Row { get; set; }
        public double Point_Col { get; set; }
        public double Point_Phi { get; set; }
        public double Point_Length1 { get; set; }
        public double Point_Length2 { get; set; }

        [Category("Edge")]
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        public int EdgeSampleCount { get; set; } = 20;
        public int EdgeTrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string EdgePolarity { get; set; } = "DarkToLight";
        [ItemsSourceProperty(nameof(EdgeDirectionList))]
        public string EdgeDirection { get; set; } = "TtoB";
        // EdgeSelection 사용자 노출 (strip-loop 가 First/Last 도 stripCount 점 누적). INI 미존재 시 폴백 "All".
        [ItemsSourceProperty(nameof(EdgeSelectionList))]
        public string EdgeSelection { get; set; } = "All";

        // PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } }

        // 측정 거리 축 선택: G 시리즈 = Datum C X 방향이므로 기본값 "X"
        [Category("Edge")]
        [ItemsSourceProperty(nameof(MeasureAxisList))]
        public string MeasureAxis { get; set; } = "X";
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> MeasureAxisList { get { return new List<string> { "Y", "X" }; } }

        // datum 교점 좌표 runtime 주입 전용 (Action_FAIMeasurement IDatumOriginConsumer 경로).
        //  transient: PropertyGrid 미표시, JSON 직렬화 제외. EdgeToLineDistanceMeasurement.cs L69~80 동일 패턴.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumOriginRow { get; set; } // datum 교점 row (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumOriginCol { get; set; } // datum 교점 col (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumAngleRad { get; set; } // datum 1차(수평) 기준선 각도(rad). 미주입 시 0.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumAngle2Rad { get; set; } // datum 2차(수직) 기준선 각도(rad). X축 측정 기준.
        // IDatumOriginConsumer 확장. 본 타입은 검출 원중심 미사용 (E2 만 사용) — 주입만 받고 미참조.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleRow { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleCol { get; set; }

        public ArcEdgeDistanceMeasurement(object owner) : base(owner) { }

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
            double pr1, pc1, pr2, pc2;

            // EdgeSelection 사용자 선택값 사용 (was 리터럴 "All"). strip-loop 가 First/Last 도 stripCount 점 누적하므로 안전.
            if (!svc.TryFitLine(image,
                Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out pr1, out pc1, out pr2, out pc2, out error,
                EdgeSelection))
            {
                return false;
            }

            // 에지 라인 중점 (EdgeToLineDistanceMeasurement L124 동일 패턴)
            double pRow = (pr1 + pr2) / 2.0;
            double pCol = (pc1 + pc2) / 2.0;

            // D-04 공용 헬퍼 호출: 에지 라인 중점 → datum 기준선 투영 거리.
            // X축 측정은 2차(수직) 기준선, Y축은 1차(수평) 기준선.
            // foot 반환 오버로드로 전환. HomMat2dInvert+AffineTransPoint2d 제거
            //   (IDatumOriginConsumer 가 이미 datum 원점 image 좌표 주입 → 행렬 역변환 불필요).
            //   FAI-DistLine 시작점도 datum 원점이 아닌 실제 정사영점(foot, datum 라인 위 수선의 발)으로 정확화.
            double measureLineAngle;
            if (MeasureAxis == "X")
                measureLineAngle = DatumAngle2Rad;
            else
                measureLineAngle = DatumAngleRad;
            double footRow, footCol;
            bool footOk;
            resultValue = VisionAlgorithmService.ComputeProjectionDistance(
                pRow, pCol,
                DatumOriginRow, DatumOriginCol, measureLineAngle,
                pixelResolution, MeasureAxis,
                out footRow, out footCol, out footOk);

            // overlay: EdgeToLineDistanceMeasurement 와 동일 패턴 (FAI-Edge1 + FAI-DistLine)
            // 1) 검출 에지 라인 overlay
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge1", // StartsWith("FAI-Edge") → HalconDisplayService 녹/적 + Action_FAIMeasurement suffix
                LineRow1 = pr1,
                LineColumn1 = pc1,
                LineRow2 = pr2,
                LineColumn2 = pc2,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = pRow, Column = pCol } // 에지 중점
                }
            });

            // 2) datum 기준선 overlay: datum 주입 시 가로(1차 수평)/세로(2차 수직) 기준선 표시.
            bool datumInjected = (DatumOriginRow != 0.0 || DatumOriginCol != 0.0);
            if (datumInjected)
            {
                // halfLength = 이미지 대각선 → 기준선 이미지 전체 span (DispLine 이 window 로 클립)
                double axisHalfLen = 4000.0; // HImage 크기 조회 실패 시 폴백
                try
                {
                    HTuple imgW, imgH;
                    image.GetImageSize(out imgW, out imgH);
                    axisHalfLen = System.Math.Sqrt(imgW.D * imgW.D + imgH.D * imgH.D);
                }
                catch { } // 조회 실패 시 폴백 길이 유지

                // 가로 datum 기준선(1차 수평선, DatumAngleRad) + datum 교점 마커
                double hR1, hC1, hR2, hC2;
                VisionAlgorithmService.GetDatumAxisLine(
                    DatumOriginRow, DatumOriginCol, DatumAngleRad, axisHalfLen,
                    out hR1, out hC1, out hR2, out hC2);
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "FAI-DatumLine", // 미지정 분기 파랑 (datum 기준선)
                    LineRow1 = hR1, LineColumn1 = hC1,
                    LineRow2 = hR2, LineColumn2 = hC2,
                    Points = new List<EdgeInspectionPoint> // datum 교점(원점) 마커
                    {
                        new EdgeInspectionPoint { Row = DatumOriginRow, Column = DatumOriginCol }
                    }
                });

                // 세로 datum 기준선(2차 수직선, DatumAngle2Rad — X축 측정 기준)
                double vR1, vC1, vR2, vC2;
                VisionAlgorithmService.GetDatumAxisLine(
                    DatumOriginRow, DatumOriginCol, DatumAngle2Rad, axisHalfLen,
                    out vR1, out vC1, out vR2, out vC2);
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "FAI-DatumLine",
                    LineRow1 = vR1, LineColumn1 = vC1,
                    LineRow2 = vR2, LineColumn2 = vC2
                });
            }

            // 3) 거리선 overlay: 측정점 → 정사영점(foot, datum 라인 위 수선의 발). 측정값과 시각적 일치.
            // footOk 가 false 면 ProjectionPl 실패 → distline skip (측정값과 에지 라인 overlay 는 유지)
            if (footOk)
            {
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "FAI-DistLine", // cyan(청록), HalconDisplayService.cs:181 분기
                    LineRow1 = footRow, LineColumn1 = footCol, // 정사영점
                    LineRow2 = pRow, LineColumn2 = pCol, // 에지 중점
                    Points = new List<EdgeInspectionPoint>
                    {
                        new EdgeInspectionPoint { Row = footRow, Column = footCol },
                        new EdgeInspectionPoint { Row = pRow, Column = pCol }
                    }
                });
            }

            return true;
        }
    }
}
