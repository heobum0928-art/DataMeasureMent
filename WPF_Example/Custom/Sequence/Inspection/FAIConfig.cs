using System;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Models;
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

        // Calibration (per D-12, D-16: camera-level calibration stored in CameraSlaveParam,
        // but FAIConfig also carries PixelResolution for RoiDefinition compatibility)
        [Category("Calibration")]
        public double PixelResolutionX { get; set; } = 1.0;  // mm/pixel
        public double PixelResolutionY { get; set; } = 1.0;  // mm/pixel

        // Polygon ROI (per D-15: serialized as "x1,y1;x2,y2;x3,y3" string for INI storage)
        [Category("ROI")]
        public string PolygonPoints { get; set; } = "";

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

        /// <summary>
        /// Converts FAIConfig Rectangle2 params (center+half-lengths+phi) to RoiDefinition bounding box.
        /// NOTE on D-05 compatibility: ROI_Phi exists in legacy INI data from Rectangle2 era.
        /// ToRoiDefinition() uses sin/cos of ROI_Phi for backward compatibility with existing INI files.
        /// New ROI input via the Rect ROI button (Plan 02) always sets ROI_Phi=0.0 (Rectangle1 only),
        /// so D-05 "Rectangle2는 사용하지 않는다" is honored for all new user input.
        /// </summary>
        public RoiDefinition ToRoiDefinition()
        {
            bool isTaught = ROI_Length1 > 0 && ROI_Length2 > 0;
            if (!isTaught)
            {
                return new RoiDefinition
                {
                    Id = FAIName ?? "FAI",
                    Name = FAIName ?? "FAI",
                    IsTaught = false
                };
            }

            double sinPhi = Math.Sin(ROI_Phi);
            double cosPhi = Math.Cos(ROI_Phi);
            double dRow = Math.Abs(ROI_Length1 * cosPhi) + Math.Abs(ROI_Length2 * sinPhi);
            double dCol = Math.Abs(ROI_Length1 * sinPhi) + Math.Abs(ROI_Length2 * cosPhi);

            return new RoiDefinition
            {
                Id = FAIName ?? "FAI",
                Name = FAIName ?? "FAI",
                Row1 = ROI_Row - dRow,
                Column1 = ROI_Col - dCol,
                Row2 = ROI_Row + dRow,
                Column2 = ROI_Col + dCol,
                IsTaught = true,
                Sigma = Sigma,
                EdgeThreshold = (int)Threshold,
                EdgeDirection = "LtoR",
                PixelResolutionX = PixelResolutionX,
                PixelResolutionY = PixelResolutionY
            };
        }
    }
}
