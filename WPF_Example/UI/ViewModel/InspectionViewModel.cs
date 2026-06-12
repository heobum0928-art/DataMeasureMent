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
                string faiName = fai.FAIName ?? "FAI";
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
