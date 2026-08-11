using Xunit;

using VideoGenerator.Views.Models;

namespace VideoGenerator.Tests;

public sealed class DashboardModelTests
{
    [Fact]
    public void WorkflowIsDisabledUntilAnalysisIsComplete()
    {
        var model = new DashboardModel();

        Assert.False(model.CanRunWorkflow);

        model.IsAnalyzed = true;

        Assert.True(model.CanRunWorkflow);
    }

    [Fact]
    public void WorkflowIsDisabledWhileProcessing()
    {
        var model = new DashboardModel
        {
            IsAnalyzed = true,
            IsProcessing = true
        };

        Assert.False(model.CanRunWorkflow);

        model.IsProcessing = false;

        Assert.True(model.CanRunWorkflow);
    }
}
