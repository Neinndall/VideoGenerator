using Xunit;

using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Tests;

public sealed class DashboardModelTests
{
    [Fact]
    public void WorkflowIsDisabledUntilAnalysisAndSelectionAreAvailable()
    {
        var model = new DashboardModel();

        Assert.False(model.CanRunWorkflow);

        model.IsAnalyzed = true;
        Assert.False(model.CanRunWorkflow);

        model.FilteredProcessedEvents.Add(new PreviewEventModel());

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
        model.FilteredProcessedEvents.Add(new PreviewEventModel());

        Assert.False(model.CanRunWorkflow);

        model.IsProcessing = false;

        Assert.True(model.CanRunWorkflow);
    }

    [Fact]
    public void SelectionStateTracksVisibleEvents()
    {
        var model = new DashboardModel
        {
            IsAnalyzed = true
        };
        var selectedEvent = new PreviewEventModel();
        var unselectedEvent = new PreviewEventModel { IsSelected = false };

        model.FilteredProcessedEvents.Add(selectedEvent);
        model.FilteredProcessedEvents.Add(unselectedEvent);

        Assert.Equal(2, model.VisibleEventCount);
        Assert.Equal(1, model.SelectedVisibleEventCount);
        Assert.Equal("SELECTED: 1/2", model.SelectionSummary);
        Assert.True(model.CanRunWorkflow);

        selectedEvent.IsSelected = false;

        Assert.False(model.CanRunWorkflow);
        Assert.Equal("SELECT ALL VISIBLE", model.SelectionActionLabel);

        model.SetVisibleEventsSelection(true);

        Assert.Equal(2, model.SelectedVisibleEventCount);
        Assert.True(model.AreAllVisibleEventsSelected);
        Assert.Equal("DESELECT VISIBLE", model.SelectionActionLabel);
    }

    [Fact]
    public void WorkflowIsDisabledWhenOnlyUnmappedEventsAreSelected()
    {
        var model = new DashboardModel
        {
            IsAnalyzed = true
        };
        model.FilteredProcessedEvents.Add(new PreviewEventModel
        {
            Status = EventStatuses.NeedsMapping,
            ParsedData = new ParsedEvent { IsMapped = false }
        });

        Assert.False(model.CanRunWorkflow);
        Assert.Equal(1, model.SelectedVisibleEventCount);

        model.FilteredProcessedEvents[0].ParsedData.IsMapped = true;
        model.FilteredProcessedEvents[0].Status = EventStatuses.Ready;

        Assert.True(model.CanRunWorkflow);
        Assert.Single(model.GetSelectedVisibleWorkflowEvents());
    }
}
