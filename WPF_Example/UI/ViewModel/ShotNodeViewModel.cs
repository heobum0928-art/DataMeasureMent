using System.Collections.ObjectModel;
using ReringProject.Sequence;

namespace ReringProject.UI
{
    public class ShotNodeViewModel : Observable
    {
        public ShotConfig ShotConfig { get; }

        public string Name
        {
            get { return ShotConfig.ShotName; }
            set
            {
                ShotConfig.ShotName = value;
                RaisePropertyChanged("Name");
            }
        }

        public ObservableCollection<FAINodeViewModel> FAIItems { get; }

        public ShotNodeViewModel(ShotConfig shot)
        {
            ShotConfig = shot;
            FAIItems = new ObservableCollection<FAINodeViewModel>();
            foreach (var fai in shot.FAIList)
            {
                FAIItems.Add(new FAINodeViewModel(fai));
            }
        }
    }
}
