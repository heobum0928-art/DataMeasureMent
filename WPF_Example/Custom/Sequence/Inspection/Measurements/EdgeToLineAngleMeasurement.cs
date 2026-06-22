using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Point ROI 에서 에지 라인을 피팅하고, Datum A 기준선과의 각도(degree)를 반환한다.
    /// EdgeToLineDistanceMeasurement 의 "ROI 1개 + Datum" 패턴을 거리 대신 각도로 연장한 구조.
    /// 단위는 degree 이므로 pixelResolution 미적용.
    /// </summary>
    public class EdgeToLineAngleMeasurement : MeasurementBase, IDatumOriginConsumer
    {
        public override string TypeName { get { return "EdgeToLineAngle"; } }

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
        //260622 hbk Phase 57.1: trim 의미가 양끝 각 %(비율)로 변경 → 라벨만 % 표기 (프로퍼티명/INI 키 보존)
        [DisplayName("Edge Trim (%)")]
        public int EdgeTrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string EdgePolarity { get; set; } = "DarkToLight";
        [ItemsSourceProperty(nameof(EdgeDirectionList))]
        public string EdgeDirection { get; set; } = "TtoB";
        // strip-loop 가 First/Last 도 stripCount 점 누적. INI 미존재 시 폴백 "All".
        [ItemsSourceProperty(nameof(EdgeSelectionList))]
        public string EdgeSelection { get; set; } = "All";

        //260616 hbk Phase 51 UAT: 보각(180-θ) 사용. Datum 기준선 반대방향 기준 각도로 보고.
        //  예) 이미지 0°기준 시계방향 138° → 우측 180°기준 반시계 42° = 180-138.
        //  raw 가 [0,180] 이므로 보각도 [0,180]. 기본 false = 기존 동작(회귀 0, INI 미존재 시 폴백 false).
        [Category("Angle")]
        public bool UseSupplementaryAngle { get; set; } = false;

        // PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } }

        // datum 교점 좌표 runtime 주입 전용 (IDatumOriginConsumer 경로).
        //  transient: PropertyGrid 미표시, JSON 직렬화 제외. EdgeToLineDistanceMeasurement 동일 패턴.
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
        // IDatumOriginConsumer 2차 각도. 본 타입은 각도 측정 — 1차선 기준 유지, 속성만 구현.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumAngle2Rad { get; set; }
        // IDatumOriginConsumer 확장. 본 타입은 검출 원중심 미사용 — 주입만 받고 미참조.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleRow { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleCol { get; set; }

        public EdgeToLineAngleMeasurement(object owner) : base(owner) { }

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
            // strip-loop 누적 raw 에지점 수집용 (overlay 가시화)
            var rawEdges = new List<System.ValueTuple<double, double>>();

            // EdgeSelection 사용자 선택값 사용. strip-loop 가 First/Last 도 stripCount 점 누적하므로 안전.
            if (!svc.TryFitLine(image,
                Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out pr1, out pc1, out pr2, out pc2, out error,
                EdgeSelection,
                rawEdges))
            {
                return false;
            }

            // Datum A 기준선: 교점 통과, 방향 = DatumAngleRad (1차 수평 기준선).
            // overlay 가 이미지 끝까지 그리도록 halfLength = 이미지 대각선
            double daHalfLen = 4000.0; // HImage 크기 조회 실패 시 폴백
            try
            {
                HTuple imgW, imgH;
                image.GetImageSize(out imgW, out imgH);
                daHalfLen = System.Math.Sqrt(imgW.D * imgW.D + imgH.D * imgH.D);
            }
            catch { }
            double daR1, daC1, daR2, daC2;
            VisionAlgorithmService.GetDatumAxisLine(
                DatumOriginRow, DatumOriginCol, DatumAngleRad, daHalfLen,
                out daR1, out daC1, out daR2, out daC2);

            // AngleLineLine: 에지 피팅선 vs Datum A 기준선 각도(degree, 0~180)
            //  AngleLineLine 은 직선 정의에 길이 무관 — daHalfLen(이미지 대각선) 그대로 사용.
            resultValue = VisionAlgorithmService.AngleLineLine(
                pr1, pc1, pr2, pc2,
                daR1, daC1, daR2, daC2);

            //260616 hbk Phase 51 UAT: 보각 옵션 — 기준 방향 반대로 각도 보고 (180-θ). raw [0,180] → 보각도 [0,180]. overlay 는 raw 기하 유지.
            if (UseSupplementaryAngle)
            {
                resultValue = 180.0 - resultValue;
            }

            // 결과 overlay 채움. EdgeToLineDistance 패턴.
            //  1) FAI-Edge1 = 검출 에지 피팅선
            //  라인 상단부(row 최소) → datum 라인 교점까지만 표시.
            //  의도: 양쪽 풀스팬 연장은 시각 노이즈 → V자(상단부~교점) 형태로 각도 직관 강화.
            //  IntersectionLl 평행/공선/Infinity 폴백: 원본 짧은 라인 유지.
            double midR = (pr1 + pr2) / 2.0; // 에지 중점 마커용
            double midC = (pc1 + pc2) / 2.0;
            double topR, topC; // 상단부 끝점(row 작은 쪽)
            if (pr1 <= pr2) { topR = pr1; topC = pc1; }
            else { topR = pr2; topC = pc2; }
            double extR1 = pr1, extC1 = pc1, extR2 = pr2, extC2 = pc2; // 폴백: 피팅선 그대로
            try // datum 라인과 에지 라인 교점 계산
            {
                HTuple ixRow, ixCol, isOverlap;
                HOperatorSet.IntersectionLl(pr1, pc1, pr2, pc2, daR1, daC1, daR2, daC2,
                    out ixRow, out ixCol, out isOverlap);
                if (isOverlap.I == 0
                    && !double.IsInfinity(ixRow.D) && !double.IsInfinity(ixCol.D)
                    && !double.IsNaN(ixRow.D) && !double.IsNaN(ixCol.D))
                {
                    extR1 = topR; extC1 = topC; // 상단부
                    extR2 = ixRow.D; extC2 = ixCol.D; // datum 교점
                }
            }
            catch { }
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge1",
                LineRow1 = extR1, LineColumn1 = extC1, // 상단부
                LineRow2 = extR2, LineColumn2 = extC2, // datum 교점
                Points = new List<EdgeInspectionPoint> // 에지 중점 마커
                {
                    new EdgeInspectionPoint { Row = midR, Column = midC }
                }
            });
            // 2) FAI-DatumLine = Datum A 기준선 (각도 비교 대상) + datum 교점 마커
            bool datumInjected = (DatumOriginRow != 0.0 || DatumOriginCol != 0.0);
            if (datumInjected)
            {
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "FAI-DatumLine",
                    LineRow1 = daR1, LineColumn1 = daC1,
                    LineRow2 = daR2, LineColumn2 = daC2,
                    Points = new List<EdgeInspectionPoint> // datum 교점 마커
                    {
                        new EdgeInspectionPoint { Row = DatumOriginRow, Column = DatumOriginCol }
                    }
                });
            }
            // 3) FAI-EdgeRaw = strip-loop 누적 raw 에지점.
            //  HalconDisplayService 가 FAI-EdgeRaw RoiId 분기에서 DispCross 일괄 렌더 + DispLine skip.
            //  라인 피팅에 실제로 사용된 점들이 ROI 어느 위치에 분포했는지 확인용.
            if (rawEdges.Count > 0)
            {
                var rawPts = new List<EdgeInspectionPoint>(rawEdges.Count);
                foreach (var e in rawEdges)
                {
                    rawPts.Add(new EdgeInspectionPoint { Row = e.Item1, Column = e.Item2 });
                }
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "FAI-EdgeRaw", // HalconDisplayService 전용 분기 (라인 skip)
                    LineRow1 = 0, LineColumn1 = 0, LineRow2 = 0, LineColumn2 = 0, // sentinel (분기에서 DispLine skip)
                    Points = rawPts
                });
            }
            return true;
        }
    }
}
