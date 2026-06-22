using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// 두 Line ROI에서 직선을 피팅하고 Line1 중점에서 Line2까지의 수직 거리(mm)를 반환한다.
    /// </summary>
    public class LineToLineDistanceMeasurement : MeasurementBase
    {
        public override string TypeName { get { return "LineToLineDistance"; } }

        [Category("Line1|ROI")]
        public double Line1_Row { get; set; }
        public double Line1_Col { get; set; }
        public double Line1_Phi { get; set; }
        public double Line1_Length1 { get; set; }
        public double Line1_Length2 { get; set; }

        [Category("Line2|ROI")]
        public double Line2_Row { get; set; }
        public double Line2_Col { get; set; }
        public double Line2_Phi { get; set; }
        public double Line2_Length1 { get; set; }
        public double Line2_Length2 { get; set; }

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

        public LineToLineDistanceMeasurement(object owner) : base(owner) { }

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

            double l1r1, l1c1, l1r2, l1c2;
            if (!svc.TryFitLine(image,
                Line1_Row, Line1_Col, Line1_Phi, Line1_Length1, Line1_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out l1r1, out l1c1, out l1r2, out l1c2, out error))
            {
                return false;
            }

            double l2r1, l2c1, l2r2, l2c2;
            if (!svc.TryFitLine(image,
                Line2_Row, Line2_Col, Line2_Phi, Line2_Length1, Line2_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out l2r1, out l2c1, out l2r2, out l2c2, out error))
            {
                return false;
            }

            double midRow = (l1r1 + l1r2) / 2.0;
            double midCol = (l1c1 + l1c2) / 2.0;
            double distPixel = VisionAlgorithmService.DistancePointToLine(midRow, midCol, l2r1, l2c1, l2r2, l2c2);
            resultValue = distPixel * pixelResolution;
            return true;
        }
    }
}
