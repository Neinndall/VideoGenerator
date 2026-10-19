using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Integration;

/// <summary>
/// All input data is generated locally. This exercises the app's actual
/// refresh coordinator with two different Riot WAD snapshots in one session.
/// </summary>
[Trait("Category", "Integration")]
public sealed class LocalGameDataRefreshServiceTests
{
    private const string Prefix = "plugins/rcp-be-lol-game-data/global/default/v1/";

    [Fact]
    public async Task NewGamePatchRefreshesOfficialSkinIdsWithoutRestartAndKeepsUserConfig()
    {
        string root = Directory.CreateTempSubdirectory("VG.GamePatch.").FullName;
        try
        {
            string wad = Path.Combine(root, "Plugins", "rcp-be-lol-game-data", "default-assets.wad");
            Directory.CreateDirectory(Path.GetDirectoryName(wad)!);
            WriteCatalogWad(wad, "103007", "Arcade Ahri");

            var logger = new LogService();
            var source = new LeagueWadAssetSource(logger, root, Path.Combine(root, "icons"));
            var bootstrap = new LocalWadCatalogBootstrapper(source, logger, root);
            var fetcher = new DataFetcher(logger, root);
            var aliases = new AliasManager(logger, Path.Combine(root, "Config", "aliases.json"));
            var groups = new GroupManager(logger, Path.Combine(root, "Config", "groups.json"));
            var skinlines = new SkinlineManager(fetcher, aliases, logger);
            var hashes = new LeagueWadHashResolver(source);
            var iconManager = new LocalGameIconManager(source, fetcher, aliases,
                groups, skinlines, logger, Path.Combine(root, "icons"), hashes,
                new OfflineSkinCatalog(root, logger));
            var refresh = new LocalGameDataRefreshService(
                source, bootstrap, fetcher, skinlines, iconManager, hashes, logger);

            string rulesFile = Path.Combine(root, "Config", "event_rules.json");
            Directory.CreateDirectory(Path.GetDirectoryName(rulesFile)!);
            await File.WriteAllTextAsync(rulesFile, """{"personal":"keep"}""");

            Assert.True(await refresh.RefreshAsync());
            Assert.Contains("103007", (await fetcher.GetSkinsDataAsync("EN")).Keys);
            Assert.False(await refresh.RefreshAsync());

            WriteCatalogWad(wad, "103008", "New Ahri Skin - patch");
            Assert.True(await refresh.RefreshAsync());

            var refreshed = await fetcher.GetSkinsDataAsync("EN");
            Assert.Contains("103008", refreshed.Keys);
            Assert.DoesNotContain("103007", refreshed.Keys);
            Assert.Equal("New Ahri Skin - patch", refreshed["103008"].GetProperty("name").GetString());
            Assert.Equal("""{"personal":"keep"}""", await File.ReadAllTextAsync(rulesFile));
            Assert.False(await refresh.RefreshAsync());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NewGamePatchAddsChampionsAndRolesToOneAliasCatalogWithoutRestart()
    {
        string root = Directory.CreateTempSubdirectory("VG.ChampionPatch.").FullName;
        try
        {
            string wad = Path.Combine(root, "Plugins", "rcp-be-lol-game-data", "default-assets.wad");
            Directory.CreateDirectory(Path.GetDirectoryName(wad)!);
            WriteCatalogWad(wad, "103007", "Arcade Ahri");

            var logger = new LogService();
            var source = new LeagueWadAssetSource(logger, root, Path.Combine(root, "icons"));
            var bootstrap = new LocalWadCatalogBootstrapper(source, logger, root);
            var fetcher = new DataFetcher(logger, root);
            var aliases = new AliasManager(logger,
                Path.Combine(root, "Config", "champion_aliases.json"));
            var groups = new GroupManager(logger, Path.Combine(root, "Config", "groups.json"));
            var skinlines = new SkinlineManager(fetcher, aliases, logger);
            var hashes = new LeagueWadHashResolver(source);
            var icons = new LocalGameIconManager(source, fetcher, aliases,
                groups, skinlines, logger, Path.Combine(root, "icons"), hashes,
                new OfflineSkinCatalog(root, logger));
            var refresh = new LocalGameDataRefreshService(
                source, bootstrap, fetcher, skinlines, icons, hashes, logger, aliases);

            Assert.False(aliases.IsValidChampion("Zaahen"));
            Assert.True(await refresh.RefreshAsync());
            Assert.False(aliases.IsValidChampion("Zaahen"));

            WriteCatalogWad(wad, "103008", "New Ahri Skin", includeZaahen: true);
            Assert.True(await refresh.RefreshAsync());

            Assert.True(aliases.IsValidChampion("Zaahen"));
            var zaahen = Assert.Single(aliases.Aliases, a => a.DisplayName == "Zaahen");
            Assert.Equal("Zaahen", zaahen.InternalName);
            Assert.Equal(new[] { "fighter", "tank" }, zaahen.Roles);
            Assert.True(zaahen.IsOfficial);
            Assert.True(File.Exists(Path.Combine(root, "Config", "champion_aliases.json")));
            Assert.False(File.Exists(Path.Combine(root, "Config", "champions.json")));
            Assert.False(await refresh.RefreshAsync());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteCatalogWad(string path, string skinId, string name,
        bool includeZaahen = false)
    {
        const string tile = "/lol-game-data/assets/ASSETS/Characters/Ahri/Skins/Skin08/Images/Ahri_Splash_Tile_8.jpg";
        string skins = JsonSerializer.Serialize(
            new System.Collections.Generic.Dictionary<string, object>
            {
                [skinId] = new { name, tilePath = tile }
            });
        var assets = new (string Path, byte[] Content)[]
        {
            (Prefix + "skins.json", Encoding.UTF8.GetBytes(skins)),
            (Prefix + "skinlines.json", Encoding.UTF8.GetBytes("""[{"id":42,"name":"Star Guardian"}]""")),
            (Prefix + "items.json", Encoding.UTF8.GetBytes("""[{"id":3031,"name":"Infinity Edge"}]""")),
            (Prefix + "champion-summary.json", Encoding.UTF8.GetBytes(includeZaahen
                ? """[{"id":62,"name":"Wukong","alias":"MonkeyKing"},{"id":901,"name":"Zaahen","alias":"Zaahen","roles":["fighter","tank"]}]"""
                : """[{"id":62,"name":"Wukong","alias":"MonkeyKing"}]"""))
        };

        string temporary = path + ".tmp";
        try
        {
            using (var stream = File.Create(temporary))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write("RW"u8);
                writer.Write((byte)3);
                writer.Write((byte)1);
                writer.Write(new byte[256]);
                writer.Write(0UL);
                writer.Write(assets.Length);
                int offset = 272 + assets.Length * 32;
                foreach (var asset in assets)
                {
                    writer.Write(LeagueWadAssetSource.ComputePathHash(asset.Path));
                    writer.Write((uint)offset);
                    writer.Write(asset.Content.Length);
                    writer.Write(asset.Content.Length);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((ushort)0);
                    writer.Write(0UL);
                    offset += asset.Content.Length;
                }
                foreach (var asset in assets)
                    writer.Write(asset.Content);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
