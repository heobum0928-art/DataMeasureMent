using ReringProject.Sequence;

namespace ReringProject.UI
{
    public class FAINodeViewModel : Observable
    {
        public FAIConfig FAIConfig { get; }

        public string Name
        {
            get { return FAIConfig.FAIName; }
            set
            {
                FAIConfig.FAIName = value;
                RaisePropertyChanged("Name");
            }
        }

        public FAINodeViewModel(FAIConfig fai)
        {
            FAIConfig = fai;
        }
    }
}
