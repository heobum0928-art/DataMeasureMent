using System.Collections.ObjectModel;
using ReringProject.Sequence;
using ReringProject.UI;

namespace ReringProject.UI
{
    /// <summary>Measurement-centric ViewModel. Manages result table rows for the selected FAI or Action node.</summary>
    public class InspectionViewModel : Observable
    {
        private readonly InspectionRecipeManager _recipeManager;

        private ObservableCollection<MeasurementResultRow> _measurementResults;

        public ObservableCollection<MeasurementResultRow> MeasurementResults
        {
            get { return _measurementResults; }
            set { _measurementResults = value; RaisePropertyChanged("MeasurementResults"); }
        }

        public InspectionViewModel(InspectionRecipeManager recipeManager)
        {
            _recipeManager = recipeManager;
            _measurementResults = new ObservableCollection<MeasurementResultRow>();
        }

        /// <summary>Called when a FAI node is selected. Shows all Measurements under this FAI as rows.</summary>
        public void OnFAISelected(FAIConfig fai)
        {
            var rows = new ObservableCollection<MeasurementResultRow>();
            if (fai != null)
            {
                string faiName = fai.FAIName;
                if (faiName == null) faiName = "FAI";
                foreach (MeasurementBase meas in fai.Measurements)
                {
                    rows.Add(new MeasurementResultRow(meas, faiName));
                }
            }
            MeasurementResults = rows;
        }

        /// <summary>Called when an Action node is selected. Shows all Measurements across all FAIs under that ShotConfig.</summary>
        public void OnActionSelected(NodeViewModel actionNode)
        {
            var rows = new ObservableCollection<MeasurementResultRow>();
            if (actionNode != null && actionNode.Param is ShotConfig shot)
            {
                foreach (FAIConfig fai in shot.FAIList)
                {
                    string faiName = fai.FAIName;
                    if (faiName == null) faiName = "FAI";
                    foreach (MeasurementBase meas in fai.Measurements)
                    {
                        rows.Add(new MeasurementResultRow(meas, faiName));
                    }
                }
            }
            MeasurementResults = rows;
        }

        //260617 hbk Quick 260617-cq2: 일괄 검사 후 체크된 SHOT 들의 모든 측정을 그리드에 펼쳐 표시.
        //  OnActionSelected 의 단일-shot 평탄화를 다중 shot 으로 확장. 행은 live MeasurementBase 를
        //  감싸므로 검사 후 LastMeasuredValue 가 이미 채워진 상태. FAIName 은 순수 유지(동일-명 FAI 는
        //  MainView 가 SourceMeasurement ReferenceEquals 로 구분 — ROI 하이라이트 회귀 0).
        public void ShowMeasurementsForShots(System.Collections.Generic.List<ShotConfig> shots)
        {
            var rows = new ObservableCollection<MeasurementResultRow>();
            if (shots != null)
            {
                foreach (ShotConfig shot in shots)
                {
                    if (shot == null) continue;
                    foreach (FAIConfig fai in shot.FAIList)
                    {
                        string faiName = fai.FAIName;
                        if (faiName == null) faiName = "FAI";
                        foreach (MeasurementBase meas in fai.Measurements)
                        {
                            rows.Add(new MeasurementResultRow(meas, faiName));
                        }
                    }
                }
            }
            MeasurementResults = rows;
        }

        public void ClearResults()
        {
            MeasurementResults = new ObservableCollection<MeasurementResultRow>();
        }

        public void RefreshResults()
        {
            if (_measurementResults == null) return;
            foreach (var row in _measurementResults)
            {
                row.Refresh();
            }
        }

        /// <summary>Adds a new FAI to the given ShotConfig.</summary>
        public FAIConfig AddFAI(ShotConfig shot, string name)
        {
            if (shot == null) return null;
            return shot.AddFAI(name);
        }

        /// <summary>Removes a FAI by index from the given ShotConfig.</summary>
        public bool RemoveFAI(ShotConfig shot, int index)
        {
            if (shot == null) return false;
            return shot.RemoveFAI(index);
        }
    }
}
