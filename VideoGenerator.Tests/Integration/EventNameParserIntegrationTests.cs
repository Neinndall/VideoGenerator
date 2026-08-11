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

    private static EventNameParser CreateParser(string root, HttpClient httpClient)
    {
        var logger = new LogService();
        var dataFetcher = new DataFetcher(httpClient, logger);
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
