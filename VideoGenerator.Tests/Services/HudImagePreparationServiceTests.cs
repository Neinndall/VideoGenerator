using System.Collections.Generic;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class HudImagePreparationServiceTests
{
    [Theory]
    [InlineData(2, "First || Second", 2)]
    [InlineData(2, "Single dialogue", 1)]
    [InlineData(1, "First || Second", 1)]
    [InlineData(0, "", 1)]
    public void RequiredImageCountMatchesAudioAndDialogueSegmentation(
        int audioCount,
        string dialogue,
        int expectedImageCount)
    {
        var pipelineEvent = new PreviewEventModel
        {
            AudioFiles = CreateAudioFiles(audioCount)
        };

        int imageCount = HudImagePreparationService.GetRequiredImageCount(pipelineEvent, dialogue);

        Assert.Equal(expectedImageCount, imageCount);
    }

    [Fact]
    public async Task PrepareAsync_OverwritesExistingImages_WhenReuseExistingImagesIsFalse()
    {
        string characterName = "TestChampion_" + System.Guid.NewGuid().ToString("N");
        string eventName = "TestEvent_" + System.Guid.NewGuid().ToString("N");
        string charDir = System.IO.Path.Combine(VideoGenerator.Models.AppConfig.OutputImagesDir, characterName);
        string expectedImagePath = System.IO.Path.Combine(charDir, $"{eventName}.png");

        try
        {
            VideoGenerator.Utils.DirectoriesCreator.CreateDirectory(charDir);
            await System.IO.File.WriteAllBytesAsync(expectedImagePath, new byte[] { 1, 2, 3, 4 });

            var pipelineEvent = new PreviewEventModel
            {
                FolderName = eventName,
                CharacterName = characterName,
                Dialogue = "Sample dialogue",
                AudioFiles = new List<string> { "sample.ogg" },
                ParsedData = new VideoGenerator.Models.ParsedEvent
                {
                    OriginalFolder = eventName,
                    DisplayText = "Test Display",
                    Dialogue = "Sample dialogue",
                    IconType = "generic"
                }
            };
            pipelineEvent.MarkImagesReady(); // Even when marked ready

            var service = new HudImagePreparationService(
                new ImageGenerator(new LogService()),
                new AppSettings());

            var paths = await service.PrepareAsync(
                pipelineEvent,
                pipelineEvent.Dialogue,
                reuseExistingImages: false);

            Assert.Single(paths);
            Assert.Equal(expectedImagePath, paths[0]);
            Assert.True(System.IO.File.Exists(expectedImagePath));
            byte[] newBytes = await System.IO.File.ReadAllBytesAsync(expectedImagePath);
            // Must have been overwritten with real PNG bytes (not the 4-byte dummy file)
            Assert.True(newBytes.Length > 4);
        }
        finally
        {
            try
            {
                if (System.IO.Directory.Exists(charDir))
                {
                    System.IO.Directory.Delete(charDir, true);
                }
            }
            catch { }
        }
    }

    private static List<string> CreateAudioFiles(int count)
    {
        var audioFiles = new List<string>();
        for (int index = 0; index < count; index++)
        {
            audioFiles.Add($"part_{index}.ogg");
        }

        return audioFiles;
    }
}
