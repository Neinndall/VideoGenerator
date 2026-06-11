using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoGenerator.Views.Models
{
    public partial class TranslationsModel : ObservableObject
    {
        [ObservableProperty]
        private string _jsonContent = "";

        [ObservableProperty]
        private string _statusMessage = "";
    }
}
