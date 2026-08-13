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

    [Fact]
    public void StartingANonBudgetedPhaseClearsThePreviousWorkBudget()
    {
        var progress = new ProgressService();
        progress.StartWork("Previous operation", 4);
        progress.Advance(2);

        progress.Start("Analyzing folders", indeterminate: false);
        progress.Report(25, "Analyzing folder");

        Assert.Equal(25d, progress.Value);
        Assert.Equal("ANALYZING FOLDER", progress.StatusText);
    }

    [Fact]
    public async Task PreviousCompletionCannotResetANewerOperation()
    {
        var progress = new ProgressService();
        progress.StartWork("First operation", 1);
        progress.FinishWork("First complete");

        Task previousCompletion = progress.CompleteAsync();
        progress.StartWork("Second operation", 2);

        await previousCompletion;

        Assert.True(progress.IsBusy);
        Assert.Equal(0d, progress.Value);
        Assert.Equal("[0/2] SECOND OPERATION", progress.StatusText);
    }
}
