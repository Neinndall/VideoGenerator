using System.Text;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Services;

public sealed class LeagueWadHashResolverTests
{
    private const string PluginBase = "plugins/rcp-be-lol-game-data/global/";
    private const string AhriTile = "assets/characters/ahri/skins/skin07/images/ahri_splash_tile_7.jpg";

    [Theory]
    [InlineData("/lol-game-data/assets/ASSETS/Characters/Ahri/Skins/Skin07/Images/Ahri_Splash_Tile_7.JPG", AhriTile)]
    [InlineData("/lol-game-data/ASSETS/Characters/Ahri/Skins/Skin07/Images/Ahri_Splash_Tile_7.jpg", AhriTile)]
    [InlineData("ASSETS\\Characters\\Ahri\\Skins\\Skin07\\Images\\Ahri_Splash_Tile_7.jpg", AhriTile)]
    [InlineData("assets/assets/characters/ahri/skins/skin07/images/ahri_splash_tile_7.jpg", AhriTile)]
    [InlineData("Characters/Ahri/Skins/Skin07/Images/Ahri_Splash_Tile_7.jpg", AhriTile)]
    [InlineData("  /ASSETS/ITEMS/ICONS2D/3031.PNG  ", "assets/items/icons2d/3031.png")]
    [InlineData("v1/SKINS.JSON", "v1/skins.json")]
    [InlineData("PLUGINS/RCP-BE-LOL-GAME-DATA/GLOBAL/DEFAULT/V1/SKINS.JSON",
        "plugins/rcp-be-lol-game-data/global/default/v1/skins.json")]
    public void NormalizesExistingVirtualPathsIntoStableLowercaseWadPaths(string input, string expected)
    {
        Assert.Equal(expected, LeagueWadHashResolver.NormalizeVirtualPath(input));
        Assert.Equal(LeagueWadAssetSource.ComputePathHash(expected),
            LeagueWadAssetSource.ComputePathHash(LeagueWadHashResolver.NormalizeVirtualPath(input)!));
    }

    [Fact]
    public void KnownRetailSkinsJsonPathHasStableXxHash64()
    {
        const string skinsPath = "plugins/rcp-be-lol-game-data/global/default/v1/skins.json";
        const ulong expected = 0x893FFB0B2DB8ECFAUL;
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();

        var registered = resolver.Register("catalog", "skins", "v1/skins.json");

        var candidate = Assert.Single(registered);
        Assert.Equal(skinsPath, candidate.Path);
        Assert.Equal(expected, candidate.Hash);
        Assert.Equal(expected, LeagueWadAssetSource.ComputePathHash(skinsPath));
    }

    [Fact]
    public void RegistersAhriTileAsLocalizedPluginAssetThenRawGameAsset()
    {
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();

        var candidates = resolver.Register("champion", "AhriSkin7",
            "/lol-game-data/assets/ASSETS/Characters/Ahri/Skins/Skin07/Images/Ahri_Splash_Tile_7.JPG");

        Assert.Equal(new[] { PluginBase + "default/" + AhriTile, AhriTile },
            candidates.Select(value => value.Path));
        foreach (var candidate in candidates)
            Assert.Equal(LeagueWadAssetSource.ComputePathHash(candidate.Path), candidate.Hash);
        Assert.Equal(candidates, resolver.GetRegistered("CHAMPION", "AHRISKIN7"));
        Assert.Empty(resolver.GetRegistered("champion", "AhriSkin8"));
    }

    [Fact]
    public void RegistersLocalItemIconPathUsingGameDataPluginAndGameFallback()
    {
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();

        var candidates = resolver.Register("item", "3031", "ASSETS/Items/Icons2D/3031.PNG");

        Assert.Equal(new[]
        {
            PluginBase + "default/assets/items/icons2d/3031.png",
            "assets/items/icons2d/3031.png"
        }, candidates.Select(candidate => candidate.Path));
        Assert.All(candidates, candidate => Assert.NotEqual(0UL, candidate.Hash));
        Assert.Equal(candidates, resolver.GetRegistered("item", "3031"));
    }

    [Theory]
    [InlineData("ES", "es_es")]
    [InlineData("es_es", "es_es")]
    [InlineData("TR", "tr_tr")]
    [InlineData("EN", "default")]
    [InlineData("en_US", "default")]
    [InlineData("ALL", "default")]
    public void ResolvesSupportedLocalesAndFallsBackToDefaultThenUnprefixed(string requested, string expected)
    {
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();

        var candidates = resolver.Register("champion", "AhriSkin7", AhriTile, requested);

        string[] paths = expected == "default"
            ? new[] { PluginBase + "default/" + AhriTile, AhriTile }
            : new[] { PluginBase + expected + "/" + AhriTile,
                PluginBase + "default/" + AhriTile, AhriTile };
        Assert.Equal(paths, candidates.Select(candidate => candidate.Path));
        Assert.Equal(candidates, resolver.GetRegistered("champion", "AhriSkin7"));
    }

