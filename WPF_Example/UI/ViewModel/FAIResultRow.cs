using System;
using ReringProject.Sequence;

namespace ReringProject.UI
{
    /// <summary>DataGrid row DTO wrapping FAIConfig for result display.</summary>
    public class FAIResultRow : Observable
    {
        private readonly FAIConfig _fai;

        public string FAIName => _fai.FAIName;

        //260408 hbk SourceFAI 프로퍼티 추가 (ROI 접근용)
        /// <summary>Underlying FAIConfig for ROI access (used by Plan 02 drawing/calibration).</summary>
        [System.ComponentModel.Browsable(false)]
        public FAIConfig SourceFAI => _fai;

        public double MeasuredValue => _fai.MeasuredValue;

        public double SpecMin => _fai.NominalValue - Math.Abs(_fai.LowerTolerance);

        public double SpecMax => _fai.NominalValue + Math.Abs(_fai.UpperTolerance);

        public bool IsPass => _fai.IsPass;

        public bool HasResult => _fai.MeasuredValue > 0;

        public string JudgeText => HasResult ? (IsPass ? "OK" : "NG") : "\u2014";

        public string MeasuredValueText => HasResult ? MeasuredValue.ToString("F3") : "\u2014";

        public string SpecMinText => SpecMin.ToString("F3");

        public string SpecMaxText => SpecMax.ToString("F3");

        public FAIResultRow(FAIConfig fai)
        {
            _fai = fai;
        }

        public void Refresh()
        {
            RaisePropertyChanged("MeasuredValue");
            RaisePropertyChanged("IsPass");
            RaisePropertyChanged("JudgeText");
            RaisePropertyChanged("MeasuredValueText");
            RaisePropertyChanged("HasResult");
        }
    }
}
