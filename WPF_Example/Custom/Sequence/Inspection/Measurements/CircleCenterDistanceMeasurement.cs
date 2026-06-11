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
    public class CircleCenterDistanceMeasurement : MeasurementBase, IDatumOriginConsumer
    {
        public override string TypeName { get { return "CircleCenterDistance"; } }

        [Category("Circle|ROI")]
        public double Circle_Row { get; set; }
        public double Circle_Col { get; set; }
        public double Circle_Radius { get; set; }

        [Category("Edge")]
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string EdgePolarity { get; set; } = "DarkToLight";

        // Datum 원 알고리즘 (polar sampling) 사용: Inward/Outward 방사 방향.
        //  CircleDiameterMeasurement REQ-28-01 패턴 동일. 신규 타입 → INI 하위호환 불필요, 기본값 "Inward".
        [ItemsSourceProperty(nameof(Circle_RadialDirectionList))]
        public string Circle_RadialDirection { get; set; } = "Inward";

        // PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Circle_RadialDirectionList { get { return EdgeOptionLists.RadialDirections; } }

        // Circle polar-sampling 알고리즘 파라미터 (DatumConfig Circle_PolarStepDeg/RectL1/L2Ratio 동일).
        //  PolarStepDeg: 사각형 ROI 회전 각도 step (360/step = 검사 각도 개수, 각도별 에지 1점 누적).
        //  RectL1Ratio:  사각형 ROI length1 = radius × ratio (반경 방향). RectL2Ratio: length2 (접선 방향).
        //  사용자 0/음수 입력 시 TryFindCircleByPolarSampling 진입부 sanity clamp 으로 default 복원.
        [Category("Circle|Polar")]
        public double Circle_PolarStepDeg { get; set; } = 10.0;
        public double Circle_RectL1Ratio { get; set; } = 0.02;
        public double Circle_RectL2Ratio { get; set; } = 0.02;

        // 측정 거리 축 선택: E8 = Datum B Y 방향이므로 기본값 "Y"
        [Category("Edge")]
        [ItemsSourceProperty(nameof(MeasureAxisList))]
        public string MeasureAxis { get; set; } = "Y";
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

        public CircleCenterDistanceMeasurement(object owner) : base(owner) { }

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
            double foundRow, foundCol, foundRadius;

            // Circle_RadialDirection 빈값=legacy fit / Inward,Outward=Datum polar sampling 분기
            //  CircleDiameterMeasurement REQ-28-02 패턴 동일.
            if (string.IsNullOrEmpty(Circle_RadialDirection))
            {
                // legacy fit 경로 (EdgePolarity 사용)
                if (!svc.TryFindCircle(image,
                    Circle_Row, Circle_Col, Circle_Radius,
                    datumTransform,
                    Sigma, EdgeThreshold, EdgePolarity,
                    out foundRow, out foundCol, out foundRadius, out error))
                {
                    return false;
                }
            }
            else
            {
                // Datum 원 알고리즘 (polar sampling): 360° 회전하며 각도별 사각형 ROI MeasurePos.
                //  polarity = MapRadialDirectionToHalconPolarity(Circle_RadialDirection) (EdgePolarity 무시)
                //  step/L1/L2 = 사용자 편집 파라미터 (DatumConfig Circle_* 와 동일 의미). selection = "all" (각도별 에지 전체 누적).
                string polarity = EdgeOptionLists.MapRadialDirectionToHalconPolarity(Circle_RadialDirection);
                HTuple unusedRows, unusedCols;
                bool[] unusedStrips;
                if (!svc.TryFindCircleByPolarSampling(
                    image,
                    Circle_Row, Circle_Col, Circle_Radius,
                    Circle_PolarStepDeg,   // 회전 각도 step (사용자 편집)
                    Circle_RectL1Ratio,    // rect length1 비율 (사용자 편집)
                    Circle_RectL2Ratio,    // rect length2 비율 (사용자 편집)
                    Sigma, EdgeThreshold, polarity,
                    "all",                 // 각도별 에지점 전체 누적 (strip-loop, 단일점 금지)
                    datumTransform,
                    out foundRow, out foundCol, out foundRadius,
                    out unusedRows, out unusedCols, out unusedStrips,
                    out error))
                {
                    return false;
                }
            }

            // D-04 공용 헬퍼 호출: 원중심 → datum 기준선 투영 거리.
            // foot 반환 오버로드: overlay(거리선/교점) 표시용.
            double footRow, footCol;
            bool footOk;
            // X축 측정은 2차(수직) 기준선, Y축은 1차(수평) 기준선
            double measureLineAngle;
            if (MeasureAxis == "X")
                measureLineAngle = DatumAngle2Rad;
            else
                measureLineAngle = DatumAngleRad;
            resultValue = VisionAlgorithmService.ComputeProjectionDistance(
                foundRow, foundCol,
                DatumOriginRow, DatumOriginCol, measureLineAngle,
                pixelResolution, MeasureAxis,
                out footRow, out footCol, out footOk);

            // 측정 결과 overlay 채움 (was: 빈 리스트 → 화면 표시 0).
            //  EdgeToLineDistanceMeasurement.TryExecute L199~259 패턴 동일.
            //  1) FAI-Edge1 = 검출 원중심 마커 (HalconDisplayService 녹/적 분기 + Action_FAIMeasurement -OK/-NG suffix)
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge1",
                LineRow1 = foundRow, LineColumn1 = foundCol, // 길이 0 라인 = 점 마커
                LineRow2 = foundRow, LineColumn2 = foundCol,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = foundRow, Column = foundCol } // 원중심 X 마커
                }
            });

            // datum 주입 시: datum 기준선 + 수선의 발(교점) + 거리선 표시.
            bool datumInjected = (DatumOriginRow != 0.0 || DatumOriginCol != 0.0);
            if (datumInjected)
            {
                // 2) datum 교점을 지나는 가로/세로 기준선을 이미지 끝까지 표시.
                //  halfLength = 이미지 대각선 → 원점 위치·각도 무관하게 이미지 전체 span (DispLine 이 window 로 클립).
                double axisHalfLen = 4000.0; // HImage 크기 조회 실패 시 폴백
                try
                {
                    HTuple imgW, imgH;
                    image.GetImageSize(out imgW, out imgH);
                    axisHalfLen = System.Math.Sqrt(imgW.D * imgW.D + imgH.D * imgH.D);
                }
                catch { } // 조회 실패 시 폴백 길이 유지

                // 가로 datum 기준선(1차 수평선, DatumAngleRad) — datum 교점 마커 동반
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

                // 세로 datum 기준선(2차 수직선, DatumAngle2Rad — 실제 datum 수직선)
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

                // 3) FAI-DistLine = 원중심 → 수선의 발 (수직 거리선, cyan)
                if (footOk)
                {
                    overlays.Add(new EdgeInspectionOverlay
                    {
                        RoiId = "FAI-DistLine", // HalconDisplayService cyan 분기
                        LineRow1 = footRow, LineColumn1 = footCol, // 수선의 발 (datum 기준선 위)
                        LineRow2 = foundRow, LineColumn2 = foundCol, // 원중심
                        Points = new List<EdgeInspectionPoint> // 양 끝점 X 마커
                        {
                            new EdgeInspectionPoint { Row = footRow, Column = footCol },
                            new EdgeInspectionPoint { Row = foundRow, Column = foundCol }
                        }
                    });
                }
            }

            return true;
        }
    }
}
