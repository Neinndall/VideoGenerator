using VideoGenerator.Models;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class PreviewEventModelTests
{
    [Fact]
    public void NewEventRequiresImageRegeneration()
    {
        var model = new PreviewEventModel();

        Assert.True(model.ImagesNeedRegeneration);
        Assert.True(model.IsSelected);

        model.MarkImagesReady();
        Assert.False(model.ImagesNeedRegeneration);

        model.MarkImagesDirty();
        Assert.True(model.ImagesNeedRegeneration);
    }

    [Fact]
    public void MappingAndAudioStatusesSurviveWorkflowStateUpdates()
    {
        var unmapped = new PreviewEventModel
        {
            Status = EventStatuses.NeedsMapping,
            ParsedData = new ParsedEvent { IsMapped = false, IconType = "champion" }
        };

        unmapped.UpdateStatusAfterIconResolution("champion.png");
        unmapped.MarkReadyAfterProcessing();

        Assert.Equal(EventStatuses.NeedsMapping, unmapped.Status);

        var noAudio = new PreviewEventModel
        {
            Status = EventStatuses.NoAudio,
            ParsedData = new ParsedEvent { IconType = "champion" }
        };

        noAudio.UpdateStatusAfterIconResolution("champion.png");

        Assert.Equal(EventStatuses.NoAudio, noAudio.Status);
    }
}
