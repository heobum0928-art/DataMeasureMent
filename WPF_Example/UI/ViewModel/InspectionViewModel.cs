using System.Collections.ObjectModel;
using ReringProject.Sequence;
using ReringProject.UI;

namespace ReringProject.UI
{
    /// <summary>FAI-centric ViewModel. Manages result table rows for the selected FAI or Action node.</summary>
    public class InspectionViewModel : Observable
    {
        private readonly InspectionRecipeManager _recipeManager;
        private ObservableCollection<FAIResultRow> _faiResults;

        public ObservableCollection<FAIResultRow> FAIResults
        {
            get { return _faiResults; }
            set { _faiResults = value; RaisePropertyChanged("FAIResults"); }
        }

        public InspectionViewModel(InspectionRecipeManager recipeManager)
        {
            _recipeManager = recipeManager;
            _faiResults = new ObservableCollection<FAIResultRow>();
        }

        /// <summary>Called when a FAI node is selected in InspectionListView. Updates result table with this FAI's row.</summary>
        public void OnFAISelected(FAIConfig fai)
        {
            var rows = new ObservableCollection<FAIResultRow>();
            if (fai != null)
            {
                rows.Add(new FAIResultRow(fai));
            }
            FAIResults = rows;
        }

        /// <summary>Called when an Action node is selected. Shows all FAIs under that action's ShotConfig.</summary>
        public void OnActionSelected(NodeViewModel actionNode)
        {
            var rows = new ObservableCollection<FAIResultRow>();
            if (actionNode != null && actionNode.Param is ShotConfig shot)
            {
                foreach (FAIConfig fai in shot.FAIList)
                {
                    rows.Add(new FAIResultRow(fai));
                }
            }
            FAIResults = rows;
        }

        public void ClearResults()
        {
            FAIResults = new ObservableCollection<FAIResultRow>();
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
