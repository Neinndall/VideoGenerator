using System.Text;
using VideoGenerator.Models;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class OfflineSkinCatalogTests
{
    [Theory]
    [InlineData("/lol-game-data/assets/ASSETS/Characters/Camille/Skins/Skin44/Camille Tile.PNG",
        "assets/characters/camille/skins/skin44/camille tile.png")]
    [InlineData("/lol-game-data/ASSETS/Characters/Camille/Skins/Skin44/Camille Tile.png",
        "assets/characters/camille/skins/skin44/camille tile.png")]
    [InlineData("ASSETS/Characters/Camille/Skins/Skin44/tile.tex",
        "assets/characters/camille/skins/skin44/tile.tex")]
    [InlineData("Characters\\Camille\\Skins\\Skin44\\splash.jpg",
        "assets/characters/camille/skins/skin44/splash.jpg")]
    [InlineData("assets/assets/characters/camille/skins/skin44/tile.png",
        "assets/characters/camille/skins/skin44/tile.png")]
    public void NormalizesVirtualPathsToWadHashInput(string input, string expected)
    {
        Assert.Equal(expected, OfflineSkinCatalog.NormalizeAssetPath(input));
        Assert.Equal(LeagueWadAssetSource.ComputePathHash(expected),
            LeagueWadAssetSource.ComputePathHash(OfflineSkinCatalog.NormalizeAssetPath(input)!));
    }

    [Theory]
    [InlineData("https://example.test/file.png")]
    [InlineData("../assets/characters/ahri/file.png")]
    [InlineData("assets/characters/../ahri/file.png")]
    [InlineData("assets//characters/ahri/file.png")]
    [InlineData("assets/characters/ahri/?file.png")]
    [InlineData("assets/characters/ahri/./file.png")]
    [InlineData("C:\\Riot Games\\assets\\characters\\ahri.png")]
    public void RejectsInvalidVirtualPaths(string input)
    {
        Assert.Null(OfflineSkinCatalog.NormalizeAssetPath(input));
    }

    [Fact]
    public void ResolvesTileBeforeSplashWithExactChampionSkinAndQuestTiers()
    {
        using var root = new CacheFixture();
        root.WriteSkins("default", """
            {
              "44000": {
                "name": "Camille",
                "splashPath": "/lol-game-data/assets/ASSETS/Characters/Camille/Skins/Skin0/base.jpg",
                "questSkinInfo": {
                  "tiers": [
                    {
                      "id": 44001,
                      "name": "Chosen Camille",
                      "tilePath": "/lol-game-data/assets/ASSETS/Characters/Camille/Skins/Skin44/tile.png",
                      "splashPath": "/lol-game-data/assets/ASSETS/Characters/Camille/Skins/Skin44/splash.jpg"
                    }
                  ]
                }
              },
              "wrong": {
                "tilePath": "ASSETS/Characters/CamilleX/Skins/Skin44/other.png",
                "splashPath": "ASSETS/Characters/Camille/Skins/Skin43/other.jpg"
              },
              "copy": {
                "tilePath": "assets/characters/camille/skins/skin44/TILE.PNG"
              }
            }
            """);
        var catalog = root.CreateCatalog();

        var results = catalog.GetSkinAssetPaths("Camille", 44);

        Assert.Equal(new[]
        {
            "assets/characters/camille/skins/skin44/tile.png",
            "assets/characters/camille/skins/skin44/splash.jpg"
        }, results);
        Assert.Equal("Chosen Camille", catalog.GetSkinName("camille", 44));
        Assert.Empty(catalog.GetSkinAssetPaths("CamilleX", 43));
        Assert.Empty(catalog.GetSkinAssetPaths("Camille", 45));
        root.AssertInputsUnmodified();
    }

    [Fact]
    public void RiotBaseFolderIsSkinZeroAndSplashLookupNeverReturnsTile()
    {
        using var root = new CacheFixture();
        root.WriteSkins("default", """
            {
              "266000": {
                "name": "Aatrox",
                "tilePath": "/lol-game-data/assets/ASSETS/Characters/Aatrox/Skins/Base/Images/aatrox_splash_tile_0.jpg",
                "splashPath": "/lol-game-data/assets/ASSETS/Characters/Aatrox/Skins/Base/Images/aatrox_splash_centered_0.jpg"
              }
            }
            """);
        var catalog = root.CreateCatalog();
        Assert.Equal("Aatrox", catalog.GetSkinName("Aatrox", 0));
        Assert.Equal(new[]
        {
            "assets/characters/aatrox/skins/base/images/aatrox_splash_tile_0.jpg",
            "assets/characters/aatrox/skins/base/images/aatrox_splash_centered_0.jpg"
        }, catalog.GetSkinAssetPaths("Aatrox", 0));
        Assert.Equal(new[]
        {
            "assets/characters/aatrox/skins/base/images/aatrox_splash_centered_0.jpg"
        }, catalog.GetSplashAssetPaths("Aatrox", 0));
        Assert.Empty(catalog.GetSplashAssetPaths("Aatrox", 1));
        root.AssertInputsUnmodified();
    }

    [Fact]
    public void PrefersRequestedLocaleAndFallsBackToDefaultWithoutWritingCaches()
    {
        using var root = new CacheFixture();
        root.WriteSkins("es_es", """
            {
              "es": {
                "name": "Flor de Ahri",
                "tilePath": "assets/characters/ahri/skins/skin12/local-tile.png"
              }
            }
            """);
        root.WriteSkins("default", """
            {
              "en": {
                "name": "Blossom Ahri",
                "tilePath": "assets/characters/ahri/skins/skin12/default-tile.png",
                "splashPath": "assets/characters/ahri/skins/skin12/default-splash.jpg"
              },
              "second": {
                "name": "Lux",
                "tilePath": "assets/characters/lux/skins/skin12/lux.png"
              }
            }
            """);
        var catalog = root.CreateCatalog();

        Assert.Equal(new[]
        {
            "assets/characters/ahri/skins/skin12/local-tile.png",
            "assets/characters/ahri/skins/skin12/default-tile.png",
            "assets/characters/ahri/skins/skin12/default-splash.jpg"
        }, catalog.GetSkinAssetPaths("Ahri", 12, "ES"));
        Assert.Equal("Flor de Ahri", catalog.GetSkinName("Ahri", 12, "es_es"));
        Assert.Equal("Blossom Ahri", catalog.GetSkinName("Ahri", 12, "default"));
        root.AssertInputsUnmodified();
    }

    [Fact]
    public void HandlesPunctuationAndDoesNotMixDifferentChampions()
    {
        using var root = new CacheFixture();
        root.WriteSkins("default", """
            {
              "one": {
                "name": "Bel'Veth",
                "tilePath": "assets/characters/belveth/skins/skin123/real.png",
                "splashPath": "assets/characters/ahri/skins/skin123/wrong.jpg"
              }
            }
            """);

        Assert.Equal(new[] { "assets/characters/belveth/skins/skin123/real.png" },
            root.CreateCatalog().GetSkinAssetPaths("Bel'Veth", 123));
        Assert.Empty(root.CreateCatalog().GetSkinAssetPaths("Bel'Veth", 124));
    }

    [Fact]
    public void ReadsLocalizedSkinlineNamesIdsAndMemberSkins()
    {
        using var root = new CacheFixture();
        root.WriteSkinlines("default", """
            [
              {"id": 42, "name": "Star Lantern"},
              {"id": 99, "name": "Moon Festival"}
            ]
            """);
        root.WriteSkinlines("es_es", """[{"id":42,"name":"Linterna Estelar"}]""");
        root.WriteSkins("default", """
            {
              "one": {
                "splashPath": "ASSETS/Characters/Ahri/Skins/Skin12/a.jpg",
                "skinLines": [{"id":42}, {"id":99}]
              },
              "two": {
                "tilePath": "ASSETS/Characters/Lux/Skins/Skin2/l.png",
                "skinLines": [42]
              },
              "duplicate": {
                "splashPath": "ASSETS/Characters/Ahri/Skins/Skin12/duplicate.jpg",
                "skinLines": [{"id":42}]
              }
            }
            """);
        var catalog = root.CreateCatalog();

        var skinlines = catalog.GetSkinlines("es_es");

        Assert.Equal(2, skinlines.Count);
        Assert.Equal("Linterna Estelar", skinlines[0].Name);
        Assert.Equal(42, skinlines[0].Id);
        Assert.Equal(new[] { new OfflineSkinReference("ahri", 12), new OfflineSkinReference("lux", 2) },
            skinlines[0].Skins);
        Assert.Equal("Moon Festival", skinlines[1].Name);
        Assert.Equal(new[] { new OfflineSkinReference("ahri", 12) }, skinlines[1].Skins);
        root.AssertInputsUnmodified();
    }

    [Fact]
    public void ReadsItemIdsSlugsAndNamesWithLocalePriority()
    {
        using var root = new CacheFixture();
        root.WriteItems("default", """
            [
              {"id":3031,"name":"Infinity Edge","nameSlug":"InfinityEdge"},
              {"id":3340,"name":"Stealth Ward","nameSlug":"StealthWard"}
            ]
            """);
        root.WriteItems("es_es", """
            [
              {"id":3031,"name":"Filo infinito","nameSlug":"FiloInfinito"},
              {"id":9999,"name":"Objeto personalizado","nameSlug":"ObjetoPersonalizado"}
            ]
            """);
        var catalog = root.CreateCatalog();

        var items = catalog.GetItems("es_es");

        Assert.Equal(new[] { 3031, 3340, 9999 }, items.Select(item => item.Id));
        Assert.Equal("Filo infinito", items.Single(item => item.Id == 3031).Name);
        Assert.Equal(3031, catalog.GetItemId("FiloInfinito", "es_es"));
        Assert.Equal(3340, catalog.GetItemId("Stealth Ward", "es_es"));
        Assert.Null(catalog.GetItemId("Unknown", "es_es"));
        root.AssertInputsUnmodified();
    }

    [Fact]
    public void MissingOrMalformedCachesAreNeverModifiedAndYieldEmptyResults()
    {
        using var root = new CacheFixture();
        root.WriteSkins("default", "{ malformed");
        root.WriteSkinlines("default", """{"id":42}""");
        root.WriteItems("default", """[123,"invalid",null]""");
        var catalog = root.CreateCatalog();

        Assert.Empty(catalog.GetSkinAssetPaths("Ahri", 1));
        Assert.Null(catalog.GetSkinName("Ahri", 1));
        Assert.Empty(catalog.GetSkinlines());
        Assert.Empty(catalog.GetItems());
        Assert.Empty(catalog.GetSkinAssetPaths("Ahri", 1, "../../outside"));
        root.AssertInputsUnmodified();
    }

    [Fact]
    public void ReReadsUpdatedJsonAndNeverRetainsStalePaths()
    {
        using var root = new CacheFixture();
        root.WriteSkins("default", """
            {"one":{"tilePath":"assets/characters/ahri/skins/skin3/old.png"}}
            """);
        var catalog = root.CreateCatalog();
        Assert.Equal(new[] { "assets/characters/ahri/skins/skin3/old.png" },
            catalog.GetSkinAssetPaths("Ahri", 3));

        root.WriteSkins("default", """
            {"one":{"tilePath":"assets/characters/ahri/skins/skin3/new.png"}}
            """);

        Assert.Equal(new[] { "assets/characters/ahri/skins/skin3/new.png" },
            catalog.GetSkinAssetPaths("Ahri", 3));
    }

    [Fact]
    public void AcceptsRawWadJsonBytesWithoutReadingOrWritingAnyDiskCache()
    {
        using var root = new CacheFixture();
        var catalog = root.CreateCatalog();
        const string json = """
            {
              "ahri": {
                "tilePath": "/lol-game-data/assets/ASSETS/Characters/Ahri/Skins/Skin27/tile.tex",
                "splashPath": "ASSETS/Characters/Ahri/Skins/Skin27/splash.jpg"
              }
            }
            """;
        var bytes = Encoding.UTF8.GetBytes(json);

        Assert.Equal(new[]
        {
            "assets/characters/ahri/skins/skin27/tile.tex",
            "assets/characters/ahri/skins/skin27/splash.jpg"
        }, catalog.GetSkinAssetPathsFromJson(bytes, "Ahri", 27));
        Assert.Empty(catalog.GetSkinAssetPaths("Ahri", 27));
        Assert.Empty(catalog.GetSkinAssetPathsFromJson(Encoding.UTF8.GetBytes("bad"), "Ahri", 27));
        Assert.False(Directory.Exists(Path.Combine(root.Root, "Cache")));
    }

    private sealed class CacheFixture : IDisposable
    {
        private readonly Dictionary<string, string> _originals = new(StringComparer.OrdinalIgnoreCase);
        public string Root { get; } = Directory.CreateTempSubdirectory("VideoGenerator.OfflineSkins.").FullName;

        public OfflineSkinCatalog CreateCatalog() => new(Root);

        public void WriteSkins(string locale, string json) =>
            Write(AppConfig.GetSkinsCachePath(locale, Root), json);

        public void WriteSkinlines(string locale, string json) =>
            Write(AppConfig.GetSkinLinesCachePath(locale, Root), json);

        public void WriteItems(string locale, string json) =>
            Write(AppConfig.GetItemsCachePath(locale, Root), json);

        private void Write(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
            _originals[path] = json;
        }

        public void AssertInputsUnmodified()
        {
            foreach (var entry in _originals)
                Assert.Equal(entry.Value, File.ReadAllText(entry.Key));
            Assert.Equal(_originals.Count, Directory.GetFiles(Path.Combine(Root, "Cache")).Length);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
