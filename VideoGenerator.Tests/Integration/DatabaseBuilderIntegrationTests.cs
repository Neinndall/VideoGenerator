using System.Text.Json;
using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class DatabaseBuilderIntegrationTests
{
    [Fact]
    public async Task FirstStartupBootstrapsSchemaCompatibleCatalogsWithoutNetwork()
    {
        string root = CreateRoot();
        try
        {
            var builder = new DatabaseBuilder(new LogService(), root);

            await builder.InitializeDatabasesAsync("15.9.1");

            Assert.True(builder.IsReady);
            await builder.ReadyTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Empty(JsonSerializer.Deserialize<List<string>>(
                await File.ReadAllTextAsync(Path.Combine(root, "Config", "champions.json")))!);
            Assert.Empty(JsonSerializer.Deserialize<Dictionary<string, string>>(
                await File.ReadAllTextAsync(Path.Combine(root, "Config", "items.json")))!);
            var monsters = JsonSerializer.Deserialize<MonsterDatabase>(
                await File.ReadAllTextAsync(Path.Combine(root, "Config", "monsters.json")))!;
            Assert.Empty(monsters.Epic);
            Assert.Empty(monsters.Large);
            Assert.Empty(JsonSerializer.Deserialize<List<StructureMapping>>(
                await File.ReadAllTextAsync(Path.Combine(root, "Config", "structures.json")))!);
            Assert.False(File.Exists(Path.Combine(root, "Config", "version.json")));
            Assert.False(Directory.Exists(Path.Combine(root, "Cache")));

            await builder.InitializeDatabasesAsync("99.9.9");
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task ExistingUserCatalogsAndVersionArePreservedByteForByte()
    {
        string root = CreateRoot();
        try
        {
            string config = Path.Combine(root, "Config");
            Directory.CreateDirectory(config);
            var originals = new Dictionary<string, string>
            {
                ["champions.json"] = "[\"MonkeyKing\",\"Wukong\",\"UserChampion\"]",
                ["items.json"] = "{\"Infinity Edge\":\"3031\",\"Personal Item\":\"1\"}",
                ["monsters.json"] = "{\"Epic\":[\"Baron Nashor\"],\"Large\":[\"Rift Herald\"]}",
                ["structures.json"] = "[{\"Keyword\":\"Blue Turret\",\"TargetName\":\"Turret\"}]",
                ["version.json"] = "14.7.1"
            };
            foreach (var entry in originals)
                await File.WriteAllTextAsync(Path.Combine(config, entry.Key), entry.Value);

            var builder = new DatabaseBuilder(new LogService(), root);
            await builder.InitializeDatabasesAsync("99.9.9");

            foreach (var entry in originals)
                Assert.Equal(entry.Value, await File.ReadAllTextAsync(Path.Combine(config, entry.Key)));
            Assert.True(builder.IsReady);
            Assert.Empty(Directory.GetFiles(config, "*.tmp"));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task BootstrapsItemNamesFromExistingLocalCacheWithoutChangingTheCache()
    {
        string root = CreateRoot();
        try
        {
            string path = AppConfig.GetItemsCachePath("default", root);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            const string json = "[{\"id\":3031,\"name\":\"Infinity Edge\"},{\"id\":3340,\"name\":\"Stealth Ward\"}]";
            await File.WriteAllTextAsync(path, json);

            var builder = new DatabaseBuilder(new LogService(), root);
            await builder.InitializeDatabasesAsync("15.9.1");

            var items = JsonSerializer.Deserialize<Dictionary<string, string>>(
                await File.ReadAllTextAsync(AppConfig.GetItemsFilePath(root)))!;
            Assert.Equal("3031", items["Infinity Edge"]);
            Assert.Equal("3340", items["Stealth Ward"]);
            Assert.Equal(json, await File.ReadAllTextAsync(path));
        }
        finally { DeleteRoot(root); }
    }

    [Theory]
    [InlineData("skins.json", "{ invalid json")]
    [InlineData("skins.json", "{\"123\":[]}")]
    [InlineData("skinlines.json", "{\"lines\":[]}")]
    [InlineData("skinlines.json", "[\"invalid entry\"]")]
    [InlineData("items.json", "{\"items\":[]}")]
    [InlineData("items.json", "[123]")]
    public async Task LeavesAllExistingCommunityDragonCachesUntouched(
        string resourceFileName, string originalContent)
    {
        string root = CreateRoot();
        try
        {
            string cachePath = Path.Combine(root, "Cache",
                $"{Path.GetFileNameWithoutExtension(resourceFileName)}_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, originalContent);

            var builder = new DatabaseBuilder(new LogService(), root);
            await builder.InitializeDatabasesAsync("15.9.1");

            Assert.True(builder.IsReady);
            Assert.Equal(originalContent, await File.ReadAllTextAsync(cachePath));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task CorruptExistingUserCatalogIsNeverOverwritten()
    {
        string root = CreateRoot();
        try
        {
            string path = AppConfig.GetMonstersFilePath(root);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            const string corruptUserData = "{ unfinished user data";
            await File.WriteAllTextAsync(path, corruptUserData);
            await new DatabaseBuilder(new LogService(), root).InitializeDatabasesAsync("15.9.1");

            Assert.Equal(corruptUserData, await File.ReadAllTextAsync(path));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task ConcurrentInitializersNeverOverwriteTheSameCatalog()
    {
        string root = CreateRoot();
        try
        {
            var first = new DatabaseBuilder(new LogService(), root);
            var second = new DatabaseBuilder(new LogService(), root);

            await Task.WhenAll(first.InitializeDatabasesAsync("15.9.1"),
                second.InitializeDatabasesAsync("99.9.9"));

            Assert.True(first.IsReady);
            Assert.True(second.IsReady);
            foreach (string file in new[] { "champions.json", "items.json", "monsters.json", "structures.json" })
            {
                using var document = JsonDocument.Parse(
                    await File.ReadAllTextAsync(Path.Combine(root, "Config", file)));
                Assert.NotEqual(JsonValueKind.Undefined, document.RootElement.ValueKind);
            }
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "Config"), "*.tmp"));
        }
        finally { DeleteRoot(root); }
    }

    private static string CreateRoot() =>
        Directory.CreateTempSubdirectory("VideoGenerator.DatabaseBuilder.").FullName;

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

}
