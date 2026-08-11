using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class ProgressServiceTests
{
    [Fact]
    public void WorkProgressUsesTheConfiguredBudget()
    {
        var progress = new ProgressService();

        progress.StartWork("Preparing dialogues", 4);
        progress.Advance(1, "Preparing image");

        Assert.True(progress.IsBusy);
        Assert.False(progress.IsIndeterminate);
        Assert.Equal(25d, progress.Value);
        Assert.Equal("[1/4] PREPARING IMAGE", progress.StatusText);

        progress.FinishWork("Preparation complete");

        Assert.Equal(100d, progress.Value);
        Assert.Equal("[4/4] PREPARATION COMPLETE", progress.StatusText);
    }

    [Fact]
    public void CancelReturnsProgressToAnIdleCanceledState()
    {
        var progress = new ProgressService();
        progress.StartWork("Rendering videos", 2);
        progress.Advance(1);

        progress.Cancel();

        Assert.False(progress.IsBusy);
        Assert.True(progress.IsIndeterminate);
        Assert.Equal(0d, progress.Value);
        Assert.Equal("CANCELED - TASK ANNULLED", progress.StatusText);
    }
}
