using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoGenerator.Views.Models
{
    public partial class StructureMapping : ObservableObject
    {
        private string _keyword;
        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        private string _targetName;
        public string TargetName
        {
            get => _targetName;
            set => SetProperty(ref _targetName, value);
        }
    }
}
