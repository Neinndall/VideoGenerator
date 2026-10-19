using System.Text.Json;
using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests.Services;

public sealed class EventManifestExportServiceTests
{
    [Fact]
    public void CreateJsonWritesVersionedSchemaWithStableEventOrder()
    {
        string sourceFolder = Path.Combine(Path.GetTempPath(), "VideoGenerator.ManifestTests", "Source");
        var zeta = MakeEvent(sourceFolder, "Zeta", "Zeta", "Ready");
        var alpha = MakeEvent(sourceFolder, "Alpha", "Ahri", "Needs Mapping");
        alpha.ParsedData.IsMapped = false;
        alpha.AudioFiles.Add("private-audio.ogg");
        alpha.AudioFamilies.Add(new AudioFamilyModel { Name = "First family" });

        string first = EventManifestExportService.CreateJson(sourceFolder, "ES", [zeta, alpha]);
        string reordered = EventManifestExportService.CreateJson(sourceFolder, "ES", [alpha, zeta]);

        Assert.Equal(first, reordered);
        Assert.Contains('\n', first);

        using var document = JsonDocument.Parse(first);
        JsonElement root = document.RootElement;
        Assert.Equal(["schemaVersion", "locale", "events"], root.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("ES", root.GetProperty("locale").GetString());

        JsonElement events = root.GetProperty("events");
        Assert.Equal(2, events.GetArrayLength());
        Assert.Equal("Alpha", events[0].GetProperty("folderName").GetString());
        Assert.Equal("Zeta", events[1].GetProperty("folderName").GetString());

        JsonElement firstEvent = events[0];
        Assert.Equal(
            ["folderName", "relativeFolder", "champion", "audioCount", "audioFamilyCount",
             "status", "isMapped", "displayText", "iconType", "iconLookupName"],
            firstEvent.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("Alpha", firstEvent.GetProperty("relativeFolder").GetString());
        Assert.Equal("Ahri", firstEvent.GetProperty("champion").GetString());
        Assert.Equal(1, firstEvent.GetProperty("audioCount").GetInt32());
        Assert.Equal(1, firstEvent.GetProperty("audioFamilyCount").GetInt32());
        Assert.Equal("Needs Mapping", firstEvent.GetProperty("status").GetString());
        Assert.False(firstEvent.GetProperty("isMapped").GetBoolean());
        Assert.Equal("Event Alpha", firstEvent.GetProperty("displayText").GetString());
        Assert.Equal("champion", firstEvent.GetProperty("iconType").GetString());
        Assert.Equal("Ahri", firstEvent.GetProperty("iconLookupName").GetString());
    }

    [Fact]
    public void CreateJsonExcludesAllPrivateAudioDialogueAndAbsolutePathFields()
    {
        string sourceFolder = Path.Combine(Path.GetTempPath(), "PrivateSource-DO-NOT-EXPORT");
        var entry = MakeEvent(sourceFolder, "Encounter", "Lux", "Ready");
        entry.AudioFiles = ["PRIVATE_AUDIO_FILE_001.ogg", "PRIVATE_AUDIO_FILE_002.wav"];
        entry.DirectAudioFiles = ["PRIVATE_DIRECT_FILE.ogg"];
        entry.AudioFamilies =
        [
            new AudioFamilyModel
            {
                Name = "PRIVATE_FAMILY_NAME",
                AudioFiles = ["PRIVATE_FAMILY_AUDIO.ogg"]
            }
        ];
        entry.Dialogue = "PRIVATE_EVENT_DIALOGUE";
        entry.ParsedData.Dialogue = "PRIVATE_PARSED_TRANSCRIPTION";
        entry.ParsedData.IconPath = Path.Combine(sourceFolder, "PRIVATE_ICON_PATH.png");
        entry.ParsedData.OriginalFolder = "PRIVATE_ORIGINAL_FOLDER";

        string json = EventManifestExportService.CreateJson(sourceFolder, "EN", [entry]);

        Assert.DoesNotContain(sourceFolder, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_", json, StringComparison.Ordinal);
        Assert.DoesNotContain(".ogg", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".wav", json, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        JsonElement exported = document.RootElement.GetProperty("events")[0];
        Assert.Equal(2, exported.GetProperty("audioCount").GetInt32());
        Assert.Equal(1, exported.GetProperty("audioFamilyCount").GetInt32());
        Assert.False(exported.TryGetProperty("folderPath", out _));
        Assert.False(exported.TryGetProperty("dialogue", out _));
        Assert.False(exported.TryGetProperty("audioFiles", out _));
    }

    [Fact]
    public void CreateJsonUsesRelativePathsOnlyForFoldersInsideSourceRoot()
    {
        string baseFolder = Path.Combine(Path.GetTempPath(), "VideoGenerator.ManifestTests", "Containment");
        string sourceFolder = Path.Combine(baseFolder, "Source");
        var nested = MakeEvent(sourceFolder, "Inside", "Ahri", "Ready");
        nested.FolderPath = Path.Combine(sourceFolder, "Region", "Champion", "Inside");

        var sibling = MakeEvent(sourceFolder, "Sibling", "Lux", "Ready");
        sibling.FolderPath = Path.Combine(baseFolder, "SourceExtended", "Sibling");

        var traversal = MakeEvent(sourceFolder, "Traversal", "Lux", "Pending");
        traversal.FolderPath = Path.Combine(sourceFolder, "..", "PRIVATE_ESCAPE", "Traversal");

        var external = MakeEvent(sourceFolder, "External", "Lux", "Pending");
        external.FolderPath = Path.Combine(baseFolder, "PRIVATE_OUTSIDE", "External");

        string json = EventManifestExportService.CreateJson(sourceFolder, "EN", [sibling, nested, traversal, external]);

        using var document = JsonDocument.Parse(json);
        var relativePaths = document.RootElement.GetProperty("events")
            .EnumerateArray()
            .ToDictionary(e => e.GetProperty("folderName").GetString()!,
                e => e.GetProperty("relativeFolder").GetString());

        Assert.Equal("Region/Champion/Inside", relativePaths["Inside"]);
        Assert.Equal("Sibling", relativePaths["Sibling"]);
        Assert.Equal("Traversal", relativePaths["Traversal"]);
        Assert.Equal("External", relativePaths["External"]);
        Assert.DoesNotContain(baseFolder, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_", json, StringComparison.Ordinal);
        Assert.DoesNotContain("..", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateJsonHandlesNullInputsAndMissingNestedData()
    {
        string empty = EventManifestExportService.CreateJson(null!, null!, null!);
        using (var document = JsonDocument.Parse(empty))
        {
            Assert.Equal(string.Empty, document.RootElement.GetProperty("locale").GetString());
            Assert.Empty(document.RootElement.GetProperty("events").EnumerateArray());
        }

        var incomplete = new PreviewEventModel
        {
            FolderName = null!,
            FolderPath = null!,
            CharacterName = null!,
            AudioFiles = null!,
            AudioFamilies = null!,
            Status = null!,
            ParsedData = null!
        };

        string json = EventManifestExportService.CreateJson(null!, null!, [null!, incomplete]);
        using var result = JsonDocument.Parse(json);
        JsonElement entry = Assert.Single(result.RootElement.GetProperty("events").EnumerateArray());
        Assert.Equal(string.Empty, entry.GetProperty("folderName").GetString());
        Assert.Equal(string.Empty, entry.GetProperty("relativeFolder").GetString());
        Assert.Equal(string.Empty, entry.GetProperty("champion").GetString());
        Assert.Equal(string.Empty, entry.GetProperty("displayText").GetString());
        Assert.Equal(string.Empty, entry.GetProperty("iconType").GetString());
        Assert.Equal(0, entry.GetProperty("audioCount").GetInt32());
        Assert.Equal(0, entry.GetProperty("audioFamilyCount").GetInt32());
        Assert.False(entry.GetProperty("isMapped").GetBoolean());
    }

    [Fact]
    public void CreateJsonSanitizesAbsoluteFallbackFolderNamesAndSortsTiesDeterministically()
    {
        string sourceFolder = Path.Combine(Path.GetTempPath(), "VideoGenerator.ManifestTests", "Fallback");
        var early = MakeEvent(sourceFolder, "Tie", "Ahri", "Pending");
        early.FolderPath = Path.Combine(sourceFolder, "Tie");
        var later = MakeEvent(sourceFolder, "Tie", "Ahri", "Ready");
        later.FolderPath = early.FolderPath;
        var external = MakeEvent(sourceFolder, "External", "Lux", "Ready");
        external.FolderPath = Path.Combine(Path.GetTempPath(), "Private", "Outside");
        external.FolderName = Path.Combine(Path.GetTempPath(), "PRIVATE_ABSOLUTE_FOLDER", "External");

        string json = EventManifestExportService.CreateJson(sourceFolder, "ES", [later, external, early]);
        string reverse = EventManifestExportService.CreateJson(sourceFolder, "ES", [early, external, later]);

        Assert.Equal(json, reverse);
        Assert.DoesNotContain("PRIVATE_ABSOLUTE_FOLDER", json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        JsonElement outside = document.RootElement.GetProperty("events")
            .EnumerateArray().Single(e => e.GetProperty("folderName").GetString() == "External");
        Assert.Equal("External", outside.GetProperty("relativeFolder").GetString());
    }

    private static PreviewEventModel MakeEvent(string sourceFolder, string folderName, string champion, string status) =>
        new()
        {
            FolderPath = Path.Combine(sourceFolder, folderName),
            FolderName = folderName,
            CharacterName = champion,
            Status = status,
            ParsedData = new ParsedEvent
            {
                DisplayText = $"Event {folderName}",
                IconType = "champion",
                IconLookupName = champion,
                IsMapped = true
            }
        };
}
