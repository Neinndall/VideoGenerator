using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace VideoGenerator.Views.Models
{
    public partial class DashboardModel : ObservableObject
    {
        [ObservableProperty]
        private string _audioPath = string.Empty;

        [ObservableProperty]
        private string _selectedLanguage = "ES";

        [ObservableProperty]
        private string _selectedFontName = "Arial";

        [ObservableProperty]
        private double _silenceDuration = 0.0;

        [ObservableProperty]
        private bool _isProcessing = false;

        [ObservableProperty]
        private bool _isAnalyzed = false;

        [ObservableProperty]
        private PreviewEventModel _selectedEvent;

        [ObservableProperty]
        private string _previewImagePath;

        public ObservableCollection<string> FontNames { get; } = new();
        public ObservableCollection<string> AvailableLanguages { get; } = new();
        public ObservableCollection<PreviewEventModel> ProcessedEvents { get; } = new();
    }
}
