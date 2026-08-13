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

    [Fact]
    public void PreparationPlanSkipsTranscriptionWhenDialogueAlreadyExists()
    {
        var settings = new AppSettings();
        var pipelineEvent = new PreviewEventModel
        {
            Status = "Ready",
            AudioFiles = new List<string> { "first.ogg", "second.ogg" },
            ParsedData = new ParsedEvent { IconType = "generic" },
            Dialogue = "Stored dialogue"
        };

        var plan = new ProductionWorkPlanningService(settings)
            .CreatePreparationPlan(new[] { pipelineEvent });

        Assert.Equal(0, plan.TranscriptionWork);
        Assert.Equal(1, plan.ImageWork);
        Assert.Equal(1, plan.TotalWork);
    }

    [Fact]
    public void PreparationPlanSkipsUnmappedEvents()
    {
        var settings = new AppSettings();
        var pipelineEvent = new PreviewEventModel
        {
            Status = EventStatuses.NeedsMapping,
            AudioFiles = new List<string> { "unknown.ogg" },
            ParsedData = new ParsedEvent { IsMapped = false, IconType = "generic" }
        };

        var plan = new ProductionWorkPlanningService(settings)
            .CreatePreparationPlan(new[] { pipelineEvent });

        Assert.Equal(0, plan.TranscriptionWork);
        Assert.Equal(0, plan.ImageWork);
        Assert.Equal(0, plan.TotalWork);
    }

    [Fact]
    public void RenderPlanAccountsForSegmentedAudioVideoWork()
    {
        var settings = new AppSettings();
        var pipelineEvent = new PreviewEventModel
        {
            Status = "Ready",
            AudioFiles = new List<string> { "first.ogg", "second.ogg" },
            ParsedData = new ParsedEvent { IconType = "generic" },
            Dialogue = "First segment || Second segment"
        };

        var plan = new ProductionWorkPlanningService(settings)
            .CreateRenderPlan(new[] { pipelineEvent });

        Assert.Equal(2, plan.ImageWork);
        Assert.Equal(3, plan.VideoWork);
        Assert.Equal(5, plan.TotalWork);
    }

    [Fact]
    public void RenderPlanSkipsEventsThatCannotBeRendered()
    {
        var settings = new AppSettings();
        var events = new[]
        {
            new PreviewEventModel
            {
                Status = "Missing Icon",
                AudioFiles = new List<string> { "missing-icon.ogg" },
                ParsedData = new ParsedEvent { IconType = "champion" }
            },
            new PreviewEventModel
            {
                Status = "No Audio",
                ParsedData = new ParsedEvent { IconType = "generic" }
            },
            new PreviewEventModel
            {
                Status = "Ready",
                AudioFiles = new List<string> { "missing-data.ogg" }
            }
        };

        var plan = new ProductionWorkPlanningService(settings)
            .CreateRenderPlan(events);

        Assert.Equal(0, plan.ImageWork);
        Assert.Equal(0, plan.VideoWork);
        Assert.Equal(0, plan.TotalWork);
    }
}
