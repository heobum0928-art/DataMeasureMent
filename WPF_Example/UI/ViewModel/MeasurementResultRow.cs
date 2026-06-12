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

        public string FAIName { get { return _faiName; } }

        // Measurement 이름 (없으면 TypeName)
        public string MeasurementName
        {
            get { return string.IsNullOrEmpty(_measurement.MeasurementName) ? _measurement.TypeName : _measurement.MeasurementName; }
        }

        public string TypeName { get { return _measurement.TypeName; } }

        // 참조 Datum 이름 (빈 문자열=무보정)
        public string DatumRef { get { return _measurement.DatumRef ?? ""; } }

        public double NominalValue { get { return _measurement.NominalValue; } }

        public double TolerancePlus { get { return _measurement.TolerancePlus; } }

        public double ToleranceMinus { get { return _measurement.ToleranceMinus; } }

        public double MeasuredValue { get { return _measurement.LastMeasuredValue; } }

        public bool IsPass { get { return _measurement.LastJudgement; } }

        // LastHasResult 플래그 기반 — 0.0 측정값도 정상 결과로 표시.
        // LastMeasuredValue != 0 검사는 resultValue = 0.0 인 에지(Datum 기준선 위 측정점)를
        // 미측정으로 오판하여 '—' 를 표시하는 거짓음성(false negative) 유발.
        public bool HasResult { get { return _measurement.LastHasResult; } }

        // 알고리즘 타입별 단위 포맷 (각도=deg, 그 외=mm)
        public string ResultDisplay
        {
            get
            {
                if (!HasResult) return "—";
                string unit = string.Equals(_measurement.TypeName, "LineToLineAngle", StringComparison.Ordinal) ? "deg" : "mm";
                return MeasuredValue.ToString("F3") + " " + unit;
            }
        }

        public string JudgeText { get { return HasResult ? (IsPass ? "OK" : "NG") : "—"; } }

        public string MeasuredValueText { get { return HasResult ? MeasuredValue.ToString("F3") : "—"; } }

        public string SpecMinText { get { return (NominalValue + ToleranceMinus).ToString("F3"); } }

        public string SpecMaxText { get { return (NominalValue + TolerancePlus).ToString("F3"); } }

        public void Refresh()
        {
            RaisePropertyChanged("MeasuredValue");
            RaisePropertyChanged("IsPass");
            RaisePropertyChanged("HasResult");
            RaisePropertyChanged("ResultDisplay");
            RaisePropertyChanged("JudgeText");
            RaisePropertyChanged("MeasuredValueText");
        }

        // 외부에서 ROI 등 접근용
        [System.ComponentModel.Browsable(false)]
        public MeasurementBase SourceMeasurement { get { return _measurement; } }
    }
}
