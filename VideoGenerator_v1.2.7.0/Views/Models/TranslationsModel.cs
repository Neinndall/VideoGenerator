using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace VideoGenerator.Views.Models
{
    public partial class TranslationEntry : ObservableObject
    {
        [ObservableProperty]
        private string _language;

        [ObservableProperty]
        private string _key;

        [ObservableProperty]
        private string _value;
    }

    public partial class TranslationsModel : ObservableObject
    {
        [ObservableProperty]
        private string _statusMessage = "";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _selectedLanguage = "ALL";

        [ObservableProperty]
        private string _searchQuery = "";

        public ObservableCollection<string> AvailableLanguages { get; } = new();
        public ObservableCollection<TranslationEntry> AllEntries { get; } = new();
        public ObservableCollection<TranslationEntry> FilteredEntries { get; } = new();
        public ObservableCollection<string> SuggestedEventKeys { get; } = new();
    }
}
