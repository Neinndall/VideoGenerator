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
        private double _silenceDuration = 0.0;

        [ObservableProperty]
        private bool _isProcessing = false;

        [ObservableProperty]
        private bool _isAnalyzed = false;

        [ObservableProperty]
        private PreviewEventModel _selectedEvent;

        [ObservableProperty]
        private string _previewImagePath;

        [ObservableProperty]
        private string _selectedFilter = "ALL"; // ALL, ERRORS, PENDING

        [ObservableProperty]
        private string _selectedCharacter = "ALL"; // ALL, Ahri, Lucian, etc.

        public ObservableCollection<string> CharactersList { get; } = new();
        public ObservableCollection<PreviewEventModel> FilteredProcessedEvents { get; } = new();

        public ObservableCollection<string> AvailableLanguages { get; } = new();
        public ObservableCollection<PreviewEventModel> ProcessedEvents { get; } = new();
    }
}
