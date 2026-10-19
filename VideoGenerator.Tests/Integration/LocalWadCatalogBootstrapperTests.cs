using System.Text;
using System.Text.Json;
using VideoGenerator.Models;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class LocalWadCatalogBootstrapperTests
{
    private const string WadsPrefix = "plugins/rcp-be-lol-game-data/global/";
    private const string ValidSkins = """
        {
          "103007": {
            "name": "Star Guardian Ahri",
            "tilePath": "/lol-game-data/assets/ASSETS/Characters/Ahri/Skins/Skin07/Images/Ahri_Splash_Tile_7.jpg",
            "splashPath": "/lol-game-data/assets/ASSETS/Characters/Ahri/Skins/Skin07/Images/Ahri_Splash_7.jpg",
            "skinLines": [{"id": 42}]
          }
        }
        """;
    private const string ValidSkinlines = """
        [{"id":42,"name":"Star Guardian"}]
        """;
    private const string ValidItems = """
        [{"id":3031,"name":"Infinity Edge","nameSlug":"InfinityEdge"}]
        """;
    private const string ValidChampionSummary = """
        [
          {"id":103,"alias":"Ahri","name":"Ahri"},
          {"id":62,"alias":"MonkeyKing","name":"Wukong"},
          {"id":103,"alias":"ahri","name":"AHRI"},
          {"id":999,"alias":"","name":" "},
          null
        ]
        """;

    [Fact]
    public async Task ImportsGameDataForAllLocalesAndChampionSummaryWithoutHttpOrWadMutation()
    {
        using var fixture = new CatalogFixture();
        string wad = fixture.AddWad(BuildFullCatalog());
        byte[] originalWad = await File.ReadAllBytesAsync(wad);
        var bootstrapper = fixture.CreateBootstrapper();

        int created = await bootstrapper.PrepareAsync();

        Assert.Equal(11, created);
        foreach (string locale in new[] { "default", "es_es", "tr_tr" })
        {
            Assert.Equal(Encoding.UTF8.GetBytes(ValidSkins),
                await File.ReadAllBytesAsync(AppConfig.GetSkinsCachePath(locale, fixture.Root)));
            Assert.Equal(Encoding.UTF8.GetBytes(ValidSkinlines),
                await File.ReadAllBytesAsync(AppConfig.GetSkinLinesCachePath(locale, fixture.Root)));
            Assert.Equal(Encoding.UTF8.GetBytes(ValidItems),
                await File.ReadAllBytesAsync(AppConfig.GetItemsCachePath(locale, fixture.Root)));
        }

        string championsPath = AppConfig.GetChampionsFilePath(fixture.Root);
        string[] champions = JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(championsPath))!;
        Assert.Equal(new[] { "Ahri", "MonkeyKing", "Wukong" }, champions);
        Assert.Equal(Encoding.UTF8.GetBytes(ValidChampionSummary),
            await File.ReadAllBytesAsync(SummaryCachePath(fixture.Root)));

        // Existing consumers must be able to read the unchanged CommunityDragon schema.
        var catalog = new OfflineSkinCatalog(fixture.Root);
        Assert.Equal(new[]
        {
            "assets/characters/ahri/skins/skin07/images/ahri_splash_tile_7.jpg",
            "assets/characters/ahri/skins/skin07/images/ahri_splash_7.jpg"
        }, catalog.GetSkinAssetPaths("Ahri", 7));
        Assert.Equal(3031, catalog.GetItemId("InfinityEdge"));
        Assert.Equal("Star Guardian", Assert.Single(catalog.GetSkinlines()).Name);

        Assert.Equal(originalWad, await File.ReadAllBytesAsync(wad));
        Assert.False(File.Exists(AppConfig.GetLocalVersionFilePath(fixture.Root)));
        Assert.Equal(0, await bootstrapper.PrepareAsync());
        Assert.Equal(originalWad, await File.ReadAllBytesAsync(wad));
        AssertNoTemporaryFiles(fixture.Root);
    }

    [Fact]
    public async Task BootstrapperAndDatabaseBuilderProduceCompatibleUserCatalogsOffline()
    {
        using var fixture = new CatalogFixture();
        fixture.AddWad(BuildDefaultCatalog());
        var bootstrapper = fixture.CreateBootstrapper();
        var databaseBuilder = new DatabaseBuilder(new LogService(), fixture.Root);

        Assert.Equal(5, await bootstrapper.PrepareAsync());
        await databaseBuilder.InitializeDatabasesAsync("local");
        await databaseBuilder.ReadyTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(databaseBuilder.IsReady);
        var items = JsonSerializer.Deserialize<Dictionary<string, string>>(
            await File.ReadAllTextAsync(AppConfig.GetItemsFilePath(fixture.Root)))!;
        Assert.Equal("3031", items["Infinity Edge"]);
        Assert.Equal(new[] { "Ahri", "MonkeyKing", "Wukong" },
            JsonSerializer.Deserialize<string[]>(
                await File.ReadAllTextAsync(AppConfig.GetChampionsFilePath(fixture.Root))));
        using (var monsters = JsonDocument.Parse(await File.ReadAllTextAsync(
                   AppConfig.GetMonstersFilePath(fixture.Root))))
            Assert.Equal(JsonValueKind.Object, monsters.RootElement.ValueKind);
        using (var structures = JsonDocument.Parse(await File.ReadAllTextAsync(
                   AppConfig.GetStructuresFilePath(fixture.Root))))
            Assert.Equal(JsonValueKind.Array, structures.RootElement.ValueKind);
        AssertNoTemporaryFiles(fixture.Root);
    }

    [Fact]
    public async Task LegacyCachesMigrateFromAvailableWadWhileUserConfigRemainsUnchanged()
    {
        using var fixture = new CatalogFixture();
        fixture.AddWad(BuildFullCatalog());
        var originals = new Dictionary<string, byte[]>();
        foreach (string locale in new[] { "default", "es_es", "tr_tr" })
        {
            originals.Add(AppConfig.GetSkinsCachePath(locale, fixture.Root),
                Encoding.UTF8.GetBytes("{ user edited cache /* not valid JSON */"));
            originals.Add(AppConfig.GetSkinLinesCachePath(locale, fixture.Root),
                new byte[] { 0xef, 0xbb, 0xbf, (byte)'[', (byte)'2', (byte)']' });
            originals.Add(AppConfig.GetItemsCachePath(locale, fixture.Root),
                Encoding.UTF8.GetBytes("[{\"id\":9999,\"name\":\"Personal Item\"}]"));
        }

        originals.Add(AppConfig.GetChampionsFilePath(fixture.Root),
            Encoding.UTF8.GetBytes("[\"Custom Champion\",\"Ahri\"]"));
        originals.Add(AppConfig.GetItemsFilePath(fixture.Root),
            Encoding.UTF8.GetBytes("{\"Custom Item\":\"12345\"}"));
        originals.Add(AppConfig.GetMonstersFilePath(fixture.Root),
            Encoding.UTF8.GetBytes("{\"Epic\":[\"Custom Dragon\"]}"));
        originals.Add(AppConfig.GetStructuresFilePath(fixture.Root),
            Encoding.UTF8.GetBytes("[{\"Keyword\":\"Personal Nexus\",\"TargetName\":\"Nexus\"}]"));
        originals.Add(AppConfig.GetLocalVersionFilePath(fixture.Root),
            Encoding.UTF8.GetBytes("14.1.1"));
        originals.Add(Path.Combine(fixture.Root, "Config", "event_rules.json"),
            Encoding.UTF8.GetBytes("{\"custom\":true}"));
        foreach (var original in originals)
            fixture.WriteFile(original.Key, original.Value);

        var bootstrapper = fixture.CreateBootstrapper();
        Assert.Equal(10, await bootstrapper.PrepareAsync());
        Assert.Equal(0, await bootstrapper.PrepareAsync());

        foreach (var original in originals)
        {
            if (original.Key.StartsWith(Path.Combine(fixture.Root, "Cache") + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                string expected = original.Key.Contains("skins_data_") ? ValidSkins :
                    original.Key.Contains("skinlines_data_") ? ValidSkinlines : ValidItems;
                Assert.Equal(Encoding.UTF8.GetBytes(expected), await File.ReadAllBytesAsync(original.Key));
                Assert.Equal(original.Value,
                    await File.ReadAllBytesAsync(original.Key + ".pre-wad.bak"));
            }
            else
                Assert.Equal(original.Value, await File.ReadAllBytesAsync(original.Key));
        }
        Assert.Equal(Encoding.UTF8.GetBytes(ValidChampionSummary),
            await File.ReadAllBytesAsync(SummaryCachePath(fixture.Root)));
        AssertNoTemporaryFiles(fixture.Root);
    }

    [Fact]
    public async Task ReplacesLegacyCachesButDoesNotReplacePreexistingCustomChampions()
    {
        using var fixture = new CatalogFixture();
        fixture.AddWad(BuildFullCatalog());
        string existingSkins = AppConfig.GetSkinsCachePath("default", fixture.Root);
        string existingItems = AppConfig.GetItemsCachePath("es_es", fixture.Root);
        string existingChampions = AppConfig.GetChampionsFilePath(fixture.Root);
        byte[] editedSkins = Encoding.UTF8.GetBytes("{\"custom\":{\"name\":\"User skin\"}}");
        byte[] editedItems = Encoding.UTF8.GetBytes("[{\"id\":123,\"name\":\"User item\"}]");
        byte[] editedChampions = Encoding.UTF8.GetBytes("[\"Custom\"]");
        fixture.WriteFile(existingSkins, editedSkins);
        fixture.WriteFile(existingItems, editedItems);
        fixture.WriteFile(existingChampions, editedChampions);

        int created = await fixture.CreateBootstrapper().PrepareAsync();

        Assert.Equal(10, created);
        Assert.Equal(Encoding.UTF8.GetBytes(ValidSkins), await File.ReadAllBytesAsync(existingSkins));
        Assert.Equal(Encoding.UTF8.GetBytes(ValidItems), await File.ReadAllBytesAsync(existingItems));
        Assert.Equal(editedSkins, await File.ReadAllBytesAsync(existingSkins + ".pre-wad.bak"));
        Assert.Equal(editedItems, await File.ReadAllBytesAsync(existingItems + ".pre-wad.bak"));
        Assert.Equal(editedChampions, await File.ReadAllBytesAsync(existingChampions));
        Assert.Equal(Encoding.UTF8.GetBytes(ValidSkinlines),
            await File.ReadAllBytesAsync(AppConfig.GetSkinLinesCachePath("default", fixture.Root)));
        Assert.Equal(Encoding.UTF8.GetBytes(ValidSkins),
            await File.ReadAllBytesAsync(AppConfig.GetSkinsCachePath("tr_tr", fixture.Root)));
        AssertNoTemporaryFiles(fixture.Root);
    }

    [Fact]
    public async Task LaterRiotPatchRefreshesAllCatalogsAndGeneratedChampionsWithoutRewritingUnchangedFiles()
    {
        using var fixture = new CatalogFixture();
        fixture.AddWad(BuildDefaultCatalog());

        Assert.Equal(5, await fixture.CreateBootstrapper().PrepareAsync());
        string skinsPath = AppConfig.GetSkinsCachePath("default", fixture.Root);
        string skinsSidecar = skinsPath + ".wad-source";
        string initialProvenance = await File.ReadAllTextAsync(skinsSidecar);
        DateTime initialWriteTime = File.GetLastWriteTimeUtc(skinsPath);
        DateTime provenanceWriteTime = File.GetLastWriteTimeUtc(skinsSidecar);
        Assert.Equal(0, await fixture.CreateBootstrapper().PrepareAsync());
        Assert.Equal(initialWriteTime, File.GetLastWriteTimeUtc(skinsPath));
        Assert.Equal(provenanceWriteTime, File.GetLastWriteTimeUtc(skinsSidecar));

        const string newSkins = """
            {"103008":{"name":"Prestige Ahri","tilePath":"/lol-game-data/assets/ASSETS/Characters/Ahri/Skins/Skin08/Images/Ahri_Splash_Tile_8.jpg"}}
            """;
        const string newSkinlines = """[{"id":91,"name":"Prestige"}]""";
        const string newItems = """[{"id":3006,"name":"Berserker's Greaves","nameSlug":"BerserkerGreaves"}]""";
        const string newChampions = """[{"id":103,"alias":"Ahri","name":"Ahri"},{"id":901,"alias":"NewChampion","name":"New Champion"}]""";
        fixture.AddWad(
            JsonAsset("default", "skins.json", newSkins),
            JsonAsset("default", "skinlines.json", newSkinlines),
            JsonAsset("default", "items.json", newItems),
            JsonAsset("default", "champion-summary.json", newChampions));

        Assert.Equal(5, await fixture.CreateBootstrapper().PrepareAsync());
        Assert.Equal(Encoding.UTF8.GetBytes(newSkins), await File.ReadAllBytesAsync(skinsPath));
        Assert.NotEqual(initialProvenance, await File.ReadAllTextAsync(skinsSidecar));
        Assert.Equal(Encoding.UTF8.GetBytes(newChampions),
            await File.ReadAllBytesAsync(SummaryCachePath(fixture.Root)));
        Assert.Equal(new[] { "Ahri", "New Champion", "NewChampion" },
            JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(
                AppConfig.GetChampionsFilePath(fixture.Root))));

        var catalog = new OfflineSkinCatalog(fixture.Root);
        Assert.Single(catalog.GetSkinAssetPaths("Ahri", 8));
        Assert.Empty(catalog.GetSkinAssetPaths("Ahri", 7));
        Assert.Equal("Prestige", Assert.Single(catalog.GetSkinlines()).Name);
        Assert.Equal(3006, catalog.GetItemId("BerserkerGreaves"));
        Assert.Null(catalog.GetItemId("InfinityEdge"));
        Assert.Equal(0, await fixture.CreateBootstrapper().PrepareAsync());
        AssertNoTemporaryFiles(fixture.Root);
    }

    [Fact]
    public async Task CorruptPatchLeavesAllGoodCachesAndTheirProvenanceUntouched()
    {
        using var fixture = new CatalogFixture();
        fixture.AddWad(BuildDefaultCatalog());
        Assert.Equal(5, await fixture.CreateBootstrapper().PrepareAsync());
        string[] tracked = new[]
        {
            AppConfig.GetSkinsCachePath("default", fixture.Root),
            AppConfig.GetSkinLinesCachePath("default", fixture.Root),
            AppConfig.GetItemsCachePath("default", fixture.Root),
            SummaryCachePath(fixture.Root),
            AppConfig.GetChampionsFilePath(fixture.Root)
        };
        var originals = tracked.ToDictionary(path => path,
            path => File.ReadAllBytes(path));
        var markers = tracked.Take(4).ToDictionary(path => path,
            path => File.ReadAllBytes(path + ".wad-source"));

        fixture.AddWad(
            JsonAsset("default", "skins.json", "{ malformed"),
            JsonAsset("default", "skinlines.json", """[{"name":"missing id"}]"""),
            JsonAsset("default", "items.json", "[]"),
            JsonAsset("default", "champion-summary.json", "[null,{},42]"));

        Assert.Equal(0, await fixture.CreateBootstrapper().PrepareAsync());
        foreach (var existing in originals)
            Assert.Equal(existing.Value, await File.ReadAllBytesAsync(existing.Key));
        foreach (var marker in markers)
            Assert.Equal(marker.Value, await File.ReadAllBytesAsync(marker.Key + ".wad-source"));
        AssertNoTemporaryFiles(fixture.Root);
    }

    [Fact]
    public async Task UserEditedGeneratedChampionsAreNeverOverwrittenByLaterPatch()
    {
        using var fixture = new CatalogFixture();
        fixture.AddWad(BuildDefaultCatalog());
        Assert.Equal(5, await fixture.CreateBootstrapper().PrepareAsync());
        string configChampions = AppConfig.GetChampionsFilePath(fixture.Root);
        byte[] customNames = Encoding.UTF8.GetBytes("""["My Champion","Ahri"]""");
        fixture.WriteFile(configChampions, customNames);
        fixture.AddWad(JsonAsset("default", "champion-summary.json",
            """[{"id":901,"alias":"NewChampion","name":"New Champion"}]"""));

        Assert.Equal(1, await fixture.CreateBootstrapper().PrepareAsync());
        Assert.Equal(customNames, await File.ReadAllBytesAsync(configChampions));
        Assert.Equal(0, await fixture.CreateBootstrapper().PrepareAsync());
        AssertNoTemporaryFiles(fixture.Root);
    }

    [Fact]
    public async Task OnlyChangedLocalizedCatalogIsRefreshedWhenOtherAssetsAreUnavailable()
    {
        using var fixture = new CatalogFixture();
        fixture.AddWad(BuildFullCatalog());
        Assert.Equal(11, await fixture.CreateBootstrapper().PrepareAsync());
        string esSkins = AppConfig.GetSkinsCachePath("es_es", fixture.Root);
        string defaultSkins = AppConfig.GetSkinsCachePath("default", fixture.Root);
        byte[] originalDefault = await File.ReadAllBytesAsync(defaultSkins);
        byte[] updated = Encoding.UTF8.GetBytes(
            """{"103008":{"name":"Ahri Nueva","tilePath":"/lol-game-data/assets/ASSETS/Characters/Ahri/Skins/Skin08/Images/Ahri_Splash_Tile_8.jpg"}}""");
        fixture.AddWad((WadsPrefix + "es_es/v1/skins.json", updated));

        Assert.Equal(1, await fixture.CreateBootstrapper().PrepareAsync());
        Assert.Equal(updated, await File.ReadAllBytesAsync(esSkins));
        Assert.Equal(originalDefault, await File.ReadAllBytesAsync(defaultSkins));
        Assert.Equal(Encoding.UTF8.GetBytes(ValidItems),
            await File.ReadAllBytesAsync(AppConfig.GetItemsCachePath("es_es", fixture.Root)));
        AssertNoTemporaryFiles(fixture.Root);
    }

    [Fact]
    public async Task MissingInstalledWadPreservesPreviouslyPreparedCatalogs()
    {
        using var fixture = new CatalogFixture();
        string wad = fixture.AddWad(BuildDefaultCatalog());
        Assert.Equal(5, await fixture.CreateBootstrapper().PrepareAsync());
        string skins = AppConfig.GetSkinsCachePath("default", fixture.Root);
        byte[] original = await File.ReadAllBytesAsync(skins);
        File.Delete(wad);

        Assert.Equal(0, await fixture.CreateBootstrapper().PrepareAsync());
        Assert.Equal(original, await File.ReadAllBytesAsync(skins));
        AssertNoTemporaryFiles(fixture.Root);
    }

    [Theory]
    [InlineData("skins.json", "{ invalid json")]
    [InlineData("skins.json", "[]")]
    [InlineData("skins.json", "{}")]
    [InlineData("skinlines.json", "{\"lines\":[]}")]
    [InlineData("skinlines.json", "[ invalid")]
    [InlineData("skinlines.json", "[]")]
    [InlineData("items.json", "{\"data\":[]}")]
    [InlineData("items.json", "[ invalid")]
    [InlineData("items.json", "[]")]
    [InlineData("champion-summary.json", "{\"data\":[]}")]
    [InlineData("champion-summary.json", "not json")]
    public async Task RejectsMalformedOrWrongRootWadCatalogAndKeepsOriginalUntouched(
        string filename, string badJson)
    {
        using var fixture = new CatalogFixture();
        var assets = new List<(string Path, byte[] Data)>
        {
            JsonAsset("default", filename, badJson)
        };
        if (filename != "skinlines.json")
            assets.Add(JsonAsset("default", "skinlines.json", ValidSkinlines));
        string wad = fixture.AddWad(assets.ToArray());
        byte[] archiveBytes = await File.ReadAllBytesAsync(wad);
        string destination = filename switch
        {
            "skins.json" => AppConfig.GetSkinsCachePath("default", fixture.Root),
            "skinlines.json" => AppConfig.GetSkinLinesCachePath("default", fixture.Root),
            "items.json" => AppConfig.GetItemsCachePath("default", fixture.Root),
            _ => AppConfig.GetChampionsFilePath(fixture.Root)
        };

        int created = await fixture.CreateBootstrapper().PrepareAsync();

        Assert.False(File.Exists(destination));
        Assert.Equal(filename == "skinlines.json" ? 0 : 1, created);
        Assert.Equal(archiveBytes, await File.ReadAllBytesAsync(wad));
        AssertNoTemporaryFiles(fixture.Root);
    }

    [Theory]
    [InlineData("skins.json", "{\"103007\":[]}")]
    [InlineData("skinlines.json", "[123,\"invalid\"]")]
    [InlineData("items.json", "[123,\"invalid\"]")]
    public async Task RejectsRootCompatibleButStructurallyCorruptWadJson(string filename, string invalidJson)
    {
        using var fixture = new CatalogFixture();
        fixture.AddWad(JsonAsset("default", filename, invalidJson));
        string path = filename switch
        {
            "skins.json" => AppConfig.GetSkinsCachePath("default", fixture.Root),
            "skinlines.json" => AppConfig.GetSkinLinesCachePath("default", fixture.Root),
            _ => AppConfig.GetItemsCachePath("default", fixture.Root)
        };

        int created = await fixture.CreateBootstrapper().PrepareAsync();

        Assert.Equal(0, created);
        Assert.False(File.Exists(path));
        AssertNoTemporaryFiles(fixture.Root);
    }

    [Fact]
    public async Task EmptyOrBrokenChampionSummaryDoesNotCreateEmptyUserChampionList()
    {
        using var fixture = new CatalogFixture();
        fixture.AddWad(JsonAsset("default", "champion-summary.json", """
            [null,42,"invalid",{"id":12,"alias":" ","name":""},{"id":19,"name":null}]
            """));

        Assert.Equal(0, await fixture.CreateBootstrapper().PrepareAsync());
        Assert.False(File.Exists(AppConfig.GetChampionsFilePath(fixture.Root)));
    }

    [Fact]
    public async Task NoInstalledOrSyntheticWadsDoesNotWriteCacheOrConfig()
    {
        using var fixture = new CatalogFixture();
        var bootstrapper = fixture.CreateBootstrapper();

        Assert.Equal(0, await bootstrapper.PrepareAsync());

        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Cache")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Config")));
    }

    [Fact]
    public async Task TwoConcurrentBootstrappersNeverReplaceEachOthersFiles()
    {
        using var fixture = new CatalogFixture();
        fixture.AddWad(BuildFullCatalog());
        var first = fixture.CreateBootstrapper();
        var second = fixture.CreateBootstrapper();

        int[] created = await Task.WhenAll(first.PrepareAsync(), second.PrepareAsync());

        Assert.Equal(11, created.Sum());
        Assert.Equal(0, await first.PrepareAsync());
        Assert.Equal(11, Directory.GetFiles(Path.Combine(fixture.Root, "Cache"), "*.json").Length +
            Directory.GetFiles(Path.Combine(fixture.Root, "Config"), "*.json").Length);
        AssertNoTemporaryFiles(fixture.Root);
    }

    [Fact]
    public async Task AlreadyCancelledTokenCannotCreateOrModifyCatalogs()
    {
        using var fixture = new CatalogFixture();
        fixture.AddWad(BuildDefaultCatalog());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.CreateBootstrapper().PrepareAsync(cancellation.Token));

        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Cache")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Config")));
    }

    private static (string Path, byte[] Data) JsonAsset(string locale, string name, string json) =>
        (WadsPrefix + locale + "/v1/" + name, Encoding.UTF8.GetBytes(json));

    private static string SummaryCachePath(string root) =>
        Path.Combine(root, "Cache", "champion_summary_data_default.json");

    private static (string Path, byte[] Data)[] BuildDefaultCatalog() =>
        new[]
        {
            JsonAsset("default", "skins.json", ValidSkins),
            JsonAsset("default", "skinlines.json", ValidSkinlines),
            JsonAsset("default", "items.json", ValidItems),
            JsonAsset("default", "champion-summary.json", ValidChampionSummary)
        };

    private static (string Path, byte[] Data)[] BuildFullCatalog() =>
        new[] { "default", "es_es", "tr_tr" }
            .SelectMany(locale => new[]
            {
                JsonAsset(locale, "skins.json", ValidSkins),
                JsonAsset(locale, "skinlines.json", ValidSkinlines),
                JsonAsset(locale, "items.json", ValidItems)
            })
            .Append(JsonAsset("default", "champion-summary.json", ValidChampionSummary))
            .ToArray();

    private static void AssertNoTemporaryFiles(string root) =>
        Assert.Empty(Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories));

    private sealed class CatalogFixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("VideoGenerator.WadBootstrap.").FullName;

        public LocalWadCatalogBootstrapper CreateBootstrapper()
        {
            var source = new LeagueWadAssetSource(new LogService(), Root, Path.Combine(Root, "IconCache"));
            return new LocalWadCatalogBootstrapper(source, new LogService(), Root);
        }

        public void WriteFile(string destination, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllBytes(destination, bytes);
        }

        public string AddWad(params (string Path, byte[] Data)[] assets)
        {
            string destination = Path.Combine(Root, "Plugins", "rcp-be-lol-game-data", "catalogs.wad.client");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            // Synthetic uncompressed WAD v3.1 with the same chunk descriptor
            // layout as LeagueWadAssetSourceTests. No game files are touched.
            using var output = File.Create(destination);
            using var writer = new BinaryWriter(output);
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
                writer.Write(asset.Data.Length);
                writer.Write(asset.Data.Length);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)0);
                writer.Write(0UL);
                offset += asset.Data.Length;
            }
            foreach (var asset in assets) writer.Write(asset.Data);
            return destination;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
