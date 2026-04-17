//260417 hbk Phase 6 Plan 04: MeasurementBase 래핑 — DataGrid 결과 행 ViewModel (D-21)
using System;
using ReringProject.Sequence;

namespace ReringProject.UI
{
    /// <summary>DataGrid row DTO wrapping MeasurementBase for Measurement-unit result display.</summary>
    public class MeasurementResultRow : Observable
    {
        private readonly MeasurementBase _measurement;
        private readonly string _faiName;

        public MeasurementResultRow(MeasurementBase measurement, string faiName)
        {
            _measurement = measurement;
            _faiName = faiName ?? "";
        }

        //260417 hbk Phase 6 Plan 04: 소속 FAI 이름
        public string FAIName { get { return _faiName; } }

        //260417 hbk Phase 6 Plan 04: Measurement 이름 (없으면 TypeName)
        public string MeasurementName
        {
            get { return string.IsNullOrEmpty(_measurement.MeasurementName) ? _measurement.TypeName : _measurement.MeasurementName; }
        }

        //260417 hbk Phase 6 Plan 04: 알고리즘 종류 표시
        public string TypeName { get { return _measurement.TypeName; } }

        //260417 hbk Phase 6 Plan 04: 참조 Datum 이름 (빈 문자열=무보정)
        public string DatumRef { get { return _measurement.DatumRef ?? ""; } }

        public double NominalValue { get { return _measurement.NominalValue; } }

        public double TolerancePlus { get { return _measurement.TolerancePlus; } }

        public double ToleranceMinus { get { return _measurement.ToleranceMinus; } }

        public double MeasuredValue { get { return _measurement.LastMeasuredValue; } }

        public bool IsPass { get { return _measurement.LastJudgement; } }

        //260417 hbk Phase 6 Plan 04: FAIResultRow.HasResult 패턴 — 0이면 미측정
        public bool HasResult { get { return _measurement.LastMeasuredValue != 0; } }

        //260417 hbk Phase 6 Plan 04: 알고리즘 타입별 단위 포맷 (각도=deg, 그 외=mm)
        public string ResultDisplay
        {
            get
            {
                if (!HasResult) return "\u2014";
                string unit = string.Equals(_measurement.TypeName, "LineToLineAngle", StringComparison.Ordinal) ? "deg" : "mm";
                return MeasuredValue.ToString("F3") + " " + unit;
            }
        }

        public string JudgeText { get { return HasResult ? (IsPass ? "OK" : "NG") : "\u2014"; } }

        public string MeasuredValueText { get { return HasResult ? MeasuredValue.ToString("F3") : "\u2014"; } }

        public string SpecMinText { get { return (NominalValue + ToleranceMinus).ToString("F3"); } }

        public string SpecMaxText { get { return (NominalValue + TolerancePlus).ToString("F3"); } }

        //260417 hbk Phase 6 Plan 04: 측정 후 PropertyChanged 일괄 발생
        public void Refresh()
        {
            RaisePropertyChanged("MeasuredValue");
            RaisePropertyChanged("IsPass");
            RaisePropertyChanged("HasResult");
            RaisePropertyChanged("ResultDisplay");
            RaisePropertyChanged("JudgeText");
            RaisePropertyChanged("MeasuredValueText");
        }

        //260417 hbk Phase 6 Plan 04: 외부에서 ROI 등 접근용
        [System.ComponentModel.Browsable(false)]
        public MeasurementBase SourceMeasurement { get { return _measurement; } }
    }
}
