//260519 hbk Phase 31 D-01 — E8: 원중심 → Datum B Y 거리
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// 원형 ROI 에서 TryFindCircle 로 원중심을 검출하고,
    /// Datum 기준선까지의 거리(mm)를 VisionAlgorithmService.ComputeProjectionDistance 로 산출한다.
    /// MeasureAxis="Y": Datum B 수평선(x축)까지 수직거리 — E8 기본 방향.
    /// MeasureAxis="X": Datum 수직선(y축)까지 거리.
    /// </summary>
    public class CircleCenterDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-01
    {
        public override string TypeName { get { return "CircleCenterDistance"; } } //260519 hbk Phase 31 D-01

        [Category("Circle|ROI")] //260519 hbk Phase 31 D-01
        public double Circle_Row { get; set; } //260519 hbk Phase 31 D-01
        public double Circle_Col { get; set; } //260519 hbk Phase 31 D-01
        public double Circle_Radius { get; set; } //260519 hbk Phase 31 D-01

        [Category("Edge")] //260519 hbk Phase 31 D-01
        public int EdgeThreshold { get; set; } = 10; //260519 hbk Phase 31 D-01
        public double Sigma { get; set; } = 1.0; //260519 hbk Phase 31 D-01
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260519 hbk Phase 31 D-01
        public string EdgePolarity { get; set; } = "DarkToLight"; //260519 hbk Phase 31 D-01

        //260519 hbk Phase 31 CO-23.1-02 — Datum 원 알고리즘 (polar sampling) 사용: Inward/Outward 방사 방향.
        //  CircleDiameterMeasurement Phase 28 REQ-28-01 패턴 동일. Phase 31 신규 타입 → INI 하위호환 불필요, 기본값 "Inward".
        [ItemsSourceProperty(nameof(Circle_RadialDirectionList))] //260519 hbk Phase 31 CO-23.1-02
        public string Circle_RadialDirection { get; set; } = "Inward"; //260519 hbk Phase 31 CO-23.1-02

        //260519 hbk Phase 31 D-01 — PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } } //260519 hbk Phase 31 D-01
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 CO-23.1-02
        public List<string> Circle_RadialDirectionList { get { return EdgeOptionLists.RadialDirections; } } //260519 hbk Phase 31 CO-23.1-02

        //260519 hbk Phase 31 CO-23.1-02 — Circle polar-sampling 알고리즘 파라미터 (DatumConfig Circle_PolarStepDeg/RectL1/L2Ratio 동일).
        //  PolarStepDeg: 사각형 ROI 회전 각도 step (360/step = 검사 각도 개수, 각도별 에지 1점 누적).
        //  RectL1Ratio:  사각형 ROI length1 = radius × ratio (반경 방향). RectL2Ratio: length2 (접선 방향).
        //  사용자 0/음수 입력 시 TryFindCircleByPolarSampling 진입부 sanity clamp 으로 default 복원.
        [Category("Circle|Polar")] //260519 hbk Phase 31 CO-23.1-02
        public double Circle_PolarStepDeg { get; set; } = 10.0; //260519 hbk Phase 31 CO-23.1-02
        public double Circle_RectL1Ratio { get; set; } = 0.02; //260519 hbk Phase 31 CO-23.1-02
        public double Circle_RectL2Ratio { get; set; } = 0.02; //260519 hbk Phase 31 CO-23.1-02

        //260519 hbk Phase 31 D-01 — 측정 거리 축 선택: E8 = Datum B Y 방향이므로 기본값 "Y"
        [Category("Edge")] //260519 hbk Phase 31 D-01
        [ItemsSourceProperty(nameof(MeasureAxisList))] //260519 hbk Phase 31 D-01
        public string MeasureAxis { get; set; } = "Y"; //260519 hbk Phase 31 D-01 (E8 = Datum B Y 방향)
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        public List<string> MeasureAxisList { get { return new List<string> { "Y", "X" }; } } //260519 hbk Phase 31 D-01

        //260519 hbk Phase 31 D-01 — datum 교점 좌표 runtime 주입 전용 (Action_FAIMeasurement IDatumOriginConsumer 경로).
        //  transient: PropertyGrid 미표시, JSON 직렬화 제외. EdgeToLineDistanceMeasurement.cs L69~80 동일 패턴.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-01
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-01
        public double DatumOriginRow { get; set; } //260519 hbk Phase 31 D-01 — datum 교점 row (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-01
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-01
        public double DatumOriginCol { get; set; } //260519 hbk Phase 31 D-01 — datum 교점 col (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-01
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-01
        public double DatumAngleRad { get; set; } //260519 hbk Phase 31 D-01 — datum 기준선 각도(rad). 미주입 시 0.

        public CircleCenterDistanceMeasurement(object owner) : base(owner) { } //260519 hbk Phase 31 D-01

        public override bool TryExecute( //260519 hbk Phase 31 D-01
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>(); //260519 hbk Phase 31 D-01

            var svc = new VisionAlgorithmService();
            double foundRow, foundCol, foundRadius;

            //260519 hbk Phase 31 CO-23.1-02 — Circle_RadialDirection 빈값=legacy fit / Inward,Outward=Datum polar sampling 분기
            //  CircleDiameterMeasurement Phase 28 REQ-28-02 패턴 동일.
            if (string.IsNullOrEmpty(Circle_RadialDirection)) //260519 hbk Phase 31 CO-23.1-02
            {
                //260519 hbk Phase 31 CO-23.1-02 — legacy fit 경로 (EdgePolarity 사용)
                if (!svc.TryFindCircle(image,
                    Circle_Row, Circle_Col, Circle_Radius,
                    datumTransform,
                    Sigma, EdgeThreshold, EdgePolarity,
                    out foundRow, out foundCol, out foundRadius, out error))
                {
                    return false;
                }
            }
            else //260519 hbk Phase 31 CO-23.1-02
            {
                //260519 hbk Phase 31 CO-23.1-02 — Datum 원 알고리즘 (polar sampling): 360° 회전하며 각도별 사각형 ROI MeasurePos.
                //  polarity = MapRadialDirectionToHalconPolarity(Circle_RadialDirection) (EdgePolarity 무시)
                //  step/L1/L2 = 사용자 편집 파라미터 (DatumConfig Circle_* 와 동일 의미). selection = "all" (각도별 에지 전체 누적).
                string polarity = EdgeOptionLists.MapRadialDirectionToHalconPolarity(Circle_RadialDirection); //260519 hbk Phase 31 CO-23.1-02
                HTuple unusedRows, unusedCols; //260519 hbk Phase 31 CO-23.1-02
                bool[] unusedStrips; //260519 hbk Phase 31 CO-23.1-02
                if (!svc.TryFindCircleByPolarSampling(
                    image,
                    Circle_Row, Circle_Col, Circle_Radius,
                    Circle_PolarStepDeg,   //260519 hbk Phase 31 CO-23.1-02 — 회전 각도 step (사용자 편집)
                    Circle_RectL1Ratio,    //260519 hbk Phase 31 CO-23.1-02 — rect length1 비율 (사용자 편집)
                    Circle_RectL2Ratio,    //260519 hbk Phase 31 CO-23.1-02 — rect length2 비율 (사용자 편집)
                    Sigma, EdgeThreshold, polarity,
                    "all",                 //260519 hbk Phase 31 CO-23.1-02 — 각도별 에지점 전체 누적 (strip-loop, 단일점 금지)
                    datumTransform,
                    out foundRow, out foundCol, out foundRadius,
                    out unusedRows, out unusedCols, out unusedStrips,
                    out error)) //260519 hbk Phase 31 CO-23.1-02
                {
                    return false; //260519 hbk Phase 31 CO-23.1-02
                }
            }

            //260519 hbk Phase 31 D-01 — D-04 공용 헬퍼 호출: 원중심 → datum 기준선 투영 거리
            //260519 hbk Phase 31 hotfix(결함 A/B) — foot 반환 오버로드: overlay(거리선/교점) 표시용
            double footRow, footCol;
            bool footOk;
            resultValue = VisionAlgorithmService.ComputeProjectionDistance(
                foundRow, foundCol,
                DatumOriginRow, DatumOriginCol, DatumAngleRad,
                pixelResolution, MeasureAxis,
                out footRow, out footCol, out footOk);

            //260519 hbk Phase 31 hotfix(결함 A) — 측정 결과 overlay 채움 (was: 빈 리스트 → 화면 표시 0).
            //  EdgeToLineDistanceMeasurement.TryExecute L199~259 패턴 동일.
            //  1) FAI-Edge1 = 검출 원중심 마커 (HalconDisplayService 녹/적 분기 + Action_FAIMeasurement -OK/-NG suffix)
            overlays.Add(new EdgeInspectionOverlay //260519 hbk Phase 31 hotfix
            {
                RoiId = "FAI-Edge1", //260519 hbk Phase 31 hotfix
                LineRow1 = foundRow, LineColumn1 = foundCol, //260519 hbk Phase 31 hotfix — 길이 0 라인 = 점 마커
                LineRow2 = foundRow, LineColumn2 = foundCol, //260519 hbk Phase 31 hotfix
                Points = new List<EdgeInspectionPoint> //260519 hbk Phase 31 hotfix
                {
                    new EdgeInspectionPoint { Row = foundRow, Column = foundCol } //260519 hbk Phase 31 hotfix — 원중심 X 마커
                }
            });

            //260519 hbk Phase 31 hotfix(결함 B) — datum 주입 시: datum 기준선 + 수선의 발(교점) + 거리선 표시.
            bool datumInjected = (DatumOriginRow != 0.0 || DatumOriginCol != 0.0); //260519 hbk Phase 31 hotfix
            if (datumInjected) //260519 hbk Phase 31 hotfix
            {
                //260519 hbk Phase 31 hotfix#2 — 2) datum 교점을 지나는 가로/세로 기준선을 이미지 끝까지 표시(사용자 요청).
                //  halfLength = 이미지 대각선 → 원점 위치·각도 무관하게 이미지 전체 span (DispLine 이 window 로 클립).
                double axisHalfLen = 4000.0; //260519 hbk Phase 31 hotfix#2 — HImage 크기 조회 실패 시 폴백
                try //260519 hbk Phase 31 hotfix#2
                {
                    HTuple imgW, imgH; //260519 hbk Phase 31 hotfix#2
                    image.GetImageSize(out imgW, out imgH); //260519 hbk Phase 31 hotfix#2
                    axisHalfLen = System.Math.Sqrt(imgW.D * imgW.D + imgH.D * imgH.D); //260519 hbk Phase 31 hotfix#2
                }
                catch { } //260519 hbk Phase 31 hotfix#2 — 조회 실패 시 폴백 길이 유지

                //260519 hbk Phase 31 hotfix#2 — 가로 datum 기준선(수평선, "Y" 축) — datum 교점 마커 동반
                double hR1, hC1, hR2, hC2; //260519 hbk Phase 31 hotfix#2
                VisionAlgorithmService.GetDatumAxisLine(
                    DatumOriginRow, DatumOriginCol, DatumAngleRad, "Y", axisHalfLen,
                    out hR1, out hC1, out hR2, out hC2); //260519 hbk Phase 31 hotfix#2
                overlays.Add(new EdgeInspectionOverlay //260519 hbk Phase 31 hotfix#2
                {
                    RoiId = "FAI-DatumLine", //260519 hbk Phase 31 hotfix#2 — 미지정 분기 파랑 (datum 기준선)
                    LineRow1 = hR1, LineColumn1 = hC1, //260519 hbk Phase 31 hotfix#2
                    LineRow2 = hR2, LineColumn2 = hC2, //260519 hbk Phase 31 hotfix#2
                    Points = new List<EdgeInspectionPoint> //260519 hbk Phase 31 hotfix#2 — datum 교점(원점) 마커
                    {
                        new EdgeInspectionPoint { Row = DatumOriginRow, Column = DatumOriginCol } //260519 hbk Phase 31 hotfix#2
                    }
                });

                //260519 hbk Phase 31 hotfix#2 — 세로 datum 기준선(수직선, "X" 축)
                double vR1, vC1, vR2, vC2; //260519 hbk Phase 31 hotfix#2
                VisionAlgorithmService.GetDatumAxisLine(
                    DatumOriginRow, DatumOriginCol, DatumAngleRad, "X", axisHalfLen,
                    out vR1, out vC1, out vR2, out vC2); //260519 hbk Phase 31 hotfix#2
                overlays.Add(new EdgeInspectionOverlay //260519 hbk Phase 31 hotfix#2
                {
                    RoiId = "FAI-DatumLine", //260519 hbk Phase 31 hotfix#2
                    LineRow1 = vR1, LineColumn1 = vC1, //260519 hbk Phase 31 hotfix#2
                    LineRow2 = vR2, LineColumn2 = vC2 //260519 hbk Phase 31 hotfix#2
                });

                //260519 hbk Phase 31 hotfix — 3) FAI-DistLine = 원중심 → 수선의 발 (수직 거리선, cyan)
                if (footOk) //260519 hbk Phase 31 hotfix
                {
                    overlays.Add(new EdgeInspectionOverlay //260519 hbk Phase 31 hotfix
                    {
                        RoiId = "FAI-DistLine", //260519 hbk Phase 31 hotfix — HalconDisplayService cyan 분기
                        LineRow1 = footRow, LineColumn1 = footCol, //260519 hbk Phase 31 hotfix — 수선의 발 (datum 기준선 위)
                        LineRow2 = foundRow, LineColumn2 = foundCol, //260519 hbk Phase 31 hotfix — 원중심
                        Points = new List<EdgeInspectionPoint> //260519 hbk Phase 31 hotfix — 양 끝점 X 마커
                        {
                            new EdgeInspectionPoint { Row = footRow, Column = footCol }, //260519 hbk Phase 31 hotfix
                            new EdgeInspectionPoint { Row = foundRow, Column = foundCol } //260519 hbk Phase 31 hotfix
                        }
                    });
                }
            }

            return true;
        }
    }
}
