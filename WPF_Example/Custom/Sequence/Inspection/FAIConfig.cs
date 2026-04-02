using System;
using PropertyTools.DataAnnotations;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    public enum EEdgeMeasureType {
        FirstToFirst,
        FirstToLast,
        LastToFirst,
        LastToLast
    }

    public class FAIConfig : ParamBase {

        // ROI
        [Category("ROI")]
        public double ROI_Row { get; set; }
        public double ROI_Col { get; set; }
        public double ROI_Phi { get; set; }
        public double ROI_Length1 { get; set; }
        public double ROI_Length2 { get; set; }

        // Edge Measurement
        [Category("Edge|Measurement")]
        public EEdgeMeasureType MeasureType { get; set; } = EEdgeMeasureType.FirstToFirst;
        public double Threshold { get; set; } = 30.0;
        public double Sigma { get; set; } = 1.0;

        // Tolerance
        [Category("Tolerance")]
        public double NominalValue { get; set; }
        public double UpperTolerance { get; set; }
        public double LowerTolerance { get; set; }

        // Result (runtime, not saved)
        [Browsable(false)]
        public double MeasuredValue { get; set; }

        [Browsable(false)]
        public bool IsPass { get; set; }

        [Browsable(false)]
        public string FAIName { get; set; }

        public FAIConfig(object owner) : base(owner) {
        }

        public FAIConfig(object owner, string name) : base(owner) {
            FAIName = name;
        }

        public void SetResult(double measuredValue) {
            MeasuredValue = measuredValue;
            double lower = NominalValue - Math.Abs(LowerTolerance);
            double upper = NominalValue + Math.Abs(UpperTolerance);
            IsPass = (measuredValue >= lower) && (measuredValue <= upper);
        }

        public void ClearResult() {
            MeasuredValue = 0;
            IsPass = false;
        }
    }
}