    [Fact]
    public void RegistersV1MetadataForRequestedLocaleWithDefaultFallbackButNoRawFallback()
    {
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();

        var localized = resolver.Register("catalog", "skins", "v1/skins.json", "es");

        Assert.Equal(new[]
        {
            PluginBase + "es_es/v1/skins.json",
            PluginBase + "default/v1/skins.json"
        }, localized.Select(candidate => candidate.Path));
        Assert.Equal(0x893FFB0B2DB8ECFAUL, localized[1].Hash);
    }

    [Fact]
    public void ExactPluginPathIsNotDoublePrefixedEvenWhenLocaleIsRequested()
    {
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();
        const string exact = PluginBase + "es_es/assets/items/icons2d/3031.png";

        var candidates = resolver.Register("item", "3031", exact, "es");

        Assert.Equal(exact, Assert.Single(candidates).Path);
    }

    [Fact]
    public void SameSemanticRequestNeverDuplicatesHashesAcrossVariantsAndLocales()
    {
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();

        var first = resolver.Register(" champion ", " AhriSkin7 ", AhriTile);
        var repeated = resolver.Register("CHAMPION", "AHRISKIN7",
            "/lol-game-data/assets/ASSETS/Characters/Ahri/Skins/Skin07/Images/Ahri_Splash_Tile_7.JPG",
            "EN");
        var spanish = resolver.Register("champion", "ahriskin7", AhriTile, "es");
        var repeatSpanish = resolver.Register("champion", "AhriSkin7", AhriTile, "es_es");

        Assert.Equal(2, first.Count);
        Assert.Empty(repeated);
        Assert.Equal(PluginBase + "es_es/" + AhriTile, Assert.Single(spanish).Path);
        Assert.Empty(repeatSpanish);
        var registered = resolver.GetRegistered("CHAMPION", "AHRISKIN7");
        Assert.Equal(3, registered.Count);
        Assert.Equal(3, registered.Select(candidate => candidate.Hash).Distinct().Count());
    }

