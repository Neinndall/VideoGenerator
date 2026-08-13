using System.Net.Http;
using VideoGenerator.Models;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class EventNameParserIntegrationTests
{
    [Fact]
    public async Task ParsesGeneralKillUsingOnlyTemporaryConfiguration()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            using var httpClient = new HttpClient();
            var parser = CreateParser(root, httpClient);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_Aatrox_Kill3DGeneral",
                "EN");

            Assert.Equal("Play_vo_Aatrox_Kill3DGeneral", parsed.OriginalFolder);
            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Contains("Kill", parsed.DisplayText, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(root, "event_rules.json")));
            Assert.True(File.Exists(Path.Combine(root, "groups.json")));
            Assert.True(File.Exists(Path.Combine(root, "champion_aliases.json")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ParsesGeneralAssistWithoutLeavingTargetPlaceholder()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            using var httpClient = new HttpClient();
            var parser = CreateParser(root, httpClient);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_SeraphineSkin69_Assist3DGeneral",
                "EN");

            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal("Assist in General", parsed.DisplayText);
            Assert.DoesNotContain("{0}", parsed.DisplayText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ParsesGeneralIdleEvent()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            using var httpClient = new HttpClient();
            var parser = CreateParser(root, httpClient);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_SeraphineSkin69_Idle3DGeneral",
                "EN");

            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal("Idle in General", parsed.DisplayText);
            Assert.True(parsed.IsMapped);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RoutesThreeDimensionalMonsterAttacksToMonsterParser()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            using var httpClient = new HttpClient();
            var parser = CreateParser(root, httpClient);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_Aatrox_Attack3DBaron",
                "EN");

            Assert.Equal("monster", parsed.IconType);
            Assert.Equal("Baron", parsed.IconLookupName);
            Assert.False(string.IsNullOrWhiteSpace(parsed.DisplayText));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RoutesItemEventsThroughTheCommunityDragonCache()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            string cachePath = Path.Combine(root, "Cache", "items_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, """
                [
                  { "id": 3031, "name": "Infinity Edge", "nameSlug": "InfinityEdge" }
                ]
                """);

            using var httpClient = new HttpClient();
            var parser = CreateParser(root, httpClient);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_Aatrox_BuyItem3DInfinityEdgeR",
                "EN");

            Assert.Equal("item", parsed.IconType);
            Assert.Equal("3031", parsed.IconLookupName);
            Assert.Contains("Buy Item", parsed.DisplayText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Infinity Edge", parsed.DisplayText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RoutesSkinInteractionsThroughTheTemporarySkinCache()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            string cachePath = Path.Combine(root, "Cache", "skins_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, """
                {
                  "123": {
                    "name": "Primordian Bel'Veth",
                    "splashPath": "Characters/Belveth/Skins/Skin123/Belveth_Splash_Centered.jpg"
                  }
                }
                """);

            using var httpClient = new HttpClient();
            var parser = CreateParser(root, httpClient);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_Belveth_FirstEncounterBelvethSkin123",
                "EN");

            Assert.Equal("champion", parsed.IconType);
            Assert.Equal("BelvethSkin123", parsed.IconLookupName);
            Assert.Contains("Primordian Bel'Veth", parsed.DisplayText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RoutesUnmappedSpellEventsToTheSpellParser()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            using var httpClient = new HttpClient();
            var parser = CreateParser(root, httpClient);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_Aatrox_CustomSpell3D",
                "EN");

            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal("Custom Spell", parsed.DisplayText);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FlagsUnknownFolderNamesAsUnmapped()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            using var httpClient = new HttpClient();
            var parser = CreateParser(root, httpClient);
            const string folderName = "Play_vo_Aatrox_UnknownEvent3D";

            ParsedEvent parsed = await parser.ParseFolderNameAsync(folderName, "EN");

            Assert.False(parsed.IsMapped);
            Assert.Equal(folderName, parsed.DisplayText);
            Assert.Equal("generic", parsed.IconType);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ParsesUltimateReadyMovementVariant()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            using var httpClient = new HttpClient();
            var parser = CreateParser(root, httpClient);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_AatroxSkin33_Move2DRReady",
                "EN");

            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal("Movement (Ultimate Ready)", parsed.DisplayText);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Play_vo_Locke_Joke3DFail", "Joke (Failed)")]
    [InlineData("Play_vo_Locke_Joke3DFailEnd", "Joke (Failed)")]
    [InlineData("Play_vo_Locke_Joke3DSuccess", "Joke (Successful)")]
    [InlineData("Play_vo_Locke_Joke3DSuccessEnd", "Joke (Successful)")]
    public async Task ParsesJokeOutcomeVariants(string folderName, string expectedDisplayText)
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            using var httpClient = new HttpClient();
            var parser = CreateParser(root, httpClient);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(folderName, "EN");

            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal(expectedDisplayText, parsed.DisplayText);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static EventNameParser CreateParser(string root, HttpClient httpClient)
    {
        var logger = new LogService();
        var dataFetcher = new DataFetcher(httpClient, logger, root);
        File.WriteAllText(Path.Combine(root, "translations.json"), """
            {
              "EN": {
                "event_buy_item": "Buy Item {item_name}",
                "event_assist_general": "Assist in General",
                "event_idle": "Idle",
                "event_joke_fail": "Joke (Failed)",
                "event_kill_general": "Kill in General",
                "event_move_r_ready": "Movement (Ultimate Ready)",
                "event_joke_success": "Joke (Successful)",
                "interaction_attack_monster": "Attack {monster}",
                "interaction_first_encounter_one": "First Encounter with {0}",
                "suffix_in_general": " in General"
              }
            }
            """);
        var translationService = new TranslationService(
            logger,
            Path.Combine(root, "translations.json"));
        var ruleManager = new RuleManager(
            logger,
            Path.Combine(root, "event_rules.json"));
        var groupManager = new GroupManager(
            logger,
            Path.Combine(root, "groups.json"));
        var aliasManager = new AliasManager(
            logger,
            Path.Combine(root, "champion_aliases.json"));
        var skinlineManager = new SkinlineManager(dataFetcher, aliasManager, logger);
        return new EventNameParser(
            translationService,
            dataFetcher,
            ruleManager,
            groupManager,
            aliasManager,
            skinlineManager);
    }
}
