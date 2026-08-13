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

        public List<string> DirectAudioFiles { get; set; } = new();

        private List<AudioFamilyModel> _audioFamilies = new();
        public List<AudioFamilyModel> AudioFamilies
        {
            get => _audioFamilies;
            set => SetProperty(ref _audioFamilies, value);
        }

        private bool _areAudioFamiliesMerged;
        public bool AreAudioFamiliesMerged
        {
            get => _areAudioFamiliesMerged;
            set => SetProperty(ref _areAudioFamiliesMerged, value);
        }

        private string _status; // "Ready", "Needs Mapping", "Missing Icon", "No Audio", "Pending", "Pending Icon"
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool RequiresMapping => ParsedData != null && !ParsedData.IsMapped;

        public bool NeedsMapping => RequiresMapping || Status == EventStatuses.NeedsMapping;

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private string _characterName;
        public string CharacterName
        {
            get => _characterName;
            set => SetProperty(ref _characterName, value);
        }

        private string _dialogue;
        public string Dialogue
        {
            get => _dialogue;
            set => SetProperty(ref _dialogue, value);
        }

        private bool _imagesNeedRegeneration = true;
        public bool ImagesNeedRegeneration
        {
            get => _imagesNeedRegeneration;
            private set => SetProperty(ref _imagesNeedRegeneration, value);
        }

        public void MarkImagesDirty()
        {
            ImagesNeedRegeneration = true;
        }

        public void MarkImagesReady()
        {
            ImagesNeedRegeneration = false;
        }

        public void UpdateStatusAfterIconResolution(string iconPath)
        {
            if (Status == EventStatuses.NoAudio)
            {
                return;
            }

            if (NeedsMapping)
            {
                Status = EventStatuses.NeedsMapping;
                return;
            }

            Status = string.IsNullOrEmpty(iconPath) && ParsedData?.IconType != "generic"
                ? EventStatuses.MissingIcon
                : EventStatuses.Ready;
        }

        public void MarkReadyAfterProcessing()
        {
            if (Status == EventStatuses.NoAudio || Status == EventStatuses.MissingIcon)
            {
                return;
            }

            Status = NeedsMapping ? EventStatuses.NeedsMapping : EventStatuses.Ready;
        }
    }

    public static class EventStatuses
    {
        public const string Ready = "Ready";
        public const string NeedsMapping = "Needs Mapping";
        public const string MissingIcon = "Missing Icon";
        public const string NoAudio = "No Audio";
        public const string Pending = "Pending";
        public const string PendingIcon = "Pending Icon";
    }

    public class AudioFamilyModel
    {
        public string Name { get; set; } = string.Empty;
        public List<string> AudioFiles { get; set; } = new();
    }
}
