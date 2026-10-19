using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VideoGenerator.Models;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class EventIconResolutionServiceIntegrationTests
{
    [Theory]
    [InlineData("system", "Gold", "system", "Gold_icon.png")]
    [InlineData(" SYSTEM ", "Gold", "system", "Gold_icon.png")]
    [InlineData("structure", "Turret", "structure", "Blue_Turret_icon.png")]
    [InlineData("Structure", "Turret", "structure", "Blue_Turret_icon.png")]
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
            using (var cachedImage = new Image<Rgba32>(1, 1))
                cachedImage.SaveAsPng(cachedIconPath);

            var logger = new LogService();
            var dataFetcher = new DataFetcher(logger);
            var aliasManager = new AliasManager(logger, Path.Combine(root, "aliases.json"));
            var groupManager = new GroupManager(logger, Path.Combine(root, "groups.json"));
            var skinlineManager = new SkinlineManager(dataFetcher, aliasManager, logger);
            var source = new LeagueWadAssetSource(logger,
                Path.Combine(root, "uninstalled-league"), iconCacheDirectory);
            var iconManager = new LocalGameIconManager(source, dataFetcher,
                aliasManager, groupManager, skinlineManager, logger, iconCacheDirectory);
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
        Assert.Null(await service.ResolveAsync(new ParsedEvent { IconType = " GENERIC " }));
        Assert.Null(await service.ResolveAsync(new ParsedEvent { IconType = "champion", IconLookupName = " " }));
    }

    [Theory]
    [InlineData(" GENERIC ", "Generic", false)]
    [InlineData("Champion", " ", false)]
    [InlineData(" champion ", "Ahri", true)]
    [InlineData("system", "Gold", true)]
    public void DeterminesIconRequirementsWithoutDependingOnDisplayCasing(
        string iconType, string lookup, bool expected)
    {
        Assert.Equal(expected, EventIconResolutionService.RequiresIcon(new ParsedEvent
        {
            IconType = iconType,
            IconLookupName = lookup
        }));
    }
}
