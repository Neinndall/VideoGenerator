using Xunit;

using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Tests;

public sealed class EventFilterServiceTests
{
    [Fact]
    public void FiltersByCharacterStatusAndSearchText()
    {
        var events = new[]
        {
            new PreviewEventModel
            {
                CharacterName = "Ahri",
                FolderName = "Play_vo_Ahri_Kill",
                Status = "Pending Icon",
                ParsedData = new ParsedEvent { DisplayText = "Kill Riven", IconType = "champion" }
            },
            new PreviewEventModel
            {
                CharacterName = "Ahri",
                FolderName = "Play_vo_Ahri_Joke",
                Status = "Ready",
                ParsedData = new ParsedEvent { DisplayText = "Joke", IconType = "generic" }
            },
            new PreviewEventModel
            {
                CharacterName = "Garen",
                FolderName = "Play_vo_Garen_Kill",
                Status = "Pending",
                ParsedData = new ParsedEvent { DisplayText = "Kill Ahri", IconType = "champion" }
            }
        };

        var result = new EventFilterService().FilterEvents(
            events,
            characterFilter: "Ahri",
            statusFilter: "PENDING",
            searchQuery: "kill");

        var selected = Assert.Single(result);
        Assert.Equal("Play_vo_Ahri_Kill", selected.FolderName);
    }

    [Fact]
    public void AllStatusFilterKeepsReadyAndPendingEvents()
    {
        var events = new[]
        {
            new PreviewEventModel { CharacterName = "Ahri", Status = "Ready" },
            new PreviewEventModel { CharacterName = "Ahri", Status = "Missing Icon" }
        };

        var result = new EventFilterService().FilterEvents(events, "ALL", "ALL", string.Empty);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ErrorFilterReturnsOnlyMissingIconAndNoAudioEvents()
    {
        var events = new[]
        {
            new PreviewEventModel { FolderName = "missing-icon", Status = "Missing Icon" },
            new PreviewEventModel { FolderName = "no-audio", Status = "No Audio" },
            new PreviewEventModel { FolderName = "pending", Status = "Pending" },
            new PreviewEventModel { FolderName = "ready", Status = "Ready" }
        };

        var result = new EventFilterService().FilterEvents(events, "ALL", "ERRORS", string.Empty);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, pipelineEvent => pipelineEvent.FolderName == "missing-icon");
        Assert.Contains(result, pipelineEvent => pipelineEvent.FolderName == "no-audio");
    }
}
