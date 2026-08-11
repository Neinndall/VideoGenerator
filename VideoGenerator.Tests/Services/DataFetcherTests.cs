using System.Net;
using System.Net.Http;
using System.Text.Json;
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

            using var httpClient = new HttpClient(new ThrowingHandler());
            var fetcher = new DataFetcher(httpClient, new LogService(), root);

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

            using var httpClient = new HttpClient(new ThrowingHandler());
            var fetcher = new DataFetcher(httpClient, new LogService(), root);

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

    [Fact]
    public void LoadsLegacyMonsterListsFromAnIsolatedConfig()
    {
        string root = CreateRoot();

        try
        {
            string databasePath = Path.Combine(root, "Config", "monsters.json");
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            File.WriteAllText(databasePath, "[\"Baron Nashor\", \"Dragon\"]");

            using var httpClient = new HttpClient(new ThrowingHandler());
            var fetcher = new DataFetcher(httpClient, new LogService(), root);

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
    public async Task UsesCachedIconsWithoutCallingTheNetwork()
    {
        string root = CreateRoot();

        try
        {
            string iconPath = Path.Combine(root, "Cache", "IconCache", "item", "cached.png");
            Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);
            await File.WriteAllBytesAsync(iconPath, new byte[] { 1, 2, 3 });

            var handler = new ThrowingHandler();
            using var httpClient = new HttpClient(handler);
            var fetcher = new DataFetcher(httpClient, new LogService(), root);

            string resolvedPath = await fetcher.DownloadIconAsync(
                "https://example.test/cached.png?version=1",
                "item");

            Assert.Equal(iconPath, resolvedPath);
            Assert.Equal(0, handler.RequestCount);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CachesTheLatestLeagueVersionAfterAValidResponse()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[\"15.9.1\", \"15.9.0\"]")
        });
        using var httpClient = new HttpClient(handler);
        var fetcher = new DataFetcher(httpClient, new LogService());

        string first = await fetcher.GetLatestLolVersionAsync();
        string second = await fetcher.GetLatestLolVersionAsync();

        Assert.Equal("15.9.1", first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task UsesTheFallbackVersionWhenTheVersionRequestFails()
    {
        using var httpClient = new HttpClient(new ThrowingHandler());
        var fetcher = new DataFetcher(httpClient, new LogService());

        string version = await fetcher.GetLatestLolVersionAsync();

        Assert.Equal("14.1.1", version);
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

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromException<HttpResponseMessage>(
                new HttpRequestException("Network access is disabled for this test."));
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
