using System.Collections.ObjectModel;
using ReringProject.Sequence;
using ReringProject.UI;

namespace ReringProject.UI
{
    /// <summary>Measurement-centric ViewModel. Manages result table rows for the selected FAI or Action node.</summary>
    //260417 hbk Phase 6 Plan 04: FAIResults → MeasurementResults (D-21)
    public class InspectionViewModel : Observable
    {
        private readonly InspectionRecipeManager _recipeManager;

        //260417 hbk Phase 6 Plan 04: Measurement 단위 결과 컬렉션 (D-21)
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
        //260417 hbk Phase 6 Plan 04: FAI 하위 모든 Measurement 행 표시 (D-21, D-24)
        public void OnFAISelected(FAIConfig fai)
        {
            var rows = new ObservableCollection<MeasurementResultRow>();
            if (fai != null)
            {
                string faiName = fai.FAIName ?? "FAI";
                foreach (MeasurementBase meas in fai.Measurements)
                {
                    rows.Add(new MeasurementResultRow(meas, faiName));
                }
            }
            MeasurementResults = rows;
        }

        /// <summary>Called when an Action node is selected. Shows all Measurements across all FAIs under that ShotConfig.</summary>
        //260417 hbk Phase 6 Plan 04: ShotConfig.FAIList 순회 → 각 FAI의 Measurements 펼치기 (D-21)
        public void OnActionSelected(NodeViewModel actionNode)
        {
            var rows = new ObservableCollection<MeasurementResultRow>();
            if (actionNode != null && actionNode.Param is ShotConfig shot)
            {
                foreach (FAIConfig fai in shot.FAIList)
                {
                    string faiName = fai.FAIName ?? "FAI";
                    foreach (MeasurementBase meas in fai.Measurements)
                    {
                        rows.Add(new MeasurementResultRow(meas, faiName));
                    }
                }
            }
            MeasurementResults = rows;
        }

        public void ClearResults()
        {
            MeasurementResults = new ObservableCollection<MeasurementResultRow>();
        }

        //260417 hbk Phase 6 Plan 04: 측정 완료 후 모든 행 갱신 (D-21)
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
