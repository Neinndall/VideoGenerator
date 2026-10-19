using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class SkinlineManagerTests
{
    [Fact]
    public void ArcadeFamilyUnifiesBothOfficialLinesButKeepsExactSublineSeparate()
    {
        string root = Directory.CreateTempSubdirectory("VG.ArcadeFamily.").FullName;
        try
        {
            string cache = Path.Combine(root, "Cache");
            Directory.CreateDirectory(cache);
            File.WriteAllText(Path.Combine(cache, "skinlines_data_default.json"),
                """[{"id":10,"name":"Arcade: Heroes"},{"id":11,"name":"Arcade: Battle Bosses"}]""");
            File.WriteAllText(Path.Combine(cache, "skins_data_default.json"), """
                {
                  "1001":{"splashPath":"ASSETS/Characters/Ahri/Skins/Skin5/splash.jpg","skinLines":[{"id":10}]},
                  "1002":{"splashPath":"ASSETS/Characters/Yasuo/Skins/Skin13/splash.jpg","skinLines":[{"id":11}]}
                }
                """);
            var logger = new LogService();
            var data = new DataFetcher(logger, root);
            var aliases = new AliasManager(logger, Path.Combine(root, "Config", "champion_aliases.json"));
            var manager = new SkinlineManager(data, aliases, logger);

            Assert.True(manager.IsKnownSkinline("Arcade"));
            Assert.True(manager.IsKnownSkinline("ArcadeHeroes"));
            Assert.True(manager.IsKnownSkinline("ArcadeBattleBosses"));
            Assert.False(manager.IsKnownSkinline("ArcadeOther"));
            Assert.Equal("Arcade", manager.GetDisplayName("Arcade"));
            Assert.Equal("Arcade: Heroes", manager.GetDisplayName("ArcadeHeroes"));
            Assert.Equal(2, manager.GetChampionsWithSkin("Arcade").Count);
            var heroes = Assert.Single(manager.GetChampionsWithSkin("ArcadeHeroes"));
            Assert.Equal("Ahri", heroes.Name);
            Assert.Equal(5, heroes.SkinId);
            var bosses = Assert.Single(manager.GetChampionsWithSkin("ArcadeBattleBosses"));
            Assert.Equal("Yasuo", bosses.Name);
            Assert.Equal(13, bosses.SkinId);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void EternalAspectsMatchesTheOfficialLineAndRetainsMultipleSkinsForSameChampion()
    {
        string root = Directory.CreateTempSubdirectory("VG.EternalAspects.").FullName;
        try
        {
            string cache = Path.Combine(root, "Cache");
            Directory.CreateDirectory(cache);
            File.WriteAllText(Path.Combine(cache, "skinlines_data_default.json"),
                """[{"id":222,"name":"The Eternal Aspects"}]""");
            File.WriteAllText(Path.Combine(cache, "skins_data_default.json"), """
                {
                  "1001":{"splashPath":"ASSETS/Characters/Leona/Skins/Skin64/splash.jpg","skinLines":[{"id":222}]},
                  "1002":{"splashPath":"ASSETS/Characters/Diana/Skins/Skin76/splash.jpg","skinLines":[{"id":222}]},
                  "1003":{"splashPath":"ASSETS/Characters/Diana/Skins/Skin77/splash.jpg","skinLines":[{"id":222}]},
                  "1004":{"splashPath":"ASSETS/Characters/Zoe/Skins/Skin43/splash.jpg","skinLines":[{"id":222}]}
                }
                """);
            var logger = new LogService();
            var data = new DataFetcher(logger, root);
            var aliases = new AliasManager(logger, Path.Combine(root, "Config", "champion_aliases.json"));
            var manager = new SkinlineManager(data, aliases, logger);
            Assert.True(manager.IsKnownSkinline("EternalAspects"));
            Assert.True(manager.IsKnownSkinline("The Eternal Aspects"));
            Assert.Equal("The Eternal Aspects", manager.GetDisplayName("EternalAspects"));
            var members = manager.GetChampionsWithSkin("EternalAspects");
            Assert.Equal(4, members.Count);
            Assert.Contains(members, member => member.Name == "Diana" && member.SkinId == 77);
            Assert.Contains(members, member => member.Name == "Diana" && member.SkinId == 76);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReplacesNewerButEmptyGeneratedMembershipFromOfficialSkinJson()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.SkinlineManager.").FullName;
        try
        {
            string config = Path.Combine(root, "Config");
            string cache = Path.Combine(root, "Cache");
            Directory.CreateDirectory(config);
            Directory.CreateDirectory(cache);
            string generated = Path.Combine(config, "skinlines.json");
            const string previous = """
                {"SkinlineMap":{"animasquad":{"DisplayName":"Anima Squad","Champions":[]}}}
                """;
            File.WriteAllText(generated, previous);
            string skinlines = Path.Combine(cache, "skinlines_data_default.json");
            string skins = Path.Combine(cache, "skins_data_default.json");
            File.WriteAllText(skinlines, """[{"id":156,"name":"Anima Squad"}]""");
            File.WriteAllText(skins, """
                {"43001":{"splashPath":"/lol-game-data/assets/ASSETS/Characters/Seraphine/Skins/Skin43/Images/splash.jpg","skinLines":[{"id":156}]}}
                """);
            DateTime before = DateTime.UtcNow.AddHours(-3);
            File.SetLastWriteTimeUtc(skinlines, before);
            File.SetLastWriteTimeUtc(skins, before);
            File.SetLastWriteTimeUtc(generated, before.AddHours(1));

            var logger = new LogService();
            var data = new DataFetcher(logger, root);
            var aliases = new AliasManager(logger, Path.Combine(config, "champion_aliases.json"));
            var manager = new SkinlineManager(data, aliases, logger);

            Assert.True(manager.IsKnownSkinline("AnimaSquad"));
            var member = Assert.Single(manager.GetChampionsWithSkin("AnimaSquad"));
            Assert.Equal("Seraphine", member.Name);
            Assert.Equal(43, member.SkinId);
            Assert.Equal(previous, File.ReadAllText(generated));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RebuildsGeneratedCatalogAfterOfficialPatchAndInvalidation()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.SkinlineManager.").FullName;
        try
        {
            string cache = Path.Combine(root, "Cache");
            Directory.CreateDirectory(cache);
            string lines = Path.Combine(cache, "skinlines_data_default.json");
            string skins = Path.Combine(cache, "skins_data_default.json");
            File.WriteAllText(lines, """[{"id":42,"name":"Star Lantern"}]""");
            File.WriteAllText(skins,
                """{"100":{"splashPath":"assets/characters/wukong/skins/skin12/splash.jpg","skinLines":[{"id":42}]}}""");

            var logger = new LogService();
            var fetcher = new DataFetcher(logger, root);
            var aliases = new AliasManager(logger, Path.Combine(root, "Config", "champion_aliases.json"));
            var manager = new SkinlineManager(fetcher, aliases, logger);

            Assert.True(manager.IsKnownSkinline("Star Lantern"));
            var original = Assert.Single(manager.GetChampionsWithSkin("Star Lantern"));
            Assert.Equal("MonkeyKing", original.Name);
            Assert.Equal(12, original.SkinId);

            string generated = Path.Combine(root, "Config", "skinlines.json");
            Assert.True(File.Exists(generated));
            string originalContents = File.ReadAllText(generated);

            // An official WAD patch changes the line and skin IDs after the
            // manager has already loaded its generated skinlines.json.
            File.WriteAllText(lines, """[{"id":77,"name":"Solar Flash"}]""");
            File.WriteAllText(skins,
                """{"200":{"splashPath":"ASSETS\\CHARACTERS\\Wukong\\Skins\\sKiN17\\splash.jpg","skinLines":[{"id":77}]}}""");
            DateTime updated = File.GetLastWriteTimeUtc(generated).AddMinutes(1);
            File.SetLastWriteTimeUtc(lines, updated);
            File.SetLastWriteTimeUtc(skins, updated);

            Assert.True(manager.IsKnownSkinline("Star Lantern"));
            manager.InvalidateCatalog();

            Assert.False(manager.IsKnownSkinline("Star Lantern"));
            Assert.True(manager.IsKnownSkinline("Solar Flash"));
            var refreshed = Assert.Single(manager.GetChampionsWithSkin("Solar Flash"));
            Assert.Equal("MonkeyKing", refreshed.Name);
            Assert.Equal(17, refreshed.SkinId);
            Assert.Equal(originalContents, File.ReadAllText(generated));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PrefersFresherOfficialCachesOverExistingGeneratedCatalog()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.SkinlineManager.").FullName;
        try
        {
            string config = Path.Combine(root, "Config");
            string cache = Path.Combine(root, "Cache");
            Directory.CreateDirectory(config);
            Directory.CreateDirectory(cache);
            string generated = Path.Combine(config, "skinlines.json");
            const string customContents = """
                {
                  "SkinlineMap": {
                    "legacytheme": {
                      "DisplayName": "Legacy Theme",
                      "Champions": [{"Name":"MonkeyKing","SkinId":3}]
                    }
                  },
                  "UserCustomNote": "Keep my edits exactly"
                }
                """;
            File.WriteAllText(generated, customContents);
            string lines = Path.Combine(cache, "skinlines_data_default.json");
            string skins = Path.Combine(cache, "skins_data_default.json");
            File.WriteAllText(lines, """[{"id":87,"name":"New Theme"}]""");
            File.WriteAllText(skins,
                """{"900":{"splashPath":"ASSETS/Characters/Wukong/Skins/Skin44/splash.jpg","skinLines":[{"id":87}]}}""");
            DateTime older = DateTime.UtcNow.AddHours(-2);
            File.SetLastWriteTimeUtc(generated, older);
            File.SetLastWriteTimeUtc(lines, older.AddMinutes(1));
            File.SetLastWriteTimeUtc(skins, older.AddMinutes(1));

            var logger = new LogService();
            var fetcher = new DataFetcher(logger, root);
            var aliases = new AliasManager(logger, Path.Combine(config, "champion_aliases.json"));
            var manager = new SkinlineManager(fetcher, aliases, logger);

            Assert.False(manager.IsKnownSkinline("Legacy Theme"));
            Assert.True(manager.IsKnownSkinline("New Theme"));
            Assert.Equal(44, Assert.Single(manager.GetChampionsWithSkin("New Theme")).SkinId);
            Assert.Equal(customContents, File.ReadAllText(generated));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PreservesGeneratedCatalogWhenOfficialCachesAreMissing()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.SkinlineManager.").FullName;
        try
        {
            string config = Path.Combine(root, "Config");
            Directory.CreateDirectory(config);
            File.WriteAllText(Path.Combine(config, "skinlines.json"),
                """{"SkinlineMap":{"legacytheme":{"DisplayName":"Legacy Theme","Champions":[{"Name":"MonkeyKing","SkinId":3}]}}}""");
            var logger = new LogService();
            var fetcher = new DataFetcher(logger, root);
            var aliases = new AliasManager(logger, Path.Combine(config, "champion_aliases.json"));
            var manager = new SkinlineManager(fetcher, aliases, logger);

            Assert.True(manager.IsKnownSkinline("Legacy Theme"));
            Assert.Equal(3, Assert.Single(manager.GetChampionsWithSkin("Legacy Theme")).SkinId);
            manager.InvalidateCatalog();
            Assert.True(manager.IsKnownSkinline("Legacy Theme"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
