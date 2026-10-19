using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Integration;

/// <summary>
/// Explicit opt-in verification against a locally installed retail client.
/// Never modifies Riot files or depends on network; generated PNGs use a temp folder.
/// </summary>
public sealed class LocalGameIconManagerRetailTests
{
    [Fact]
    public async Task OptionalRetailResolvesChampionSkinItemAndSplashFromWad()
    {
        if (Environment.GetEnvironmentVariable("VG_TEST_RETAIL_WAD") != "1") return;

        const string gamePath = @"C:\Riot Games\League of Legends";
        Assert.True(Directory.Exists(gamePath), "Retail League of Legends installation required for opt-in smoke.");

        string root = Directory.CreateTempSubdirectory("VG.LocalIcons.").FullName;
        try
        {
            var logger = new LogService();
            var cache = Path.Combine(root, "IconCache");
            var source = new LeagueWadAssetSource(logger, gamePath, cache);
            var fetcher = new DataFetcher(logger, root);
            var aliases = new AliasManager(logger, Path.Combine(root, "aliases.json"));
            var groups = new GroupManager(logger, Path.Combine(root, "groups.json"));
            var skinlines = new SkinlineManager(fetcher, aliases, logger);
            var resolver = new LeagueWadHashResolver(source);
            var catalog = new OfflineSkinCatalog(root, logger);
            var manager = new LocalGameIconManager(source, fetcher, aliases,
                groups, skinlines, logger, cache, resolver, catalog);

            Assert.True(source.IsAvailable);

            string champion = await manager.FindIconAsync("champion", "Ahri");
            using (var image = Image.Load(champion!))
            {
                Assert.Equal(128, image.Width);
                Assert.Equal(128, image.Height);
            }

            string tile = await manager.FindIconAsync("champion", "AhriSkin7");
            using (var image = Image.Load(tile!))
            {
                Assert.Equal(380, image.Width);
                Assert.Equal(380, image.Height);
            }

            string item = await manager.FindIconAsync("item", "Boots");
            using (var image = Image.Load(item!))
            {
                Assert.Equal(64, image.Width);
                Assert.Equal(64, image.Height);
            }

            string splash = await manager.FindSplashAsync("Ahri", 7);
            using (var image = Image.Load(splash!))
            {
                Assert.Equal(1280, image.Width);
                Assert.Equal(720, image.Height);
            }

        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

}
