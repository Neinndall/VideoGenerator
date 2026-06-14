using CommunityToolkit.Mvvm.ComponentModel;
using VideoGenerator.Models;
using System.Collections.Generic;

namespace VideoGenerator.Views.Models
{
    public partial class PreviewEventModel : ObservableObject
    {
        private string _folderPath;
        public string FolderPath
        {
            get => _folderPath;
            set => SetProperty(ref _folderPath, value);
        }

        private string _folderName;
        public string FolderName
        {
            get => _folderName;
            set => SetProperty(ref _folderName, value);
        }

        private ParsedEvent _parsedData;
        public ParsedEvent ParsedData
        {
            get => _parsedData;
            set => SetProperty(ref _parsedData, value);
        }

        private List<string> _audioFiles = new();
        public List<string> AudioFiles
        {
            get => _audioFiles;
            set => SetProperty(ref _audioFiles, value);
        }

        private string _status; // "Ready", "Missing Icon", "No Audio", "Pending Translation"
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private string _characterName;
        public string CharacterName
        {
            get => _characterName;
            set => SetProperty(ref _characterName, value);
        }
    }
}
