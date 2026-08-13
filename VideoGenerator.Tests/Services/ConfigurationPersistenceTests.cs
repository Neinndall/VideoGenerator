using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class ConfigurationPersistenceTests
{
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
