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
