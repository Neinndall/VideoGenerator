using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class DataFetcherTests
{
    [Fact]
    public async Task LoadsQuestSkinTiersFromAnIsolatedCache()
    {
        string root = CreateRoot();

        try
        {
            string cachePath = Path.Combine(root, "Cache", "skins_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, """
                {
                  "100": {
                    "name": "Base Skin",
                    "questSkinInfo": {
                      "tiers": [
                        { "id": 101, "name": "Tier Skin" }
                      ]
                    }
                  }
                }
                """);

            var fetcher = new DataFetcher(new LogService(), root);

            var skins = await fetcher.GetSkinsDataAsync("EN");

            Assert.Contains("100", skins.Keys);
            Assert.Contains("101", skins.Keys);
            Assert.Equal("Tier Skin", skins["101"].GetProperty("name").GetString());
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ResolvesItemsFromAnIsolatedCommunityDragonCache()
    {
        string root = CreateRoot();

        try
        {
            string cachePath = Path.Combine(root, "Cache", "items_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, """
                [
                  { "id": 3031, "name": "Infinity Edge", "nameSlug": "InfinityEdge" },
                  { "id": 3340, "name": "Stealth Ward", "nameSlug": "StealthWard" }
                ]
                """);

            var fetcher = new DataFetcher(new LogService(), root);

            var exact = await fetcher.GetItemInfoAsync("InfinityEdge", "EN");
            string normalized = await fetcher.ResolveItemNameToIdAsync("Stealth Ward");
            string numeric = await fetcher.ResolveItemNameToIdAsync("3031");

            Assert.True(exact.HasValue);
            Assert.Equal(3031, exact.Value.Id);
            Assert.Equal("Infinity Edge", exact.Value.Name);
            Assert.Equal("3340", normalized);
            Assert.Equal("3031", numeric);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData("ES")]
    [InlineData("TR")]
    public async Task MissingLocalizedItemsCacheFallsBackToDefaultWithoutNetwork(string language)
    {
        string root = CreateRoot();
        try
        {
            string cachePath = Path.Combine(root, "Cache", "items_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, """
                [{ "id": 3340, "name": "Stealth Ward", "nameSlug": "StealthWard" }]
                """);

            var fetcher = new DataFetcher(new LogService(), root);
            var ward = await fetcher.GetItemInfoAsync("StealthWard", language);

            Assert.True(ward.HasValue);
            Assert.Equal(3340, ward.Value.Id);
            Assert.Equal("Stealth Ward", ward.Value.Name);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task LocalizedItemsOverrideNamesButRetainMissingDefaultItemIdsAndEnglishAliases()
    {
        string root = CreateRoot();
        try
        {
            string cacheDirectory = Path.Combine(root, "Cache");
            Directory.CreateDirectory(cacheDirectory);
            await File.WriteAllTextAsync(Path.Combine(cacheDirectory, "items_data_default.json"), """
                [
                  { "id": 3031, "name": "Infinity Edge", "nameSlug": "InfinityEdge" },
                  { "id": 3340, "name": "Stealth Ward", "nameSlug": "StealthWard" }
                ]
                """);
            await File.WriteAllTextAsync(Path.Combine(cacheDirectory, "items_data_es_es.json"), """
                [
                  { "id": 3031, "name": "Filo Infinito", "nameSlug": "FiloInfinito" },
                  { "id": 9999, "name": "Artefacto", "nameSlug": "Artefacto" }
                ]
                """);

            var fetcher = new DataFetcher(new LogService(), root);
            var englishAlias = await fetcher.GetItemInfoAsync("InfinityEdge", "ES");
            var spanishAlias = await fetcher.GetItemInfoAsync("FiloInfinito", "ES");
            var defaultOnly = await fetcher.GetItemInfoAsync("StealthWard", "ES");
            var spanishOnly = await fetcher.GetItemInfoAsync("Artefacto", "ES");

            Assert.Equal(3031, englishAlias?.Id);
            Assert.Equal("Filo Infinito", englishAlias?.Name);
            Assert.Equal(3031, spanishAlias?.Id);
            Assert.Equal(3340, defaultOnly?.Id);
            Assert.Equal(9999, spanishOnly?.Id);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task MalformedLocalizedItemsCacheDoesNotHideValidDefaultCatalog()
    {
        string root = CreateRoot();
        try
        {
            string cacheDirectory = Path.Combine(root, "Cache");
            Directory.CreateDirectory(cacheDirectory);
            await File.WriteAllTextAsync(Path.Combine(cacheDirectory, "items_data_default.json"), """
                [{ "id": 1001, "name": "Boots", "nameSlug": "Boots" }]
                """);
            await File.WriteAllTextAsync(Path.Combine(cacheDirectory, "items_data_es_es.json"), "{invalid");

            var fetcher = new DataFetcher(new LogService(), root);
            var boots = await fetcher.GetItemInfoAsync("Boots", "ES");

            Assert.Equal(1001, boots?.Id);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task MalformedItemRowsDoNotDiscardFollowingValidItemRecords()
    {
        string root = CreateRoot();
        try
        {
            string cachePath = Path.Combine(root, "Cache", "items_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, """
                [
                  null,
                  { "id": "not-an-id", "name": "Bad Item" },
                  { "id": 999999999999, "name": "Out-of-range ID" },
                  { "id": 1001, "name": "Boots", "nameSlug": "Boots" }
                ]
                """);

            var fetcher = new DataFetcher(new LogService(), root);
            Assert.Equal(1001, (await fetcher.GetItemInfoAsync("Boots", "EN"))?.Id);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void LoadsLegacyMonsterListsFromAnIsolatedConfig()
    {
        string root = CreateRoot();

        try
        {
            string databasePath = Path.Combine(root, "Config", "monsters.json");
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            File.WriteAllText(databasePath, "[\"Baron Nashor\", \"Dragon\"]");

            var fetcher = new DataFetcher(new LogService(), root);

            MonsterDatabase database = fetcher.LoadMonsterDatabase();

            Assert.Equal(new[] { "Baron Nashor", "Dragon" }, database.Large);
            Assert.Empty(database.Epic);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task AcceptsValidCachedIconWithoutAnyNetworkClient()
    {
        string root = CreateRoot();

        try
        {
            string iconPath = Path.Combine(root, "Cache", "IconCache", "item", "cached.png");
            Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);
            using (var image = new Image<Rgba32>(2, 2))
                image.SaveAsPng(iconPath);

            Assert.True(await DataFetcher.IsUsableCachedIconAsync(iconPath));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task RejectsInvalidMissingOrEmptyCachedImagesOffline()
    {
        string root = CreateRoot();
        try
        {
            string iconDirectory = Path.Combine(root, "Cache", "IconCache", "item");
            Directory.CreateDirectory(iconDirectory);
            string html = Path.Combine(iconDirectory, "fake.png");
            string empty = Path.Combine(iconDirectory, "empty.png");
            string truncated = Path.Combine(iconDirectory, "truncated.png");
            await File.WriteAllTextAsync(html, "<html>Not an image</html>");
            await File.WriteAllBytesAsync(empty, Array.Empty<byte>());
            await File.WriteAllBytesAsync(truncated, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

            Assert.False(await DataFetcher.IsUsableCachedIconAsync(html));
            Assert.False(await DataFetcher.IsUsableCachedIconAsync(empty));
            Assert.False(await DataFetcher.IsUsableCachedIconAsync(truncated));
            Assert.False(await DataFetcher.IsUsableCachedIconAsync(Path.Combine(root, "absent.png")));
            Assert.False(await DataFetcher.IsUsableCachedIconAsync(null!));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ConstructorDoesNotRequireHttpClientAndMissingCachesRemainOffline()
    {
        string root = CreateRoot();
        try
        {
            var fetcher = new DataFetcher(new LogService(), root);

            Assert.Empty(await fetcher.GetSkinsDataAsync("EN"));
            Assert.Empty(await fetcher.GetSkinLinesAsync("EN"));
            Assert.Null(await fetcher.GetItemInfoAsync("UnknownItem", "EN"));
            Assert.Null(await fetcher.ResolveItemNameToIdAsync("UnknownItem"));
            Assert.Empty(fetcher.LoadMonsterDatabase().All);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task LoadsSkinAndSkinlineCachesByLocaleWithoutNetwork()
    {
        string root = CreateRoot();
        try
        {
            string cache = Path.Combine(root, "Cache");
            Directory.CreateDirectory(cache);
            await File.WriteAllTextAsync(Path.Combine(cache, "skins_data_es_es.json"),
                """{"1001":{"name":"Aspecto ES"}}""");
            await File.WriteAllTextAsync(Path.Combine(cache, "skins_data_default.json"),
                """{"1001":{"name":"Skin EN"}}""");
            await File.WriteAllTextAsync(Path.Combine(cache, "skinlines_data_es_es.json"),
                """[{"id":44,"name":"Temática ES"}]""");
            await File.WriteAllTextAsync(Path.Combine(cache, "skinlines_data_default.json"),
                """[{"id":44,"name":"Theme EN"}]""");

            var fetcher = new DataFetcher(new LogService(), root);
            Assert.Equal("Aspecto ES", (await fetcher.GetSkinsDataAsync("ES"))["1001"].GetProperty("name").GetString());
            Assert.Equal("Skin EN", (await fetcher.GetSkinsDataAsync("EN"))["1001"].GetProperty("name").GetString());
            Assert.Equal("Temática ES", (await fetcher.GetSkinLinesAsync("ES"))[0].GetProperty("name").GetString());
            Assert.Equal("Theme EN", (await fetcher.GetSkinLinesAsync("EN"))[0].GetProperty("name").GetString());
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task InvalidateCachesReloadsUpdatedSkinsSkinlinesAndItems()
    {
        string root = CreateRoot();
        try
        {
            string cache = Path.Combine(root, "Cache");
            Directory.CreateDirectory(cache);
            string skinsPath = Path.Combine(cache, "skins_data_default.json");
            string linesPath = Path.Combine(cache, "skinlines_data_default.json");
            string itemsPath = Path.Combine(cache, "items_data_default.json");
            await File.WriteAllTextAsync(skinsPath, """{"100":{"name":"Old Skin"}}""");
            await File.WriteAllTextAsync(linesPath, """[{"id":1,"name":"Old Line"}]""");
            await File.WriteAllTextAsync(itemsPath,
                """[{"id":1001,"name":"Old Item","nameSlug":"OldItem"}]""");

            var fetcher = new DataFetcher(new LogService(), root);
            Assert.Equal("Old Skin", (await fetcher.GetSkinsDataAsync("EN"))["100"].GetProperty("name").GetString());
            Assert.Equal("Old Line", (await fetcher.GetSkinLinesAsync("EN"))[0].GetProperty("name").GetString());
            Assert.Equal("1001", await fetcher.ResolveItemNameToIdAsync("OldItem"));

            await File.WriteAllTextAsync(skinsPath, """{"200":{"name":"New Skin"}}""");
            await File.WriteAllTextAsync(linesPath, """[{"id":2,"name":"New Line"}]""");
            await File.WriteAllTextAsync(itemsPath,
                """[{"id":2002,"name":"New Item","nameSlug":"NewItem"}]""");

            // Existing lookups stay memoized until the game patch is synchronized.
            Assert.Contains("100", (await fetcher.GetSkinsDataAsync("EN")).Keys);
            Assert.Equal("Old Line", (await fetcher.GetSkinLinesAsync("EN"))[0].GetProperty("name").GetString());
            Assert.Equal("1001", await fetcher.ResolveItemNameToIdAsync("OldItem"));

            fetcher.InvalidateCaches();

            var skins = await fetcher.GetSkinsDataAsync("EN");
            Assert.DoesNotContain("100", skins.Keys);
            Assert.Equal("New Skin", skins["200"].GetProperty("name").GetString());
            Assert.Equal("New Line", (await fetcher.GetSkinLinesAsync("EN"))[0].GetProperty("name").GetString());
            Assert.Equal("2002", await fetcher.ResolveItemNameToIdAsync("NewItem"));
            Assert.Null(await fetcher.GetItemInfoAsync("OldItem", "EN"));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task InvalidateCachesAlsoReloadsLegacyItemFallback()
    {
        string root = CreateRoot();
        try
        {
            string config = Path.Combine(root, "Config");
            Directory.CreateDirectory(config);
            string itemsPath = Path.Combine(config, "items.json");
            await File.WriteAllTextAsync(itemsPath, """{"Old Artifact":"9001"}""");
            var fetcher = new DataFetcher(new LogService(), root);
            Assert.Equal("9001", await fetcher.ResolveItemNameToIdAsync("OldArtifact"));

            await File.WriteAllTextAsync(itemsPath, """{"New Artifact":"9002"}""");
            fetcher.InvalidateCaches();

            Assert.Null(await fetcher.ResolveItemNameToIdAsync("OldArtifact"));
            Assert.Equal("9002", await fetcher.ResolveItemNameToIdAsync("NewArtifact"));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ResolvesItemsFromLegacyLocalFileWhenItemCacheIsMissing()
    {
        string root = CreateRoot();
        try
        {
            string config = Path.Combine(root, "Config");
            Directory.CreateDirectory(config);
            await File.WriteAllTextAsync(Path.Combine(config, "items.json"),
                """{"Zhonya's Hourglass":"3157","Guardian Angel":"3026"}""");

            var fetcher = new DataFetcher(new LogService(), root);
            Assert.Equal("3157", await fetcher.ResolveItemNameToIdAsync("ZhonyasHourglass"));
            Assert.Equal("3026", await fetcher.ResolveItemNameToIdAsync("Guardian Angel"));
            Assert.Equal("3026", await fetcher.ResolveItemNameToIdAsync("3026"));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CorruptLocalDatabasesNeverTriggerNetworkRequests()
    {
        string root = CreateRoot();
        try
        {
            string cache = Path.Combine(root, "Cache");
            Directory.CreateDirectory(cache);
            await File.WriteAllTextAsync(Path.Combine(cache, "skins_data_default.json"), "{invalid");
            await File.WriteAllTextAsync(Path.Combine(cache, "skinlines_data_default.json"), "invalid");
            await File.WriteAllTextAsync(Path.Combine(cache, "items_data_default.json"), "{invalid");

            var fetcher = new DataFetcher(new LogService(), root);
            Assert.Empty(await fetcher.GetSkinsDataAsync("EN"));
            Assert.Empty(await fetcher.GetSkinLinesAsync("EN"));
            Assert.Null(await fetcher.GetItemInfoAsync("Sword", "EN"));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void LoadsStructuredMonsterDatabaseFromLocalConfig()
    {
        string root = CreateRoot();
        try
        {
            string config = Path.Combine(root, "Config");
            Directory.CreateDirectory(config);
            File.WriteAllText(Path.Combine(config, "monsters.json"),
                """{"Epic":["Baron Nashor","Elder Dragon"],"Large":["Blue Sentinel"]}""");

            var fetcher = new DataFetcher(new LogService(), root);
            var result = fetcher.LoadMonsterDatabase();
            Assert.Equal(new[] { "Baron Nashor", "Elder Dragon" }, result.Epic);
            Assert.Equal(new[] { "Blue Sentinel" }, result.Large);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static string CreateRoot() =>
        Directory.CreateTempSubdirectory("VideoGenerator.DataFetcher.").FullName;

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

}
