using Xunit;

using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Tests;

public sealed class ProductionWorkPlanningServiceTests
{
    [Fact]
    public void PreparationPlanAccountsForAudioTranscriptionAndSegmentedImages()
    {
        var settings = new AppSettings();
        var pipelineEvent = new PreviewEventModel
        {
            Status = "Ready",
            AudioFiles = new List<string> { "first.ogg", "second.ogg" },
            ParsedData = new ParsedEvent { IconType = "generic" },
            Dialogue = string.Empty
        };

        var plan = new ProductionWorkPlanningService(settings)
            .CreatePreparationPlan(new[] { pipelineEvent });

        Assert.Equal(2, plan.TranscriptionWork);
        Assert.Equal(2, plan.ImageWork);
        Assert.Equal(4, plan.TotalWork);
    }
}
