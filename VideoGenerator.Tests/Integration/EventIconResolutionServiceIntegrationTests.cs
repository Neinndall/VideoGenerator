using System.Net.Http;
using VideoGenerator.Models;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class EventIconResolutionServiceIntegrationTests
{
    [Theory]
    [InlineData("system", "Gold", "system", "Gold_icon.png")]
    [InlineData("structure", "Turret", "structure", "Blue_Turret_icon.png")]
    public async Task ResolvesCachedIconsWithoutNetwork(
        string iconType,
        string lookupName,
        string cacheCategory,
        string cacheFileName)
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.IconIntegration.").FullName;

        try
        {
            string iconCacheDirectory = Path.Combine(root, "icons");
            string cachedIconPath = Path.Combine(iconCacheDirectory, cacheCategory, cacheFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(cachedIconPath)!);
            File.WriteAllBytes(cachedIconPath, new byte[] { 1, 2, 3 });

            var logger = new LogService();
            using var httpClient = new HttpClient();
            var dataFetcher = new DataFetcher(httpClient, logger);
            var aliasManager = new AliasManager(logger, Path.Combine(root, "aliases.json"));
            var groupManager = new GroupManager(logger, Path.Combine(root, "groups.json"));
            var skinlineManager = new SkinlineManager(dataFetcher, aliasManager, logger);
            var iconManager = new IconManager(
                dataFetcher,
                groupManager,
                aliasManager,
                skinlineManager,
                logger,
                iconCacheDirectory);
            var service = new EventIconResolutionService(dataFetcher, iconManager, logger);

            string resolvedPath = await service.ResolveAsync(new ParsedEvent
            {
                IconType = iconType,
                IconLookupName = lookupName
            });

            Assert.Equal(cachedIconPath, resolvedPath);
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
    public async Task ResolveSkipsGenericEventsWithoutIconDependencies()
    {
        var service = new EventIconResolutionService(null!, null!, new LogService());

        Assert.Null(await service.ResolveAsync(null));
        Assert.Null(await service.ResolveAsync(new ParsedEvent { IconType = "generic" }));
    }
}
