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
            Assert.False(File.Exists(Path.Combine(root, "Config", "champions.json")));
            Assert.False(File.Exists(Path.Combine(root, "Config", "items.json")));
            var monsters = JsonSerializer.Deserialize<MonsterDatabase>(
                await File.ReadAllTextAsync(AppConfig.GetMonstersFilePath(root)))!;
            Assert.Equal(DefaultMonsters.Get().Epic, monsters.Epic);
            Assert.Equal(DefaultMonsters.Get().Large, monsters.Large);
            var structures = JsonSerializer.Deserialize<List<StructureMapping>>(
                await File.ReadAllTextAsync(Path.Combine(root, "Config", "structures.json")))!;
            Assert.Equal(DefaultStructures.Entries.Count, structures.Count);
            Assert.False(File.Exists(Path.Combine(root, "Config", "version.json")));
            Assert.False(Directory.Exists(Path.Combine(root, "Cache")));

            await builder.InitializeDatabasesAsync("99.9.9");
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task ExistingMonsterAndStructureCatalogsMergeNewDefaultsWithoutLosingCustomEntries()
    {
        string root = CreateRoot();
        try
        {
            string config = Path.Combine(root, "Config");
            Directory.CreateDirectory(config);
            var originals = new Dictionary<string, string>
            {
                ["monsters.json"] = "{\"Epic\":[\"My Custom Boss\"],\"Large\":[\"Rift Herald\",\"Dinosaurio\"]}",
                ["structures.json"] = "[{\"Keyword\":\"ObsidianGate\",\"TargetName\":\"Nexus\"}]",
                ["version.json"] = "14.7.1"
            };
            foreach (var entry in originals)
                await File.WriteAllTextAsync(Path.Combine(config, entry.Key), entry.Value);

            var builder = new DatabaseBuilder(new LogService(), root);
            await builder.InitializeDatabasesAsync("99.9.9");

            foreach (string name in new[] { "version.json" })
                Assert.Equal(originals[name], await File.ReadAllTextAsync(Path.Combine(config, name)));
            Assert.False(File.Exists(Path.Combine(config, "items.json")));
            var mergedMonsters = JsonSerializer.Deserialize<MonsterDatabase>(
                await File.ReadAllTextAsync(AppConfig.GetMonstersFilePath(root)))!;
            Assert.Contains("Dinosaurio", mergedMonsters.Large);
            Assert.Contains("My Custom Boss", mergedMonsters.Epic);
            Assert.Contains("Rift Herald", mergedMonsters.Large);
            Assert.Contains("Atakhan", mergedMonsters.Epic);
            Assert.Equal(DefaultMonsters.Entries.Count + 2, mergedMonsters.All.Count);
            var structures = JsonSerializer.Deserialize<List<StructureMapping>>(
                await File.ReadAllTextAsync(Path.Combine(config, "structures.json")))!;
            Assert.Contains(structures, s => s.Keyword == "ObsidianGate" && s.TargetName == "Nexus");
            Assert.Contains(structures, s => s.Keyword == "RedTurret" && s.TargetName == "Turret");
            Assert.Equal(DefaultStructures.Entries.Count + 1, structures.Count);
            Assert.True(builder.IsReady);
            Assert.Empty(Directory.GetFiles(config, "*.tmp"));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task FullyMergedCatalogsAreNotRewrittenOnSubsequentStartup()
    {
        string root = CreateRoot();
        try
        {
            await new DatabaseBuilder(new LogService(), root).InitializeDatabasesAsync("1");
            string monsterPath = AppConfig.GetMonstersFilePath(root);
            string structurePath = AppConfig.GetStructuresFilePath(root);
            string monsterContent = await File.ReadAllTextAsync(monsterPath);
            string structureContent = await File.ReadAllTextAsync(structurePath);

            await new DatabaseBuilder(new LogService(), root).InitializeDatabasesAsync("2");

            Assert.Equal(monsterContent, await File.ReadAllTextAsync(monsterPath));
            Assert.Equal(structureContent, await File.ReadAllTextAsync(structurePath));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task InvalidMonsterJsonIsPreservedWithoutCreatingAnotherDatabase()
    {
        string root = CreateRoot();
        try
        {
            string canonical = AppConfig.GetMonstersFilePath(root);
            Directory.CreateDirectory(Path.GetDirectoryName(canonical)!);
            const string damaged = "{ user data was interrupted";
            await File.WriteAllTextAsync(canonical, damaged);

            await new DatabaseBuilder(new LogService(), root).InitializeDatabasesAsync("1");

            Assert.Equal(damaged, await File.ReadAllTextAsync(canonical));
            Assert.False(File.Exists(Path.Combine(root, "Config", "MonsterDatabase.json")));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task ExistingUnifiedJsonGetsNewDefaultsWithoutLosingUserMappings()
    {
        string root = CreateRoot();
        try
        {
            string monster = AppConfig.GetMonstersFilePath(root);
            string structure = AppConfig.GetStructuresFilePath(root);
            Directory.CreateDirectory(Path.GetDirectoryName(monster)!);
            await File.WriteAllTextAsync(monster,
                """{"Epic":["My Custom Boss"],"Large":["Dinosaurio"]}""");
            await File.WriteAllTextAsync(structure,
                """[{"Keyword":"ObsidianGate","TargetName":"Nexus"}]""");

            await new DatabaseBuilder(new LogService(), root).InitializeDatabasesAsync("1");

            var saved = JsonSerializer.Deserialize<MonsterDatabase>(
                await File.ReadAllTextAsync(monster))!;
            Assert.Contains("My Custom Boss", saved.Epic);
            Assert.Contains("Dinosaurio", saved.Large);
            Assert.Contains("Atakhan", saved.Epic);
            Assert.Equal(DefaultMonsters.Entries.Count + 2, saved.All.Count);
            var structures = JsonSerializer.Deserialize<List<StructureMapping>>(
                await File.ReadAllTextAsync(structure))!;
            Assert.Equal(DefaultStructures.Entries.Count + 1, structures.Count);
            Assert.Contains(structures, item => item.Keyword == "ObsidianGate");
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task DoesNotCopyRiotItemCacheIntoConfig()
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

            Assert.False(File.Exists(Path.Combine(root, "Config", "items.json")));
            var fetcher = new DataFetcher(new LogService(), root);
            Assert.Equal("3031", await fetcher.ResolveItemNameToIdAsync("InfinityEdge"));
            Assert.Equal("3340", await fetcher.ResolveItemNameToIdAsync("StealthWard"));
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
            Assert.False(File.Exists(Path.Combine(root, "Config", "champions.json")));
            Assert.False(File.Exists(Path.Combine(root, "Config", "items.json")));
            foreach (string file in new[] { "monsters.json", "structures.json" })
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
