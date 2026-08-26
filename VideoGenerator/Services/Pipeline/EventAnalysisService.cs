using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Builds dashboard pipeline events from an audio source directory.
    /// UI collection updates and view state intentionally remain outside this service.
    /// </summary>
    public sealed class EventAnalysisService
    {
        private static readonly Regex ChampionNamePattern = new(
            @"Play_vo_([A-Za-z0-9]+)(Skin\d+)?_",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly AudioFolderDiscoveryService _folderDiscovery;
        private readonly IEventNameParser _nameParser;
        private readonly VideoService _videoService;
        private readonly IDialogueStore _dialogueService;
        private readonly LogService _logger;
        private readonly AppSettings _settings;

        public EventAnalysisService(
            AudioFolderDiscoveryService folderDiscovery,
            IEventNameParser nameParser,
            VideoService videoService,
            IDialogueStore dialogueService,
            LogService logger,
            AppSettings settings)
        {
            _folderDiscovery = folderDiscovery;
            _nameParser = nameParser;
            _videoService = videoService;
            _dialogueService = dialogueService;
            _logger = logger;
            _settings = settings;
        }

        public async Task<IReadOnlyList<PreviewEventModel>> AnalyzeAsync(
            string rootDirectory,
            string language,
            Action<string> reportStatus,
            Action<double> reportProgress,
            CancellationToken cancellationToken)
        {
            return await Task.Run(async () =>
            {
                var events = new List<PreviewEventModel>();
                var folders = _folderDiscovery.GetEventFolders(rootDirectory);
                int total = folders.Count;

                for (int index = 0; index < total; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string folderPath = folders[index];
                    string folderName = Path.GetFileName(folderPath);
                    reportStatus?.Invoke($"Analyzing: {folderName}");

                    try
                    {
                        PreviewEventModel pipelineEvent = await CreateEventAsync(
                            folderPath,
                            folderName,
                            language,
                            cancellationToken);
                        events.Add(pipelineEvent);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to process folder: {folderName} | Error: {ex.Message}");
                    }

                    reportProgress?.Invoke((double)(index + 1) / total * 100.0);
                }

                reportProgress?.Invoke(100.0);
                return (IReadOnlyList<PreviewEventModel>)events;
            }, cancellationToken);
        }

        private async Task<PreviewEventModel> CreateEventAsync(
            string folderPath,
            string folderName,
            string language,
            CancellationToken cancellationToken)
        {
            ParsedEvent parsedEvent = await _nameParser.ParseFolderNameAsync(folderName, language);
            cancellationToken.ThrowIfCancellationRequested();

            List<string> directAudioFiles = _folderDiscovery.GetSupportedAudioFiles(folderPath);
            List<AudioFamilyModel> audioFamilies = _folderDiscovery.GetAudioFamilies(folderPath);
            List<string> audioFiles = directAudioFiles
                .Concat(audioFamilies.SelectMany(family => family.AudioFiles))
                .ToList();

            bool familiesMerged = TryUseCachedMergedFamilies(
                directAudioFiles,
                audioFamilies,
                folderName,
                folderPath,
                out List<string> mergedAudioFiles);
            if (familiesMerged)
            {
                audioFiles = mergedAudioFiles;
            }

            string characterName = GetCharacterName(folderName);
            string dialogue = GetStoredDialogue(language, folderName);
            bool isDialogueValidated = _dialogueService.IsDialogueValidated(language, folderName);
            var eventData = parsedEvent ?? new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = folderName,
                IsMapped = false
            };
            eventData.Dialogue = dialogue;

            return new PreviewEventModel
            {
                CharacterName = characterName,
                FolderName = folderName,
                FolderPath = folderPath,
                ParsedData = eventData,
                AudioFiles = audioFiles,
                DirectAudioFiles = directAudioFiles,
                AudioFamilies = audioFamilies,
                AreAudioFamiliesMerged = familiesMerged,
                Status = GetInitialStatus(parsedEvent, folderName),
                Dialogue = dialogue,
                IsDialogueValidated = isDialogueValidated
            };
        }

        private bool TryUseCachedMergedFamilies(
            List<string> directAudioFiles,
            List<AudioFamilyModel> audioFamilies,
            string folderName,
            string folderPath,
            out List<string> audioFiles)
        {
            audioFiles = null;
            if (!_settings.MergeAudioFamilies || audioFamilies.Count == 0)
            {
                return false;
            }

            var cachedTracks = new List<string>(directAudioFiles);
            foreach (AudioFamilyModel family in audioFamilies)
            {
                string cachedPath = _videoService.GetMergedAudioFamilyPath(
                    family.AudioFiles,
                    folderName,
                    family.Name,
                    folderPath,
                    _settings.SilenceDuration);
                if (string.IsNullOrEmpty(cachedPath) || !File.Exists(cachedPath) || new FileInfo(cachedPath).Length == 0)
                {
                    return false;
                }

                cachedTracks.Add(cachedPath);
            }

            audioFiles = cachedTracks;
            return true;
        }

        private string GetStoredDialogue(string language, string folderName)
        {
            if (_settings.ForceBatchRetranscribe)
            {
                return string.Empty;
            }

            string dialogue = _dialogueService.GetDialogue(language, folderName);
            if (!_settings.CleanWhisperHallucinations || string.IsNullOrEmpty(dialogue))
            {
                return dialogue;
            }

            string cleanedDialogue = DialogueService.CleanDialogue(dialogue);
            if (cleanedDialogue != dialogue)
            {
                _dialogueService.SetDialogue(language, folderName, cleanedDialogue);
            }

            return cleanedDialogue;
        }

        private static string GetCharacterName(string folderName)
        {
            Match match = ChampionNamePattern.Match(folderName);
            return match.Success ? match.Groups[1].Value : "General";
        }

        private static string GetInitialStatus(ParsedEvent parsedEvent, string folderName)
        {
            if (parsedEvent == null || !parsedEvent.IsMapped)
            {
                return EventStatuses.NeedsMapping;
            }

            if (string.IsNullOrEmpty(parsedEvent.DisplayText) ||
                parsedEvent.DisplayText.Contains("event_") ||
                parsedEvent.DisplayText.Contains("interaction_") ||
                parsedEvent.DisplayText.Equals(folderName))
            {
                return EventStatuses.Pending;
            }

            if (parsedEvent.IconType == "generic")
            {
                return EventStatuses.Ready;
            }

            return !string.IsNullOrEmpty(parsedEvent.IconLookupName) ? EventStatuses.PendingIcon : EventStatuses.Ready;
        }
    }
}
