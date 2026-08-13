using System.Net.Http;
using System.Text;
using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ProductionPipelineIntegrationTests
{
    [Fact]
    public async Task AnalyzesPreparesAndRendersAnEventWithRealAssets()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.PipelineIntegration.").FullName;
        string eventName = $"Play_vo_Aatrox_CustomSpell3D_{Guid.NewGuid():N}";
        string eventDirectory = Path.Combine(root, eventName);
        var generatedImages = new List<string>();

        try
        {
            Directory.CreateDirectory(eventDirectory);
            WriteWave(Path.Combine(eventDirectory, "first.wav"), 440);
            WriteWave(Path.Combine(eventDirectory, "second.wav"), 660);

            var logger = new LogService();
            using var httpClient = new HttpClient();
            var dataFetcher = new DataFetcher(httpClient, logger, root);
            var translationService = new TranslationService(
                logger,
                Path.Combine(root, "translations.json"));
            var ruleManager = new RuleManager(logger, Path.Combine(root, "event_rules.json"));
            var groupManager = new GroupManager(logger, Path.Combine(root, "groups.json"));
            var aliasManager = new AliasManager(logger, Path.Combine(root, "champion_aliases.json"));
            var skinlineManager = new SkinlineManager(dataFetcher, aliasManager, logger);
            var nameParser = new EventNameParser(
                translationService,
                dataFetcher,
                ruleManager,
                groupManager,
                aliasManager,
                skinlineManager);
            var settings = new AppSettings();
            var videoService = new VideoService(logger, Path.Combine(root, "cache"));
            var dialogueStore = new InMemoryDialogueStore();
            dialogueStore.SetDialogue("EN", eventName, "First segment || Second segment");

            var analysisService = new EventAnalysisService(
                new AudioFolderDiscoveryService(),
                nameParser,
                videoService,
                dialogueStore,
                logger,
                settings);
            var statuses = new List<string>();
            var progress = new List<double>();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            IReadOnlyList<PreviewEventModel> analyzedEvents = await analysisService.AnalyzeAsync(
                root,
                "EN",
                statuses.Add,
                progress.Add,
                timeout.Token);

            PreviewEventModel pipelineEvent = Assert.Single(analyzedEvents);
            Assert.Equal(eventName, pipelineEvent.FolderName);
            Assert.Equal("Aatrox", pipelineEvent.CharacterName);
            Assert.Equal("First segment || Second segment", pipelineEvent.Dialogue);
            Assert.Equal(2, pipelineEvent.AudioFiles.Count);
            Assert.Equal("generic", pipelineEvent.ParsedData.IconType);
            Assert.Equal("Ready", pipelineEvent.Status);
            Assert.Equal(100d, progress.Last());
            Assert.Contains(statuses, status => status.Contains(eventName, StringComparison.Ordinal));

            var preparationService = new HudImagePreparationService(
                new ImageGenerator(logger),
                settings);
            IReadOnlyList<string> imagePaths = await preparationService.PrepareAsync(
                pipelineEvent,
                pipelineEvent.Dialogue,
                reuseExistingImages: false,
                timeout.Token);

            generatedImages.AddRange(imagePaths);
            Assert.Equal(2, imagePaths.Count);
            Assert.All(imagePaths, imagePath =>
            {
                Assert.True(File.Exists(imagePath));
                Assert.True(new FileInfo(imagePath).Length > 0);
            });
            Assert.False(pipelineEvent.ImagesNeedRegeneration);

            string outputPath = Path.Combine(root, "Generated", "Aatrox", eventName + ".mp4");
            bool rendered = await videoService.CreateVideoAsync(
                imagePaths.ToList(),
                pipelineEvent.AudioFiles,
                outputPath,
                silenceDuration: 0.05,
                dialogue: pipelineEvent.Dialogue,
                cancellationToken: timeout.Token);

            Assert.True(rendered);
            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
        }
        finally
        {
            foreach (string imagePath in generatedImages)
            {
                if (File.Exists(imagePath))
                {
                    File.Delete(imagePath);
                }
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void WriteWave(string path, double frequency)
    {
        const int sampleRate = 8000;
        const short channels = 1;
        const short bitsPerSample = 16;
        int sampleCount = sampleRate / 4;
        int dataSize = sampleCount * channels * (bitsPerSample / 8);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8));
        writer.Write((short)(channels * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        for (int sample = 0; sample < sampleCount; sample++)
        {
            double phase = 2 * Math.PI * frequency * sample / sampleRate;
            writer.Write((short)(Math.Sin(phase) * short.MaxValue * 0.15));
        }
    }

    private sealed class InMemoryDialogueStore : IDialogueStore
    {
        private readonly Dictionary<string, string> _dialogues = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _validations = new(StringComparer.OrdinalIgnoreCase);

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

        public bool IsDialogueValidated(string language, string folderName)
        {
            return _validations.TryGetValue(BuildKey(language, folderName), out bool isValidated) && isValidated;
        }

        public void SetDialogueValidation(string language, string folderName, bool isValidated)
        {
            _validations[BuildKey(language, folderName)] = isValidated;
        }

        private static string BuildKey(string language, string folderName) => $"{language}|{folderName}";
    }
}
