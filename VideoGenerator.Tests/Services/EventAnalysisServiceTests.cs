using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class EventAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeBuildsEventModelFromDiscoveredFolderData()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.Analysis.").FullName;

        try
        {
            string eventFolder = Path.Combine(root, "Play_vo_Aatrox_Kill3DGeneral");
            string familyFolder = Path.Combine(eventFolder, "[Voice] Main");
            Directory.CreateDirectory(familyFolder);
            File.WriteAllBytes(Path.Combine(eventFolder, "direct.ogg"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(familyFolder, "family.ogg"), new byte[] { 2 });

            var parser = new StubEventNameParser(new ParsedEvent
            {
                DisplayText = "Kill in General",
                IconType = "generic"
            });
            var dialogues = new InMemoryDialogueStore();
            dialogues.SetDialogue("EN", "Play_vo_Aatrox_Kill3DGeneral", "Stored dialogue");
            var statuses = new List<string>();
            var progress = new List<double>();
            var logger = new LogService();
            var service = new EventAnalysisService(
                new AudioFolderDiscoveryService(),
                parser,
                new VideoService(logger, Path.Combine(root, "cache")),
                dialogues,
                logger,
                new AppSettings());

            IReadOnlyList<PreviewEventModel> result = await service.AnalyzeAsync(
                root,
                "EN",
                statuses.Add,
                progress.Add,
                CancellationToken.None);

            var analyzedEvent = Assert.Single(result);
            Assert.Equal("Play_vo_Aatrox_Kill3DGeneral", analyzedEvent.FolderName);
            Assert.Equal("Aatrox", analyzedEvent.CharacterName);
            Assert.Equal("Stored dialogue", analyzedEvent.Dialogue);
            Assert.Equal("Stored dialogue", analyzedEvent.ParsedData.Dialogue);
            Assert.Equal("Ready", analyzedEvent.Status);
            Assert.Single(analyzedEvent.DirectAudioFiles);
            Assert.Single(analyzedEvent.AudioFamilies);
            Assert.Equal(2, analyzedEvent.AudioFiles.Count);
            Assert.False(analyzedEvent.AreAudioFamiliesMerged);
            Assert.Equal("EN", Assert.Single(parser.Calls).Language);
            Assert.Contains(statuses, status => status.Contains(analyzedEvent.FolderName, StringComparison.Ordinal));
            Assert.Equal(100d, progress.Last());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AnalyzeHonorsCancellationBeforeProcessingFolders()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.Analysis.").FullName;

        try
        {
            var logger = new LogService();
            var service = new EventAnalysisService(
                new AudioFolderDiscoveryService(),
                new StubEventNameParser(new ParsedEvent { DisplayText = "Unused", IconType = "generic" }),
                new VideoService(logger, Path.Combine(root, "cache")),
                new InMemoryDialogueStore(),
                logger,
                new AppSettings());
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AnalyzeAsync(
                root,
                "EN",
                _ => { },
                _ => { },
                cancellation.Token));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class StubEventNameParser : IEventNameParser
    {
        private readonly ParsedEvent _parsedEvent;

        public StubEventNameParser(ParsedEvent parsedEvent)
        {
            _parsedEvent = parsedEvent;
        }

        public List<(string FolderName, string Language)> Calls { get; } = new();

        public Task<ParsedEvent> ParseFolderNameAsync(string folderName, string language)
        {
            Calls.Add((folderName, language));
            return Task.FromResult(_parsedEvent);
        }
    }

    private sealed class InMemoryDialogueStore : IDialogueStore
    {
        private readonly Dictionary<string, string> _dialogues = new(StringComparer.OrdinalIgnoreCase);

        public string GetDialogue(string language, string folderName)
        {
            return _dialogues.TryGetValue(BuildKey(language, folderName), out string? dialogue)
                ? dialogue ?? string.Empty
                : string.Empty;
        }

        public void SetDialogue(string language, string folderName, string text)
        {
            _dialogues[BuildKey(language, folderName)] = text;
        }

        private static string BuildKey(string language, string folderName) => $"{language}|{folderName}";
    }
}
