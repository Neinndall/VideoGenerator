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
}
