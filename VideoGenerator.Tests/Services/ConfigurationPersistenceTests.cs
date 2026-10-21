using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class ConfigurationPersistenceTests
{
    [Theory]
    [InlineData("{ broken data")]
    [InlineData("null")]
    [InlineData("[{\"Name\":null,\"Category\":\"Region\",\"ChampionsRaw\":\"Ahri\"}]")]
    public void InvalidGroupCatalogNeverGetsReplacedWithDefaults(string corrupt)
    {
        string root = CreateRoot();
        try
        {
            string path = Path.Combine(root, "groups.json");
            File.WriteAllText(path, corrupt);
            var manager = new GroupManager(new LogService(), path);
            Assert.Contains(manager.Groups, item => item.Name == "Demacia");
            Assert.False(manager.SaveGroups());
            Assert.Equal(corrupt, File.ReadAllText(path));
        }
        finally { DeleteRoot(root); }
    }

    [Theory]
    [InlineData("{ broken data")]
    [InlineData("null")]
    [InlineData("[{\"DisplayName\":\"Ahri\",\"InternalName\":null}]")]
    public void InvalidChampionAliasesNeverGetReplacedWithDefaults(string corrupt)
    {
        string root = CreateRoot();
        try
        {
            string path = Path.Combine(root, "champion_aliases.json");
            File.WriteAllText(path, corrupt);
            var manager = new AliasManager(new LogService(), path);
            Assert.Contains(manager.Aliases, item => item.DisplayName == "Wukong");
            Assert.False(manager.SaveAliases());
            Assert.Equal(corrupt, File.ReadAllText(path));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public void DefaultGroupAndChampionAliasesPersistOnlyWhenChangesExist()
    {
        string root = CreateRoot();
        try
        {
            string groupsPath = Path.Combine(root, "groups.json");
            string aliasesPath = Path.Combine(root, "champion_aliases.json");
            var groups = new GroupManager(new LogService(), groupsPath);
            var aliases = new AliasManager(new LogService(), aliasesPath);
            string groupsJson = File.ReadAllText(groupsPath);
            string aliasesJson = File.ReadAllText(aliasesPath);
            Assert.NotEmpty(groups.Groups);
            Assert.NotEmpty(aliases.Aliases);
            new GroupManager(new LogService(), groupsPath);
            new AliasManager(new LogService(), aliasesPath);
            Assert.Equal(groupsJson, File.ReadAllText(groupsPath));
            Assert.Equal(aliasesJson, File.ReadAllText(aliasesPath));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public void ChampionsFromBuiltInGroupsAppearInSingleMappingCatalog()
    {
        string root = CreateRoot();
        try
        {
            string aliases = Path.Combine(root, "champion_aliases.json");
            var manager = new AliasManager(new LogService(), aliases);
            Assert.Contains(manager.Aliases, a => a.DisplayName == "Aatrox" &&
                a.IsOfficial);
            Assert.Contains(manager.Aliases, a => a.DisplayName == "Wukong" &&
                a.InternalName == "MonkeyKing");
            Assert.True(manager.IsValidChampion("Aatrox"));
            Assert.Equal("MonkeyKing", manager.GetInternalName("Wukong"));
            Assert.True(File.Exists(aliases));
            Assert.False(File.Exists(Path.Combine(root, "champions.json")));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public void RiotSummaryAddsChampionAndUpdatesWithoutASecondConfigJson()
    {
        string root = CreateRoot();
        try
        {
            string aliasPath = Path.Combine(root, "Config", "champion_aliases.json");
            string cache = Path.Combine(root, "Cache", "champion_summary_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
            File.WriteAllText(cache, """
                [{"id":62,"name":"Wukong","alias":"MonkeyKing"},
                 {"id":901,"name":"New Champion","alias":"NewChampion"}]
                """);
            var manager = new AliasManager(new LogService(), aliasPath, cache);
            Assert.True(manager.IsValidChampion("New Champion"));
            Assert.Equal("NewChampion", manager.GetInternalName("New Champion"));
            Assert.Contains(manager.Aliases, x => x.DisplayName == "New Champion" && x.IsOfficial);
            Assert.False(File.Exists(Path.Combine(root, "Config", "champions.json")));

            File.WriteAllText(cache, """
                [{"id":901,"name":"New Champion","alias":"NewChampion"},
                 {"id":902,"name":"Another Champion","alias":"AnotherChampion"}]
                """);
            File.SetLastWriteTimeUtc(cache, DateTime.UtcNow.AddSeconds(3));
            Assert.True(manager.IsValidChampion("Another Champion"));
            Assert.Equal("AnotherChampion", manager.GetInternalName("Another Champion"));
            manager.LoadAliases();
            Assert.Contains(manager.Aliases, x => x.DisplayName == "Another Champion");
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public void RiotChampionSummarySuppliesNewChampionsRolesAndCorrectAliases()
    {
        string root = CreateRoot();
        try
        {
            string config = Path.Combine(root, "Config", "champion_aliases.json");
            string summary = Path.Combine(root, "Cache", "champion_summary_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(summary)!);
            File.WriteAllText(summary, """
                [{"id":901,"name":"Zaahen","alias":"Zaahen","roles":["fighter","tank"]},
                 {"id":62,"name":"Wukong","alias":"MonkeyKing","roles":["fighter"]},
                 {"id":300,"name":"Aatrox","alias":"AatroxRiot","roles":["fighter"]}]
                """);

            var manager = new AliasManager(new LogService(), config, summary);
            var zaahen = Assert.Single(manager.Aliases, entry => entry.DisplayName == "Zaahen");
            Assert.True(zaahen.IsOfficial);
            Assert.Equal("Zaahen", zaahen.InternalName);
            Assert.Equal(new[] { "fighter", "tank" }, zaahen.Roles);
            Assert.Equal("fighter / tank", zaahen.RolesDisplay);
            Assert.Equal("AatroxRiot", manager.GetInternalName("Aatrox"));
            Assert.Equal("MonkeyKing", manager.GetInternalName("Wukong"));

            string persisted = File.ReadAllText(config);
            Assert.Contains("\"Zaahen\"", persisted);
            Assert.Contains("\"fighter\"", persisted);
            Assert.False(File.Exists(Path.Combine(root, "Config", "champions.json")));

            var reloaded = new AliasManager(new LogService(), config, summary);
            Assert.Equal(1, reloaded.Aliases.Count(entry => entry.DisplayName == "Zaahen"));
            Assert.Equal(new[] { "fighter", "tank" },
                Assert.Single(reloaded.Aliases, entry => entry.DisplayName == "Zaahen").Roles);
        }
        finally { DeleteRoot(root); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RiotVariantRecordsNeverReplaceCanonicalChampionAliases(bool reverseOrder)
    {
        string root = CreateRoot();
        try
        {
            string config = Path.Combine(root, "Config", "champion_aliases.json");
            string summary = Path.Combine(root, "Cache", "champion_summary_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(summary)!);
            string[] records =
            {
                """{"id":103,"name":"Ahri","alias":"Ahri","roles":["mage"]}""",
                """{"id":60103,"name":"Ahri","alias":"Jade_Ahri","roles":["mage"]}""",
                """{"id":28,"name":"Evelynn","alias":"Evelynn"}""",
                """{"id":60028,"name":"Evelynn","alias":"Jade_Evelynn"}""",
                """{"id":37,"name":"Sona","alias":"Sona"}""",
                """{"id":60037,"name":"Sona","alias":"Jade_Sona"}""",
                """{"id":62,"name":"Wukong","alias":"MonkeyKing"}""",
                """{"id":60062,"name":"Wukong","alias":"Jade_Wukong"}"""
            };
            if (reverseOrder) Array.Reverse(records);
            File.WriteAllText(summary, "[" + string.Join(",", records) + "]");

            var aliases = new AliasManager(new LogService(), config, summary);
            Assert.Equal("Ahri", aliases.GetInternalName("Ahri"));
            Assert.Equal("Evelynn", aliases.GetInternalName("Evelynn"));
            Assert.Equal("Sona", aliases.GetInternalName("Sona"));
            Assert.Equal("MonkeyKing", aliases.GetInternalName("Wukong"));
            // A game-mode variant is a valid explicit Riot alias. It must be
            // recognized without ever overwriting its canonical champion.
            Assert.True(aliases.IsValidChampion("Jade_Ahri"));
            Assert.True(aliases.IsValidChampion("Jade_Evelynn"));
            Assert.True(aliases.IsValidChampion("Jade_Wukong"));
            Assert.Equal("Jade_Ahri", aliases.GetInternalName("Jade_Ahri"));
            Assert.Equal("Jade_Wukong", aliases.GetInternalName("Jade_Wukong"));
            Assert.Equal(1, aliases.Aliases.Count(a => a.DisplayName == "Ahri"));
            Assert.DoesNotContain(aliases.Aliases, a => a.DisplayName == "Jade_Ahri");

            // Repair the official alias already persisted by older versions,
            // preserving the existing user's JSON and custom entries.
            var ahri = Assert.Single(aliases.Aliases, a => a.DisplayName == "Ahri");
            ahri.InternalName = "Jade_Ahri";
            Assert.True(aliases.SaveAliases());
            aliases.LoadAliases();
            Assert.Equal("Ahri", aliases.GetInternalName("Ahri"));
            Assert.Contains("\"InternalName\": \"Ahri\"", File.ReadAllText(config));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public void RiotSummaryRefreshKeepsUserAliasesAndAddsNewChampions()
    {
        string root = CreateRoot();
        try
        {
            string config = Path.Combine(root, "Config", "champion_aliases.json");
            string summary = Path.Combine(root, "Cache", "champion_summary_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(summary)!);
            File.WriteAllText(summary, """
                [{"id":901,"name":"Zaahen","alias":"Zaahen","roles":["fighter"]}]
                """);
            var manager = new AliasManager(new LogService(), config, summary);
            manager.Aliases.Add(new VideoGenerator.Views.Models.ChampionAlias
            {
                DisplayName = "My Custom Hero",
                InternalName = "MyCustomHero",
                IsOfficial = false
            });
            Assert.True(manager.SaveAliases());

            File.WriteAllText(summary, """
                [{"id":901,"name":"Zaahen","alias":"Zaahen","roles":["fighter","tank"]},
                 {"id":902,"name":"Next Champion","alias":"NextChampion","roles":["mage"]}]
                """);
            File.SetLastWriteTimeUtc(summary, DateTime.UtcNow.AddSeconds(4));
            manager.InvalidateChampionCatalog();
            manager.LoadAliases();

            Assert.Equal(new[] { "fighter", "tank" },
                Assert.Single(manager.Aliases, entry => entry.DisplayName == "Zaahen").Roles);
            Assert.Equal("mage",
                Assert.Single(manager.Aliases, entry => entry.DisplayName == "Next Champion").RolesDisplay);
            Assert.True(manager.IsValidChampion("NextChampion"));
            Assert.Equal("NextChampion", manager.GetInternalName("Next Champion"));
            Assert.False(Assert.Single(manager.Aliases, entry => entry.DisplayName == "My Custom Hero").IsOfficial);
            Assert.Equal("MyCustomHero", manager.GetInternalName("My Custom Hero"));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public void ExternalChangesToGroupAndChampionJsonAreNotOverwritten()
    {
        string root = CreateRoot();
        try
        {
            string groupsPath = Path.Combine(root, "groups.json");
            string aliasPath = Path.Combine(root, "champion_aliases.json");
            var groups = new GroupManager(new LogService(), groupsPath);
            var aliases = new AliasManager(new LogService(), aliasPath);
            const string edited = "[]";
            File.WriteAllText(groupsPath, edited);
            File.WriteAllText(aliasPath, edited);
            File.SetLastWriteTimeUtc(groupsPath, DateTime.UtcNow.AddSeconds(3));
            File.SetLastWriteTimeUtc(aliasPath, DateTime.UtcNow.AddSeconds(3));
            Assert.False(groups.SaveGroups());
            Assert.False(aliases.SaveAliases());
            Assert.Equal(edited, File.ReadAllText(groupsPath));
            Assert.Equal(edited, File.ReadAllText(aliasPath));
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public void UnknownGroupFlaggedOfficialInJsonDoesNotBecomeUndeletable()
    {
        string root = CreateRoot();
        try
        {
            string path = Path.Combine(root, "groups.json");
            File.WriteAllText(path, """
                [{"Name":" Custom Region ","Category":"Region",
                  "ChampionsRaw":"Aatrox","IsOfficial":true}]
                """);
            var manager = new GroupManager(new LogService(), path);
            var custom = Assert.Single(manager.Groups, x => x.Name == "Custom Region");
            Assert.False(custom.IsOfficial);
            Assert.Equal(1, manager.Groups.Count(x => x.Name == "Custom Region"));
            Assert.True(manager.SaveGroups());
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public void AliasManagerPersistsCustomAliasesAndResolvesThem()
    {
        string root = CreateRoot();
        string path = Path.Combine(root, "champion_aliases.json");

        try
        {
            var manager = new AliasManager(new LogService(), path);
            manager.Aliases.Add(new ChampionAlias
            {
                DisplayName = "Test Champion",
                InternalName = "TestChampion"
            });
            manager.SaveAliases();

            var reloaded = new AliasManager(new LogService(), path);

            Assert.Equal("TestChampion", reloaded.GetInternalName("Test Champion"));
            Assert.Single(reloaded.Aliases, alias =>
                alias.DisplayName.Equals("Test Champion", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void RuleManagerCreatesAndReloadsTheDefaultRuleCatalog()
    {
        string root = CreateRoot();
        string path = Path.Combine(root, "event_rules.json");

        try
        {
            var manager = new RuleManager(new LogService(), path);

            Assert.NotEmpty(manager.Rules);
            Assert.Contains(manager.Rules, rule =>
                rule.Keyword.Equals("Kill", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(path));

            int ruleCount = manager.Rules.Count;
            var reloaded = new RuleManager(new LogService(), path);

            Assert.Equal(ruleCount, reloaded.Rules.Count);
            Assert.Contains(reloaded.Rules, rule =>
                rule.Keyword.Equals("Kill", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void GroupManagerPersistsCustomGroupsAlongsideDefaults()
    {
        string root = CreateRoot();
        string path = Path.Combine(root, "groups.json");

        try
        {
            var manager = new GroupManager(new LogService(), path);
            manager.Groups.Add(new ThematicGroup
            {
                Name = "Test Group",
                Category = "Custom",
                ChampionsRaw = "Aatrox, Ahri"
            });
            manager.SaveGroups();

            var reloaded = new GroupManager(new LogService(), path);
            ThematicGroup group = Assert.Single(reloaded.Groups, item =>
                item.Name.Equals("Test Group", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(new[] { "Aatrox", "Ahri" }, group.GetChampionsList());
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static string CreateRoot() =>
        Directory.CreateTempSubdirectory("VideoGenerator.Configuration.").FullName;

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
