using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Point ROI에서 에지 라인을 피팅하여 중점을 점으로 사용하고,
    /// Line ROI에서 에지 라인을 피팅한 뒤 두 대상 간 수직 거리를 산출한다.
    /// 결과 단위: mm (pixelResolution 적용).
    /// </summary>
    public class PointToLineDistanceMeasurement : MeasurementBase
    {
        public override string TypeName { get { return "PointToLineDistance"; } }

        [Category("Point|ROI")]
        public double Point_Row { get; set; }
        public double Point_Col { get; set; }
        public double Point_Phi { get; set; }
        public double Point_Length1 { get; set; }
        public double Point_Length2 { get; set; }

        [Category("Line|ROI")]
        public double Line_Row { get; set; }
        public double Line_Col { get; set; }
        public double Line_Phi { get; set; }
        public double Line_Length1 { get; set; }
        public double Line_Length2 { get; set; }

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
        public string EdgeDirection { get; set; } = "LtoR";

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }

        public PointToLineDistanceMeasurement(object owner) : base(owner) { }

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
            List<System.ValueTuple<double, double>> collectedEdgePoints = new List<System.ValueTuple<double, double>>();
            if (!svc.TryFitLine(image,
                Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out pr1, out pc1, out pr2, out pc2, out error,
                collectedEdges: collectedEdgePoints))
            {
                return false;
            }
            double pRow = (pr1 + pr2) / 2.0;            // 라인 중점 — 폴백(수집점 없음)용으로 유지
            double pCol = (pc1 + pc2) / 2.0;

            double lr1, lc1, lr2, lc2;
            if (!svc.TryFitLine(image,
                Line_Row, Line_Col, Line_Phi, Line_Length1, Line_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out lr1, out lc1, out lr2, out lc2, out error))
            {
                return false;
            }

            // 수집 에지점(collectedEdgePoints) 각각의 기준선까지 UNSIGNED 수직거리를 구해 산술평균한다.
            // (기존: 라인 중점 1점만으로 거리 산출 — 단일점 노이즈에 취약했음)
            double distPixel;
            if (collectedEdgePoints.Count >= 1)
            {
                double sumDistPixel = 0.0;
                foreach (var ep in collectedEdgePoints)
                {
                    sumDistPixel += VisionAlgorithmService.DistancePointToLine(ep.Item1, ep.Item2, lr1, lc1, lr2, lc2);
                }
                distPixel = sumDistPixel / collectedEdgePoints.Count;
            }
            else
            {
                // 폴백: 수집점 없음 — 기존 단일-중점 동작 그대로
                distPixel = VisionAlgorithmService.DistancePointToLine(pRow, pCol, lr1, lc1, lr2, lc2);
            }
            resultValue = distPixel * pixelResolution;
            return true;
        }
    }
}
