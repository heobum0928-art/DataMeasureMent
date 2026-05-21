//260519 hbk Phase 31 D-05 — D1/H5: Datum A 기준 직선 각도
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
    /// D-05: D1/H5(SOP) = 신규 타입 EdgeToLineAngle (ROI 1개 + Datum A 기준선).
    /// </summary>
    public class EdgeToLineAngleMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-05
    {
        public override string TypeName { get { return "EdgeToLineAngle"; } } //260519 hbk Phase 31 D-05

        [Category("Point|ROI")] //260519 hbk Phase 31 D-05
        public double Point_Row { get; set; } //260519 hbk Phase 31 D-05
        public double Point_Col { get; set; } //260519 hbk Phase 31 D-05
        public double Point_Phi { get; set; } //260519 hbk Phase 31 D-05
        public double Point_Length1 { get; set; } //260519 hbk Phase 31 D-05
        public double Point_Length2 { get; set; } //260519 hbk Phase 31 D-05

        [Category("Edge")] //260519 hbk Phase 31 D-05
        public int EdgeThreshold { get; set; } = 10; //260519 hbk Phase 31 D-05
        public double Sigma { get; set; } = 1.0; //260519 hbk Phase 31 D-05
        public int EdgeSampleCount { get; set; } = 20; //260519 hbk Phase 31 D-05
        public int EdgeTrimCount { get; set; } = 10; //260519 hbk Phase 31 D-05
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260519 hbk Phase 31 D-05
        public string EdgePolarity { get; set; } = "DarkToLight"; //260519 hbk Phase 31 D-05
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260519 hbk Phase 31 D-05
        public string EdgeDirection { get; set; } = "TtoB"; //260519 hbk Phase 31 D-05

        //260519 hbk Phase 31 D-05 — PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-05
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } } //260519 hbk Phase 31 D-05
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-05
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } } //260519 hbk Phase 31 D-05

        //260519 hbk Phase 31 D-05 — datum 교점 좌표 runtime 주입 전용 (Action_FAIMeasurement IDatumOriginConsumer 경로).
        //  transient: PropertyGrid 미표시, JSON 직렬화 제외. EdgeToLineDistanceMeasurement.cs L69~80 동일 패턴.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-05
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-05
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-05
        public double DatumOriginRow { get; set; } //260519 hbk Phase 31 D-05 — datum 교점 row (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-05
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-05
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-05
        public double DatumOriginCol { get; set; } //260519 hbk Phase 31 D-05 — datum 교점 col (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-05
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-05
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-05
        public double DatumAngleRad { get; set; } //260519 hbk Phase 31 D-05 — datum 1차(수평) 기준선 각도(rad). 미주입 시 0.
        //260519 hbk Phase 31 hotfix#3 — IDatumOriginConsumer 2차 각도 (EdgeToLineAngle 은 각도 측정 — 1차선 기준 유지, 속성만 구현)
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 hotfix#3
        public double DatumAngle2Rad { get; set; } //260519 hbk Phase 31 hotfix#3
        //260521 hbk Phase 32 — IDatumOriginConsumer 확장. 본 타입은 검출 원중심 미사용 (E2 만 사용) — 주입만 받고 미참조.
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumDetectedCircleRow { get; set; } //260521 hbk Phase 32
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumDetectedCircleCol { get; set; } //260521 hbk Phase 32

        public EdgeToLineAngleMeasurement(object owner) : base(owner) { } //260519 hbk Phase 31 D-05

        public override bool TryExecute( //260519 hbk Phase 31 D-05
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>(); //260519 hbk Phase 31 D-05

            var svc = new VisionAlgorithmService();
            double pr1, pc1, pr2, pc2;
            //260519 hbk Phase 31 hotfix#5 — strip-loop 누적 raw 에지점 수집용 (overlay 가시화)
            var rawEdges = new List<System.ValueTuple<double, double>>(); //260519 hbk Phase 31 hotfix#5

            //260519 hbk Phase 31 D-05 — TryFitLine "All" 고정 (memory feedback — EdgeSelection 명시 필수, CO-23-01 구조적 차단)
            if (!svc.TryFitLine(image,
                Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out pr1, out pc1, out pr2, out pc2, out error,
                "All", //260519 hbk Phase 31 D-05 — EdgeSelection "All" 고정
                rawEdges)) //260519 hbk Phase 31 hotfix#5 — raw 에지점 수집
            {
                return false;
            }

            //260519 hbk Phase 31 D-05 — Datum A 기준선: 교점 통과, 방향 = DatumAngleRad (1차 수평 기준선).
            //260519 hbk Phase 31 hotfix#4 — overlay 가 이미지 끝까지 그리도록 halfLength = 이미지 대각선
            double daHalfLen = 4000.0; //260519 hbk Phase 31 hotfix#4 — HImage 크기 조회 실패 시 폴백
            try //260519 hbk Phase 31 hotfix#4
            {
                HTuple imgW, imgH; //260519 hbk Phase 31 hotfix#4
                image.GetImageSize(out imgW, out imgH); //260519 hbk Phase 31 hotfix#4
                daHalfLen = System.Math.Sqrt(imgW.D * imgW.D + imgH.D * imgH.D); //260519 hbk Phase 31 hotfix#4
            }
            catch { } //260519 hbk Phase 31 hotfix#4
            double daR1, daC1, daR2, daC2; //260519 hbk Phase 31 hotfix#4
            VisionAlgorithmService.GetDatumAxisLine(
                DatumOriginRow, DatumOriginCol, DatumAngleRad, daHalfLen,
                out daR1, out daC1, out daR2, out daC2); //260519 hbk Phase 31 hotfix#4

            //260519 hbk Phase 31 D-05 — AngleLineLine: 에지 피팅선 vs Datum A 기준선 각도(degree, 0~180)
            //  AngleLineLine 은 직선 정의에 길이 무관 — daHalfLen(이미지 대각선) 그대로 사용.
            resultValue = VisionAlgorithmService.AngleLineLine(
                pr1, pc1, pr2, pc2,
                daR1, daC1, daR2, daC2); //260519 hbk Phase 31 D-05

            //260519 hbk Phase 31 hotfix#4 — 결과 overlay 채움 (was: 빈 리스트 → 화면 표시 0). EdgeToLineDistance 패턴.
            //  1) FAI-Edge1 = 검출 에지 피팅선 (HalconDisplayService 녹/적 분기 + Action_FAIMeasurement -OK/-NG suffix)
            //260519 hbk Phase 31 hotfix#7 — 라인 상단부(row 최소) → datum 라인 교점까지만 표시 (사용자 요청).
            //  의도: 양쪽 풀스팬 연장은 시각 노이즈 → V자(상단부~교점) 형태로 각도 직관 강화.
            //  IntersectionLl 평행/공선/Infinity 폴백: 원본 짧은 라인 유지(회귀 0).
            double midR = (pr1 + pr2) / 2.0; //260519 hbk Phase 31 hotfix#4 — 에지 중점 마커용(보존)
            double midC = (pc1 + pc2) / 2.0; //260519 hbk Phase 31 hotfix#4
            double topR, topC; //260519 hbk Phase 31 hotfix#7 — 상단부 끝점(row 작은 쪽)
            if (pr1 <= pr2) { topR = pr1; topC = pc1; } //260519 hbk Phase 31 hotfix#7
            else { topR = pr2; topC = pc2; } //260519 hbk Phase 31 hotfix#7
            double extR1 = pr1, extC1 = pc1, extR2 = pr2, extC2 = pc2; //260519 hbk Phase 31 hotfix#7 — 폴백: 피팅선 그대로
            try //260519 hbk Phase 31 hotfix#7 — datum 라인과 에지 라인 교점 계산 (DatumFindingService.cs:125 패턴 차용)
            {
                HTuple ixRow, ixCol, isOverlap; //260519 hbk Phase 31 hotfix#7
                HOperatorSet.IntersectionLl(pr1, pc1, pr2, pc2, daR1, daC1, daR2, daC2,
                    out ixRow, out ixCol, out isOverlap); //260519 hbk Phase 31 hotfix#7
                if (isOverlap.I == 0
                    && !double.IsInfinity(ixRow.D) && !double.IsInfinity(ixCol.D)
                    && !double.IsNaN(ixRow.D) && !double.IsNaN(ixCol.D)) //260519 hbk Phase 31 hotfix#7
                {
                    extR1 = topR; extC1 = topC; //260519 hbk Phase 31 hotfix#7 — 상단부
                    extR2 = ixRow.D; extC2 = ixCol.D; //260519 hbk Phase 31 hotfix#7 — datum 교점
                }
            }
            catch { } //260519 hbk Phase 31 hotfix#7
            overlays.Add(new EdgeInspectionOverlay //260519 hbk Phase 31 hotfix#4
            {
                RoiId = "FAI-Edge1", //260519 hbk Phase 31 hotfix#4
                LineRow1 = extR1, LineColumn1 = extC1, //260519 hbk Phase 31 hotfix#7 — 상단부
                LineRow2 = extR2, LineColumn2 = extC2, //260519 hbk Phase 31 hotfix#7 — datum 교점
                Points = new List<EdgeInspectionPoint> //260519 hbk Phase 31 hotfix#4 — 에지 중점 마커
                {
                    new EdgeInspectionPoint { Row = midR, Column = midC } //260519 hbk Phase 31 hotfix#4
                }
            });
            //260519 hbk Phase 31 hotfix#4 — 2) FAI-DatumLine = Datum A 기준선 (각도 비교 대상, 파랑) + datum 교점 마커
            bool datumInjected = (DatumOriginRow != 0.0 || DatumOriginCol != 0.0); //260519 hbk Phase 31 hotfix#4
            if (datumInjected) //260519 hbk Phase 31 hotfix#4
            {
                overlays.Add(new EdgeInspectionOverlay //260519 hbk Phase 31 hotfix#4
                {
                    RoiId = "FAI-DatumLine", //260519 hbk Phase 31 hotfix#4 — 미지정 분기 파랑
                    LineRow1 = daR1, LineColumn1 = daC1, //260519 hbk Phase 31 hotfix#4
                    LineRow2 = daR2, LineColumn2 = daC2, //260519 hbk Phase 31 hotfix#4
                    Points = new List<EdgeInspectionPoint> //260519 hbk Phase 31 hotfix#4 — datum 교점 마커
                    {
                        new EdgeInspectionPoint { Row = DatumOriginRow, Column = DatumOriginCol } //260519 hbk Phase 31 hotfix#4
                    }
                });
            }
            //260519 hbk Phase 31 hotfix#5 — 3) FAI-EdgeRaw = strip-loop 누적 raw 에지점 (노랑 작은 + 마커, 라인 skip).
            //  HalconDisplayService 가 FAI-EdgeRaw RoiId 분기에서 DispCross 일괄 렌더 + DispLine skip.
            //  사용자 시각 검증: 라인 피팅에 실제로 사용된 점들이 ROI 어느 위치에 분포했는지 확인.
            if (rawEdges.Count > 0) //260519 hbk Phase 31 hotfix#5
            {
                var rawPts = new List<EdgeInspectionPoint>(rawEdges.Count); //260519 hbk Phase 31 hotfix#5
                foreach (var e in rawEdges) //260519 hbk Phase 31 hotfix#5
                {
                    rawPts.Add(new EdgeInspectionPoint { Row = e.Item1, Column = e.Item2 }); //260519 hbk Phase 31 hotfix#5
                }
                overlays.Add(new EdgeInspectionOverlay //260519 hbk Phase 31 hotfix#5
                {
                    RoiId = "FAI-EdgeRaw", //260519 hbk Phase 31 hotfix#5 — HalconDisplayService 전용 분기 (라인 skip + 노랑 작은 +)
                    LineRow1 = 0, LineColumn1 = 0, LineRow2 = 0, LineColumn2 = 0, //260519 hbk Phase 31 hotfix#5 — sentinel (분기에서 DispLine skip)
                    Points = rawPts //260519 hbk Phase 31 hotfix#5
                });
            }
            return true;
        }
    }
}
