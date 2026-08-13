using System.Net;
using System.Net.Http;
using System.Text.Json;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class DatabaseBuilderIntegrationTests
{
    [Fact]
    public async Task InitializesAllDatabasesInAnIsolatedRootAndOnlyOnce()
    {
        string root = CreateRoot();

        try
        {
            var handler = new StubHandler(CreateSuccessfulResponse);
            using var httpClient = new HttpClient(handler);
            var builder = new DatabaseBuilder(httpClient, new LogService(), root);

            await builder.InitializeDatabasesAsync("15.9.1");

            Assert.True(builder.IsReady);
            await builder.ReadyTask;
            Assert.Equal(8, handler.RequestCount);

            string configDirectory = Path.Combine(root, "Config");
            string cacheDirectory = Path.Combine(root, "Cache");
            var champions = JsonSerializer.Deserialize<List<string>>(
                await File.ReadAllTextAsync(Path.Combine(configDirectory, "champions.json")));
            var items = JsonSerializer.Deserialize<Dictionary<string, string>>(
                await File.ReadAllTextAsync(Path.Combine(configDirectory, "items.json")));
            var monsters = JsonSerializer.Deserialize<MonsterDatabase>(
                await File.ReadAllTextAsync(Path.Combine(configDirectory, "monsters.json")));
            var structures = JsonSerializer.Deserialize<List<StructureMapping>>(
                await File.ReadAllTextAsync(Path.Combine(configDirectory, "structures.json")));
            Assert.NotNull(structures);
            var loadedStructures = structures!;

            Assert.Contains("MonkeyKing", champions!);
            Assert.Contains("Wukong", champions!);
            Assert.Equal("3031", items!["Infinity Edge"]);
            Assert.Contains("Baron Nashor", monsters!.Epic);
            Assert.Contains("Rift Herald", monsters.Large);
            Assert.Equal("Turret", loadedStructures.Single(s => s.Keyword == "Blue Turret").TargetName);
            Assert.Equal("Nexus", loadedStructures.Single(s => s.Keyword == "Nexus").TargetName);
            Assert.Equal("15.9.1", await File.ReadAllTextAsync(Path.Combine(configDirectory, "version.json")));
            Assert.True(File.Exists(Path.Combine(cacheDirectory, "skins_data_default.json")));
            Assert.True(File.Exists(Path.Combine(cacheDirectory, "skinlines_data_default.json")));
            Assert.True(File.Exists(Path.Combine(cacheDirectory, "items_data_default.json")));

            await builder.InitializeDatabasesAsync("15.9.1");

            Assert.Equal(8, handler.RequestCount);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task MarksInitializationReadyWhenNetworkSynchronizationFails()
    {
        string root = CreateRoot();

        try
        {
            using var httpClient = new HttpClient(new ThrowingHandler());
            var builder = new DatabaseBuilder(httpClient, new LogService(), root);

            await builder.InitializeDatabasesAsync("15.9.1");

            Assert.True(builder.IsReady);
            await builder.ReadyTask;
            Assert.True(File.Exists(Path.Combine(root, "Config", "version.json")));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static HttpResponseMessage CreateSuccessfulResponse(HttpRequestMessage request)
    {
        string url = Uri.UnescapeDataString(request.RequestUri!.ToString());

        if (url.Contains("/champion.json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonResponse("{\"data\":{\"Aatrox\":{\"name\":\"Aatrox\"},\"MonkeyKing\":{\"name\":\"Wukong\"}}}");
        }

        if (url.Contains("/item.json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonResponse("{\"data\":{\"3031\":{\"name\":\"Infinity Edge\"}}}");
        }

        if (url.Contains("/skins.json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonResponse("{}");
        }

        if (url.Contains("/skinlines.json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonResponse("[]");
        }

        if (url.Contains("/items.json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonResponse("[]");
        }

        if (url.Contains("Category:Epic_monsters", StringComparison.OrdinalIgnoreCase))
        {
            return JsonResponse("{\"query\":{\"categorymembers\":[{\"title\":\"Baron Nashor/LoL\"},{\"title\":\"Dragon camp\"}]}}");
        }

        if (url.Contains("Category:Large_monsters", StringComparison.OrdinalIgnoreCase))
        {
            return JsonResponse("{\"query\":{\"categorymembers\":[{\"title\":\"Rift Herald\"}]}}");
        }

        if (url.Contains("Category:Structures", StringComparison.OrdinalIgnoreCase))
        {
            return JsonResponse("{\"query\":{\"categorymembers\":[{\"title\":\"Blue Turret\"},{\"title\":\"Nexus\"}]}}");
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };

    private static string CreateRoot() =>
        Directory.CreateTempSubdirectory("VideoGenerator.DatabaseBuilder.").FullName;

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(
                new HttpRequestException("Network access is disabled for this test."));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;
        private int _requestCount;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public int RequestCount => _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(_responseFactory(request));
        }
    }
}
