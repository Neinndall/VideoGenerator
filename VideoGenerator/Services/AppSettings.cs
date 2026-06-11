using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoGenerator.Services
{
    public partial class AppSettings : ObservableObject
    {
        private static AppSettings _instance;
        public static AppSettings Instance => _instance ??= new AppSettings();

        [ObservableProperty]
        private string _customBackgroundPath;

        [ObservableProperty]
        private float _textVerticalOffset = -8f;

        private AppSettings() { }
    }
}