    [Fact]
    public async Task ConcurrentRegistrationOfSamePathIsIdempotent()
    {
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();

        var results = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => Task.Run(() =>
            resolver.Register("champion", "AhriSkin7", AhriTile, "es"))));

        Assert.Equal(3, results.Sum(result => result.Count));
        Assert.Equal(3, resolver.GetRegistered("champion", "ahriskin7").Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("https://example.test/asset.png")]
    [InlineData("ftp://example.test/asset.png")]
    [InlineData("file:C:/Windows/asset.png")]
    [InlineData("C:\\Windows\\asset.png")]
    [InlineData("assets/characters/ahri/../lux/icon.png")]
    [InlineData("/lol-game-data/assets/ASSETS/Characters/Ahri/./tile.png")]
    [InlineData("assets/characters//ahri/icon.png")]
    [InlineData("assets/characters/ahri/icon.png?version=1")]
    [InlineData("assets/characters/ahri/icon.png#fragment")]
    public void RejectsUrlsAbsoluteDrivesTraversalAndMalformedVirtualPaths(string? invalid)
    {
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();

        Assert.Null(LeagueWadHashResolver.NormalizeVirtualPath(invalid!));
        Assert.Empty(resolver.Register("champion", "Ahri", invalid!));
        Assert.Empty(resolver.GetRegistered("champion", "Ahri"));
    }

    [Theory]
    [InlineData("unknown/asset.png")]
    [InlineData("assets")]
    [InlineData("network/asset.png")]
    public void RefusesUnsupportedAssetRoots(string input)
    {
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();

        Assert.Empty(resolver.Register("champion", "Ahri", input));
        Assert.Empty(resolver.GetRegistered("champion", "Ahri"));
    }

    [Theory]
    [InlineData("../es_es")]
    [InlineData("http://server")]
    [InlineData("xx-XX")]
    [InlineData("")]
    public void RejectsInvalidLocaleWithoutRegisteringAnything(string invalidLocale)
    {
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();

        Assert.Empty(resolver.Register("champion", "Ahri", AhriTile, invalidLocale));
        Assert.Empty(resolver.GetRegistered("champion", "Ahri"));
    }

    [Fact]
    public void EmptySemanticIconNamesDoNotRegisterCandidates()
    {
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();

        Assert.Empty(resolver.Register("", "Ahri", AhriTile));
        Assert.Empty(resolver.Register("champion", " ", AhriTile));
        Assert.Empty(resolver.GetRegistered("", "Ahri"));
        Assert.Empty(resolver.GetRegistered("champion", ""));
    }

    [Fact]
    public async Task ReadsChampionSummaryOnlyFromSyntheticLocalWadAndCachesBothAliasAndName()
    {
        using var fixture = new ResolverFixture();
        fixture.WriteWad("Plugins/rcp-be-lol-game-data/test.wad.client",
            PluginBase + "default/v1/champion-summary.json", Encoding.UTF8.GetBytes("""
                [
                  {"id":103,"alias":"Ahri","name":"Ahri"},
                  {"id":62,"alias":"MonkeyKing","name":"Wukong"},
                  {"id":0,"alias":"NotPlayable","name":"Not Playable"},
                  {"id":104,"alias":"Ahri","name":"Different Hero"},
                  null
                ]
                """));
        var resolver = fixture.CreateResolver();

        var simultaneous = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => resolver.GetChampionIdAsync("Ahri")));

        Assert.All(simultaneous, id => Assert.Equal(103, id));
        Assert.Equal(62, await resolver.GetChampionIdAsync("MonkeyKing"));
        Assert.Equal(62, await resolver.GetChampionIdAsync("wukong"));
        Assert.Equal(104, await resolver.GetChampionIdAsync("Different Hero"));
        Assert.Null(await resolver.GetChampionIdAsync("NotPlayable"));
        Assert.Null(await resolver.GetChampionIdAsync("Unknown"));
        Assert.Null(await resolver.GetChampionIdAsync(""));
    }

    [Fact]
    public async Task MalformedChampionIdIsSkippedWithoutLosingOtherValidEntries()
    {
        using var fixture = new ResolverFixture();
        fixture.WriteWad("Plugins/rcp-be-lol-game-data/invalid-id.wad.client",
            PluginBase + "default/v1/champion-summary.json", Encoding.UTF8.GetBytes("""
                [
                  {"id":"invalid","alias":"Broken"},
                  {"id":103,"alias":"Ahri","name":"Ahri"}
                ]
                """));
        var resolver = fixture.CreateResolver();

        Assert.Equal(103, await resolver.GetChampionIdAsync("Ahri"));
        Assert.Null(await resolver.GetChampionIdAsync("Broken"));
    }

    [Fact]
    public async Task MissingChampionSummaryReturnsNullOffline()
    {
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();

        Assert.Null(await resolver.GetChampionIdAsync("Ahri"));
        Assert.Null(await resolver.GetChampionIdAsync("MonkeyKing"));
    }

    [Fact]
    public async Task HonorsCancellationWhileLoadingChampionIds()
    {
        using var fixture = new ResolverFixture();
        var resolver = fixture.CreateResolver();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => resolver.GetChampionIdAsync("Ahri", cts.Token));
    }

    [Fact]
    public async Task ChampionIdsAreReloadedWhenAnInstalledWadGetsPatched()
    {
        const string summary = PluginBase + "default/v1/champion-summary.json";
        const string archive = "Plugins/rcp-be-lol-game-data/default-assets.wad";
        using var fixture = new ResolverFixture();
        fixture.WriteWad(archive, summary,
            Encoding.UTF8.GetBytes("""[{"id":103,"alias":"Ahri","name":"Ahri"}]"""));
        var source = fixture.CreateSource();
        var resolver = new LeagueWadHashResolver(source);

        Assert.Equal(103, await resolver.GetChampionIdAsync("Ahri"));
        fixture.WriteWad(archive, summary,
            Encoding.UTF8.GetBytes("""[{"id":104,"alias":"Ahri","name":"Ahri New Season"}]"""));

        Assert.True(source.RefreshArchives());
        Assert.Equal(104, await resolver.GetChampionIdAsync("Ahri"));
        Assert.Equal(104, await resolver.GetChampionIdAsync("Ahri New Season"));
    }

    private sealed class ResolverFixture : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("VideoGenerator.WadHashResolver.").FullName;

        public LeagueWadAssetSource CreateSource() =>
            new(new LogService(), _root, Path.Combine(_root, "cache"));

        public LeagueWadHashResolver CreateResolver() =>
            new(CreateSource());

        public void WriteWad(string relativePath, string assetPath, byte[] contents)
        {
            string filePath = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            using var file = File.Create(filePath);
            using var writer = new BinaryWriter(file);

            // WAD 3.1 with one uncompressed chunk. All offsets and hashes are
            // generated locally; no installed Riot client or HTTP is consulted.
            writer.Write("RW"u8);
            writer.Write((byte)3);
            writer.Write((byte)1);
            writer.Write(new byte[256]);
            writer.Write(0UL);
            writer.Write(1);
            writer.Write(LeagueWadAssetSource.ComputePathHash(assetPath));
            writer.Write((uint)(272 + 32));
            writer.Write(contents.Length);
            writer.Write(contents.Length);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)0);
            writer.Write(0UL);
            writer.Write(contents);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
