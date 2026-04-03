using System.Collections.ObjectModel;
using System.Windows;
using ReringProject.Sequence;

namespace ReringProject.UI
{
    /// <summary>Root ViewModel for Shot/FAI inspection view. Coordinates TreeView, canvas, and result table.</summary>
    public class InspectionViewModel : Observable
    {
        private readonly InspectionRecipeManager _recipeManager;

        private object _selectedNode;
        private ObservableCollection<FAIResultRow> _selectedShotFAIResults;
        private ShotNodeViewModel _selectedShot;

        public ObservableCollection<ShotNodeViewModel> Shots { get; }

        public object SelectedNode
        {
            get { return _selectedNode; }
            set
            {
                _selectedNode = value;
                RaisePropertyChanged("SelectedNode");
                OnSelectionChanged();
            }
        }

        public ObservableCollection<FAIResultRow> SelectedShotFAIResults
        {
            get { return _selectedShotFAIResults; }
            set
            {
                _selectedShotFAIResults = value;
                RaisePropertyChanged("SelectedShotFAIResults");
            }
        }

        public ShotNodeViewModel SelectedShot
        {
            get { return _selectedShot; }
            private set
            {
                _selectedShot = value;
                RaisePropertyChanged("SelectedShot");
            }
        }

        public bool CanRemove => SelectedNode != null;

        public bool CanRename => SelectedNode != null;

        public InspectionViewModel(InspectionRecipeManager recipeManager)
        {
            _recipeManager = recipeManager;
            Shots = new ObservableCollection<ShotNodeViewModel>();
            foreach (var shot in _recipeManager.Shots)
            {
                Shots.Add(new ShotNodeViewModel(shot));
            }
            _selectedShotFAIResults = new ObservableCollection<FAIResultRow>();
        }

        private void OnSelectionChanged()
        {
            if (_selectedNode is ShotNodeViewModel shotVm)
            {
                SelectedShot = shotVm;
                RebuildFAIResults(shotVm);
            }
            else if (_selectedNode is FAINodeViewModel faiVm)
            {
                ShotNodeViewModel parent = FindParentShot(faiVm);
                SelectedShot = parent;
                if (parent != null)
                {
                    RebuildFAIResults(parent);
                }
            }
            else
            {
                SelectedShot = null;
                SelectedShotFAIResults = new ObservableCollection<FAIResultRow>();
            }

            RaisePropertyChanged("CanRemove");
            RaisePropertyChanged("CanRename");
        }

        private void RebuildFAIResults(ShotNodeViewModel shotVm)
        {
            var rows = new ObservableCollection<FAIResultRow>();
            foreach (var fai in shotVm.ShotConfig.FAIList)
            {
                rows.Add(new FAIResultRow(fai));
            }
            SelectedShotFAIResults = rows;
        }

        private ShotNodeViewModel FindParentShot(FAINodeViewModel faiVm)
        {
            foreach (var shotVm in Shots)
            {
                foreach (var item in shotVm.FAIItems)
                {
                    if (item == faiVm)
                    {
                        return shotVm;
                    }
                }
            }
            return null;
        }

        public void AddShot()
        {
            string defaultName = "SHOT_" + _recipeManager.Shots.Count;
            if (!TextInputBox.Show("Shot \uc774\ub984 \uc785\ub825", defaultName, out string name))
            {
                return;
            }
            ShotConfig newShot = _recipeManager.AddShot(name);
            Shots.Add(new ShotNodeViewModel(newShot));
        }

        public void AddFAIToSelectedShot()
        {
            ShotNodeViewModel targetShotVm = null;

            if (_selectedNode is ShotNodeViewModel shotVm)
            {
                targetShotVm = shotVm;
            }
            else if (_selectedNode is FAINodeViewModel faiVm)
            {
                targetShotVm = FindParentShot(faiVm);
            }

            if (targetShotVm == null)
            {
                return;
            }

            string defaultName = "FAI_" + targetShotVm.ShotConfig.FAIList.Count;
            if (!TextInputBox.Show("FAI \uc774\ub984 \uc785\ub825", defaultName, out string name))
            {
                return;
            }

            FAIConfig newFai = targetShotVm.ShotConfig.AddFAI(name);
            targetShotVm.FAIItems.Add(new FAINodeViewModel(newFai));

            if (SelectedShot == targetShotVm)
            {
                RebuildFAIResults(targetShotVm);
            }
        }

        public void RemoveSelected()
        {
            if (_selectedNode is ShotNodeViewModel shotVm)
            {
                MessageBoxResult result = CustomMessageBox.ShowConfirmation(
                    "Shot \uc0ad\uc81c",
                    $"Shot \uc0ad\uc81c: \"{shotVm.Name}\" Shot\uacfc \ud558\uc704 FAI\ub97c \ubaa8\ub450 \uc0ad\uc81c\ud569\ub2c8\ub2e4. \uacc4\uc18d\ud558\uc2dc\uaca0\uc2b5\ub2c8\uae4c?",
                    MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                int index = Shots.IndexOf(shotVm);
                if (index >= 0)
                {
                    _recipeManager.RemoveShot(index);
                    Shots.RemoveAt(index);
                }
                SelectedNode = null;
            }
            else if (_selectedNode is FAINodeViewModel faiVm)
            {
                ShotNodeViewModel parentShot = FindParentShot(faiVm);
                if (parentShot == null)
                {
                    return;
                }

                MessageBoxResult result = CustomMessageBox.ShowConfirmation(
                    "FAI \uc0ad\uc81c",
                    $"FAI \uc0ad\uc81c: \"{faiVm.Name}\" FAI\ub97c \uc0ad\uc81c\ud569\ub2c8\ub2e4. \uacc4\uc18d\ud558\uc2dc\uaca0\uc2b5\ub2c8\uae4c?",
                    MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                int index = parentShot.FAIItems.IndexOf(faiVm);
                if (index >= 0)
                {
                    parentShot.ShotConfig.RemoveFAI(index);
                    parentShot.FAIItems.RemoveAt(index);
                }
                SelectedNode = null;
            }
        }

        public void RenameSelected()
        {
            if (_selectedNode is ShotNodeViewModel shotVm)
            {
                if (!TextInputBox.Show("Shot \uc774\ub984 \uc218\uc815", shotVm.Name, out string newName))
                {
                    return;
                }
                shotVm.Name = newName;
            }
            else if (_selectedNode is FAINodeViewModel faiVm)
            {
                if (!TextInputBox.Show("FAI \uc774\ub984 \uc218\uc815", faiVm.Name, out string newName))
                {
                    return;
                }
                faiVm.Name = newName;
            }
        }
    }
}
