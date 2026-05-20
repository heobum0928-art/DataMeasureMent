//260519 hbk Phase 31 D-08 — G 시리즈: 호 에지 라인 중점 → Datum C X 거리
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
    /// D-08: ArcEdgeDistance(G 시리즈) = EdgeToLineDistance 와 알고리즘 동일, MeasureAxis 기본값 "X" 만 다름.
    /// </summary>
    public class ArcEdgeDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-08
    {
        public override string TypeName { get { return "ArcEdgeDistance"; } } //260519 hbk Phase 31 D-08

        [Category("Point|ROI")] //260519 hbk Phase 31 D-08
        public double Point_Row { get; set; } //260519 hbk Phase 31 D-08
        public double Point_Col { get; set; } //260519 hbk Phase 31 D-08
        public double Point_Phi { get; set; } //260519 hbk Phase 31 D-08
        public double Point_Length1 { get; set; } //260519 hbk Phase 31 D-08
        public double Point_Length2 { get; set; } //260519 hbk Phase 31 D-08

        [Category("Edge")] //260519 hbk Phase 31 D-08
        public int EdgeThreshold { get; set; } = 10; //260519 hbk Phase 31 D-08
        public double Sigma { get; set; } = 1.0; //260519 hbk Phase 31 D-08
        public int EdgeSampleCount { get; set; } = 20; //260519 hbk Phase 31 D-08
        public int EdgeTrimCount { get; set; } = 10; //260519 hbk Phase 31 D-08
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260519 hbk Phase 31 D-08
        public string EdgePolarity { get; set; } = "DarkToLight"; //260519 hbk Phase 31 D-08
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260519 hbk Phase 31 D-08
        public string EdgeDirection { get; set; } = "TtoB"; //260519 hbk Phase 31 D-08

        //260519 hbk Phase 31 D-08 — PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-08
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } } //260519 hbk Phase 31 D-08
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-08
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } } //260519 hbk Phase 31 D-08

        //260519 hbk Phase 31 D-08 — 측정 거리 축 선택: G 시리즈 = Datum C X 방향이므로 기본값 "X"
        [Category("Edge")] //260519 hbk Phase 31 D-08
        [ItemsSourceProperty(nameof(MeasureAxisList))] //260519 hbk Phase 31 D-08
        public string MeasureAxis { get; set; } = "X"; //260519 hbk Phase 31 D-08 (G 시리즈 = Datum C X 방향)
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-08
        public List<string> MeasureAxisList { get { return new List<string> { "Y", "X" }; } } //260519 hbk Phase 31 D-08

        //260519 hbk Phase 31 D-08 — datum 교점 좌표 runtime 주입 전용 (Action_FAIMeasurement IDatumOriginConsumer 경로).
        //  transient: PropertyGrid 미표시, JSON 직렬화 제외. EdgeToLineDistanceMeasurement.cs L69~80 동일 패턴.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-08
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-08
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-08
        public double DatumOriginRow { get; set; } //260519 hbk Phase 31 D-08 — datum 교점 row (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-08
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-08
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-08
        public double DatumOriginCol { get; set; } //260519 hbk Phase 31 D-08 — datum 교점 col (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-08
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-08
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-08
        public double DatumAngleRad { get; set; } //260519 hbk Phase 31 D-08 — datum 1차(수평) 기준선 각도(rad). 미주입 시 0.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 hotfix#3
        public double DatumAngle2Rad { get; set; } //260519 hbk Phase 31 hotfix#3 — datum 2차(수직) 기준선 각도(rad). X축 측정 기준.

        public ArcEdgeDistanceMeasurement(object owner) : base(owner) { } //260519 hbk Phase 31 D-08

        public override bool TryExecute( //260519 hbk Phase 31 D-08
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>(); //260519 hbk Phase 31 D-08

            var svc = new VisionAlgorithmService();
            double pr1, pc1, pr2, pc2;

            //260519 hbk Phase 31 D-08 — TryFitLine "All" 고정 (memory feedback — EdgeSelection 명시 필수, CO-23-01 구조적 차단)
            if (!svc.TryFitLine(image,
                Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out pr1, out pc1, out pr2, out pc2, out error,
                "All")) //260519 hbk Phase 31 D-08 — EdgeSelection "All" 고정
            {
                return false;
            }

            //260519 hbk Phase 31 D-08 — 에지 라인 중점 (EdgeToLineDistanceMeasurement L124 동일 패턴)
            double pRow = (pr1 + pr2) / 2.0; //260519 hbk Phase 31 D-08
            double pCol = (pc1 + pc2) / 2.0; //260519 hbk Phase 31 D-08

            //260519 hbk Phase 31 D-08 — D-04 공용 헬퍼 호출: 에지 라인 중점 → datum 기준선 투영 거리
            //260519 hbk Phase 31 hotfix#3 — X축 측정은 2차(수직) 기준선, Y축은 1차(수평) 기준선
            //260520 hbk Phase 31 simplify — foot 반환 오버로드로 전환. HomMat2dInvert+AffineTransPoint2d 제거
            //   (IDatumOriginConsumer 가 이미 datum 원점 image 좌표 주입 → 행렬 역변환 불필요).
            //   FAI-DistLine 시작점도 datum 원점이 아닌 실제 정사영점(foot, datum 라인 위 수선의 발)으로 정확화.
            double measureLineAngle = (MeasureAxis == "X") ? DatumAngle2Rad : DatumAngleRad; //260519 hbk Phase 31 hotfix#3
            double footRow, footCol; //260520 hbk Phase 31 simplify
            bool footOk; //260520 hbk Phase 31 simplify
            resultValue = VisionAlgorithmService.ComputeProjectionDistance(
                pRow, pCol,
                DatumOriginRow, DatumOriginCol, measureLineAngle,
                pixelResolution, MeasureAxis,
                out footRow, out footCol, out footOk); //260520 hbk Phase 31 simplify

            //260519 hbk Phase 31 D-08 — overlay: EdgeToLineDistanceMeasurement 와 동일 패턴 (FAI-Edge1 + FAI-DistLine)
            // 1) 검출 에지 라인 overlay
            overlays.Add(new EdgeInspectionOverlay //260519 hbk Phase 31 D-08
            {
                RoiId = "FAI-Edge1", //260519 hbk Phase 31 D-08 — StartsWith("FAI-Edge") → HalconDisplayService 녹/적 + Action_FAIMeasurement suffix
                LineRow1 = pr1, //260519 hbk Phase 31 D-08
                LineColumn1 = pc1, //260519 hbk Phase 31 D-08
                LineRow2 = pr2, //260519 hbk Phase 31 D-08
                LineColumn2 = pc2, //260519 hbk Phase 31 D-08
                Points = new List<EdgeInspectionPoint> //260519 hbk Phase 31 D-08
                {
                    new EdgeInspectionPoint { Row = pRow, Column = pCol } //260519 hbk Phase 31 D-08 — 에지 중점
                }
            }); //260519 hbk Phase 31 D-08

            // 2) 거리선 overlay: 측정점 → 정사영점(foot, datum 라인 위 수선의 발). 측정값과 시각적 일치.
            //260520 hbk Phase 31 simplify — footOk 가 false 면 ProjectionPl 실패 → distline skip (측정값과 에지 라인 overlay 는 유지)
            if (footOk) //260520 hbk Phase 31 simplify
            {
                overlays.Add(new EdgeInspectionOverlay //260520 hbk Phase 31 simplify
                {
                    RoiId = "FAI-DistLine", //260520 hbk Phase 31 simplify — cyan(청록), HalconDisplayService.cs:181 분기
                    LineRow1 = footRow, LineColumn1 = footCol, //260520 hbk Phase 31 simplify — 정사영점
                    LineRow2 = pRow, LineColumn2 = pCol, //260520 hbk Phase 31 simplify — 에지 중점
                    Points = new List<EdgeInspectionPoint> //260520 hbk Phase 31 simplify
                    {
                        new EdgeInspectionPoint { Row = footRow, Column = footCol }, //260520 hbk Phase 31 simplify
                        new EdgeInspectionPoint { Row = pRow, Column = pCol } //260520 hbk Phase 31 simplify
                    }
                });
            }

            return true;
        }
    }
}
