using VideoGenerator.Models;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class EventNameParserIntegrationTests
{
    [Fact]
    public async Task SonaSkin77IsRecognizedButStillRequiresItsExactOfficialPortrait()
    {
        string root = Directory.CreateTempSubdirectory("VG.SonaSkin77.").FullName;
        try
        {
            var parsed = await CreateParser(root).ParseFolderNameAsync(
                "Play_vo_EvelynnSkin65_Kill3DSonaSkin77", "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("champion", parsed.IconType);
            Assert.Equal("SonaSkin77", parsed.IconLookupName);
            Assert.Equal("Kill Sona (Skin 77)", parsed.DisplayText);
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_EvelynnSkin65_FirstEncounter3DKDAFirestarter", "First Encounter with K/DA FIRESTARTER")]
    [InlineData("Play_vo_EvelynnSkin65_Kill3DKDAFirestarter", "Kill K/DA FIRESTARTER")]
    [InlineData("Play_vo_AkaliSkin102_FirstEncounter2DKDAFirestarter", "First Encounter with K/DA FIRESTARTER")]
    public async Task UnindexedRiotKdaFirestarterSkinsAreRecognizedAsNamedCollection(
        string folder, string display)
    {
        string root = Directory.CreateTempSubdirectory("VG.KDAFirestarter.").FullName;
        try
        {
            SeedNewKdaSkinsWithoutSkinlineMembership(root);
            var parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("skinline", parsed.IconType);
            Assert.Equal("K/DA FIRESTARTER", parsed.IconLookupName);
            Assert.Equal(display, parsed.DisplayText);
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));
            Assert.DoesNotContain("K D A", parsed.DisplayText);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task UnverifiedFirestarterSuffixDoesNotCreateAnImaginarySkinline()
    {
        string root = Directory.CreateTempSubdirectory("VG.KDAFirestarterUnknown.").FullName;
        try
        {
            SeedNewKdaSkinsWithoutSkinlineMembership(root);
            var parsed = await CreateParser(root).ParseFolderNameAsync(
                "Play_vo_EvelynnSkin65_FirstEncounter3DKDAUnknownProject", "EN");
            Assert.NotEqual("skinline", parsed.IconType);
            Assert.NotEqual("K/DA", parsed.IconLookupName);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task UserIconRuleForFirestarterIsNotOverridden()
    {
        string root = Directory.CreateTempSubdirectory("VG.KDAFirestarterCustom.").FullName;
        try
        {
            SeedNewKdaSkinsWithoutSkinlineMembership(root);
            File.WriteAllText(Path.Combine(root, "event_rules.json"),
                System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new VideoGenerator.Views.Models.EventRule
                    {
                        Keyword = "FirstEncounter",
                        TranslationKey = "interaction_first_encounter_one",
                        Section = "INTERACTIONS",
                        IconType = "system",
                        IconLookup = "Danger",
                        Type = VideoGenerator.Views.Models.RuleType.Interaction,
                        ExtractsTarget = true
                    }
                }));
            var parsed = await CreateParser(root).ParseFolderNameAsync(
                "Play_vo_EvelynnSkin65_FirstEncounter3DKDAFirestarter", "EN");
            Assert.Equal("system", parsed.IconType);
            Assert.Equal("Danger", parsed.IconLookupName);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void SeedNewKdaSkinsWithoutSkinlineMembership(string root)
    {
        string cache = Path.Combine(root, "Cache");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "skinlines_data_default.json"),
            """[{"id":91,"name":"K/DA"}]""");
        File.WriteAllText(Path.Combine(cache, "skins_data_default.json"), """
            {
              "28065":{"name":"K/DA FIRESTARTER Evelynn",
                "splashPath":"ASSETS/Characters/Evelynn/Skins/Skin65/Images/evelynn_splash_centered_65.jpg","skinLines":[]},
              "84102":{"name":"K/DA FIRESTARTER Ignition Akali",
                "splashPath":"ASSETS/Characters/Akali/Skins/Skin102/Images/akali_splash_centered_102.jpg","skinLines":[]},
              "103097":{"name":"K/DA FIRESTARTER Ahri",
                "splashPath":"ASSETS/Characters/Ahri/Skins/Skin97/Images/ahri_splash_centered_97.jpg","skinLines":[]},
              "103015":{"name":"K/DA Ahri",
                "splashPath":"ASSETS/Characters/Ahri/Skins/Skin15/Images/ahri_splash_centered_15.jpg","skinLines":[{"id":91}]}
            }
            """);
    }

    [Theory]
    [InlineData("Play_vo_AkaliSkin102_FirstEncounter3DAhriSkinlineKDA", "skinline", "AhriSkinlineK/DA")]
    [InlineData("Play_vo_AkaliSkin102_Kill3DEvelynnSkinlineKDA", "skinline", "EvelynnSkinlineK/DA")]
    [InlineData("Play_vo_AkaliSkin102_FirstEncounter3DPentakill", "skinline", "Pentakill")]
    [InlineData("Play_vo_AkaliSkin102_FirstEncounter3DEvelynnSkin66", "champion", "EvelynnSkin66")]
    [InlineData("Play_vo_AkaliSkin102_FirstEncounter3DEvelynnSkin65", "champion", "EvelynnSkin65")]
    [InlineData("Play_vo_AkaliSkin102_FirstEncounter3DSonaSkin06", "champion", "SonaSkin6")]
    public async Task AkaliSkin102UsesCanonicalChampionIdentityRatherThanJadeVariants(
        string folder, string expectedType, string expectedLookup)
    {
        string root = Directory.CreateTempSubdirectory("VG.AkaliSkin102.").FullName;
        try
        {
            string cache = Path.Combine(root, "Cache");
            Directory.CreateDirectory(cache);
            // The actual Riot summary contains both normal champions and
            // Jade_* gameplay variants sharing the exact same display name.
            // Their ordering must not corrupt FirstEncounter/Kill icon requests.
            File.WriteAllText(Path.Combine(cache, "champion_summary_data_default.json"), """
                [{"id":103,"name":"Ahri","alias":"Ahri"},
                 {"id":60103,"name":"Ahri","alias":"Jade_Ahri"},
                 {"id":28,"name":"Evelynn","alias":"Evelynn"},
                 {"id":60028,"name":"Evelynn","alias":"Jade_Evelynn"},
                 {"id":37,"name":"Sona","alias":"Sona"},
                 {"id":60037,"name":"Sona","alias":"Jade_Sona"}]
                """);
            File.WriteAllText(Path.Combine(cache, "skinlines_data_default.json"),
                """[{"id":91,"name":"K/DA"},{"id":16,"name":"Pentakill"}]""");
            File.WriteAllText(Path.Combine(cache, "skins_data_default.json"), """
                {"103015":{"splashPath":"ASSETS/Characters/Ahri/Skins/Skin15/Images/ahri_splash_centered_15.jpg","skinLines":[{"id":91}]},
                 "28006":{"splashPath":"ASSETS/Characters/Evelynn/Skins/Skin06/Images/evelynn_splash_centered_6.jpg","skinLines":[{"id":91}]},
                 "37002":{"splashPath":"ASSETS/Characters/Sona/Skins/Skin02/Images/sona_splash_centered_2.jpg","skinLines":[{"id":16}]}}
                """);
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal(expectedType, parsed.IconType);
            Assert.Equal(expectedLookup, parsed.IconLookupName);
            Assert.DoesNotContain("Jade_", parsed.IconLookupName);
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_JannaSkin67_Receive3DDamage", "Receive Damage")]
    [InlineData("Play_vo_JannaSkin67_Receive2DDamage", "Receive Damage")]
    [InlineData("Play_vo_AkaliSkin103_Receive3DHealShield", "Receive Heal/Shield")]
    [InlineData("Play_vo_AkaliSkin103_Receive2DHealShield", "Receive Heal/Shield")]
    public async Task GeneralReceiveEffectsAreMappedWithoutFabricatingTargetIcons(
        string folder, string display)
    {
        string root = Directory.CreateTempSubdirectory("VG.ReceiveEffects.").FullName;
        try
        {
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal(display, parsed.DisplayText);
            Assert.False(EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_AkaliSkin103_Receive3DUnknownEffect")]
    [InlineData("Play_vo_JannaSkin67_Receive3DDamageFromAhri")]
    public async Task UnknownReceiveEffectsStillNeedMapping(string folder)
    {
        string root = Directory.CreateTempSubdirectory("VG.UnknownReceive.").FullName;
        try
        {
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.False(parsed.IsMapped);
            Assert.Equal("generic", parsed.IconType);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_AkaliSkin103_FirstEncounter3DEkkoSkinlineTrueDamage", "EkkoSkinlineTrue Damage")]
    [InlineData("Play_vo_AkaliSkin103_FirstEncounter3DKaynSkinlineHearsteel", "KaynSkinlineHEARTSTEEL")]
    [InlineData("Play_vo_AkaliSkin103_FirstEncounter3DQiyanaSkinlineTrueDamage", "QiyanaSkinlineTrue Damage")]
    [InlineData("Play_vo_AkaliSkin103_FirstEncounter3DSennaSkinlineTrueDamage", "SennaSkinlineTrue Damage")]
    [InlineData("Play_vo_AkaliSkin103_FirstEncounter3DYasuoSkinlineTrueDamage", "YasuoSkinlineTrue Damage")]
    [InlineData("Play_vo_AkaliSkin103_Move2DFirstAllyTrueDamageX", "True Damage")]
    public async Task NewRiotSkinlineInteractionsRetainRealThemeMembership(
        string folder, string iconLookup)
    {
        string root = Directory.CreateTempSubdirectory("VG.NewRiotSkinline.").FullName;
        try
        {
            string cache = Path.Combine(root, "Cache");
            Directory.CreateDirectory(cache);
            File.WriteAllText(Path.Combine(cache, "skinlines_data_default.json"),
                """[{"id":120,"name":"True Damage"},{"id":199,"name":"HEARTSTEEL"}]""");
            File.WriteAllText(Path.Combine(cache, "skins_data_default.json"), """
                {
                  "1001":{"splashPath":"ASSETS/Characters/Ekko/Skins/Skin19/splash.jpg","skinLines":[{"id":120}]},
                  "1002":{"splashPath":"ASSETS/Characters/Kayn/Skins/Skin20/splash.jpg","skinLines":[{"id":199}]},
                  "1003":{"splashPath":"ASSETS/Characters/Qiyana/Skins/Skin2/splash.jpg","skinLines":[{"id":120}]},
                  "1004":{"splashPath":"ASSETS/Characters/Senna/Skins/Skin1/splash.jpg","skinLines":[{"id":120}]},
                  "1005":{"splashPath":"ASSETS/Characters/Yasuo/Skins/Skin18/splash.jpg","skinLines":[{"id":120}]}
                }
                """);
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("skinline", parsed.IconType);
            Assert.Equal(iconLookup, parsed.IconLookupName);
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("KillBaronSteal", "monster", "Baron")]
    [InlineData("KillDragonSteal", "monster", "Dragon")]
    [InlineData("KillElderSteal", "monster", "Elder Dragon")]
    [InlineData("KillTurret", "structure", "Turret")]
    [InlineData("Ping2DAllIn", "system", "All In")]
    [InlineData("Ping2DAssistMe", "system", "Assist Me")]
    [InlineData("Ping2DBait", "system", "Bait")]
    [InlineData("Ping2DDanger", "system", "Danger")]
    [InlineData("Ping2DEnemyMissing", "system", "Enemy Missing")]
    [InlineData("Ping2DHold", "system", "Hold")]
    [InlineData("Ping2DOnMyWay", "system", "On My Way")]
    [InlineData("Ping2DPush", "system", "Push")]
    [InlineData("Ping2DRetreat", "system", "Retreat")]
    [InlineData("Ping2DTarget", "system", "Target")]
    [InlineData("Shop", "generic", "Generic")]
    [InlineData("ShopOpen", "generic", "Generic")]
    public async Task PreviouslyRegisteredRulesKeepTheirFixedSemanticIconRequests(
        string action, string expectedType, string expectedLookup)
    {
        string root = Directory.CreateTempSubdirectory("VG.PreRegisteredIcons.").FullName;
        try
        {
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(
                $"Play_vo_DianaSkin77_{action}", "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal(expectedType, parsed.IconType);
            Assert.Equal(expectedLookup, parsed.IconLookupName);
            Assert.Equal(expectedType != "generic",
                EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Shop", "event_shop_open", "Play_vo_AatroxSkin33_Shop2D", "Gold")]
    [InlineData("ShopOpen", "event_open_shop", "Play_vo_AatroxSkin33_Shop2DOpen", "Gold")]
    public async Task LegacyShopGoldMappingDoesNotDemandAnUnavailableSystemIcon(
        string keyword, string translationKey, string folder, string oldLookup)
    {
        string root = Directory.CreateTempSubdirectory("VG.LegacyShopIcon.").FullName;
        try
        {
            string config = Path.Combine(root, "event_rules.json");
            var legacy = new VideoGenerator.Views.Models.EventRule
            {
                Keyword = keyword,
                Section = "ITEMS",
                TranslationKey = translationKey,
                IconType = "system",
                IconLookup = oldLookup,
                Type = VideoGenerator.Views.Models.RuleType.Simple,
                ExtractsTarget = false
            };
            File.WriteAllText(config, System.Text.Json.JsonSerializer.Serialize(new[] { legacy }));
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.False(EventIconResolutionService.RequiresIcon(parsed));

            // A user-owned rule is not migrated or overwritten, only
            // interpreted safely at runtime while keeping the same text.
            var persisted = new RuleManager(new LogService(), config).Rules
                .Single(rule => rule.Keyword == keyword);
            Assert.Equal("system", persisted.IconType);
            Assert.Equal("Gold", persisted.IconLookup);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task CustomShopIconLookupRemainsMandatoryAndUnchanged()
    {
        string root = Directory.CreateTempSubdirectory("VG.CustomShopIcon.").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "event_rules.json"),
                System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new VideoGenerator.Views.Models.EventRule
                    {
                        Keyword = "ShopOpen",
                        TranslationKey = "event_open_shop",
                        Section = "ITEMS",
                        IconType = "system",
                        IconLookup = "Danger",
                        Type = VideoGenerator.Views.Models.RuleType.Simple
                    }
                }));
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(
                "Play_vo_AatroxSkin33_Shop2DOpen", "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("system", parsed.IconType);
            Assert.Equal("Danger", parsed.IconLookupName);
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_Aatrox_Kill3DADC")]
    [InlineData("Play_vo_Aatrox_Kill3DAPC")]
    public async Task BuiltInRoleAcronymsResolveAsGroupsNotUnknownTargets(string folder)
    {
        string root = Directory.CreateTempSubdirectory("VG.DefaultRoles.").FullName;
        try
        {
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("champion", parsed.IconType);
            Assert.NotEqual("ADC", parsed.IconLookupName);
            Assert.NotEqual("APC", parsed.IconLookupName);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_Aatrox_Kill3DBlueTurret", "Turret")]
    [InlineData("Play_vo_Aatrox_Kill3DRedTower", "Turret")]
    [InlineData("Play_vo_Aatrox_Kill3DInhibitorTurret", "Turret")]
    [InlineData("Play_vo_Aatrox_Kill3DBlueInhibitor", "Inhibitor")]
    [InlineData("Play_vo_Aatrox_Kill3DRedNexus", "Nexus")]
    public async Task BuiltInStructuresResolveWithoutPersonalJson(
        string folder, string expectedIcon)
    {
        string root = Directory.CreateTempSubdirectory("VG.DefaultStructures.").FullName;
        try
        {
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("structure", parsed.IconType);
            Assert.Equal(expectedIcon, parsed.IconLookupName);
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));
            Assert.False(File.Exists(Path.Combine(root, "Config", "structures.json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task PersonalStructureMappingRefreshesWithinExistingParser()
    {
        string root = Directory.CreateTempSubdirectory("VG.StructureRefresh.").FullName;
        try
        {
            string path = Path.Combine(root, "Config", "structures.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "[]");
            var parser = CreateParser(root);
            const string folder = "Play_vo_Aatrox_Kill3DObsidianGate";
            Assert.NotEqual("structure",
                (await parser.ParseFolderNameAsync(folder, "EN")).IconType);

            await File.WriteAllTextAsync(path,
                "[{\"Keyword\":\"ObsidianGate\",\"TargetName\":\"Nexus\"}]");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(4));
            ParsedEvent parsed = await parser.ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("structure", parsed.IconType);
            Assert.Equal("Nexus", parsed.IconLookupName);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Assist3DAhri", "champion")]
    [InlineData("Death3DAhri", "champion")]
    [InlineData("FirstEncounter3DAhri", "champion")]
    [InlineData("Kill3DAhri", "champion")]
    [InlineData("MoveFirst3DAhri", "champion")]
    [InlineData("MoveFirstAlly3DAhri", "champion")]
    [InlineData("MoveFirstEnemy3DAhri", "champion")]
    [InlineData("SecondEncounter3DAhri", "champion")]
    [InlineData("BuyItem3DInfinityEdgeR", "item")]
    [InlineData("UseItem3DGuardianAngelR", "item")]
    public async Task PreviouslyRegisteredTargetRulesDetermineWhetherTheyNeedAnIcon(
        string action, string expectedCategory)
    {
        string root = Directory.CreateTempSubdirectory("VG.RegisteredTargetRules.").FullName;
        try
        {
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(
                $"Play_vo_DianaSkin77_{action}", "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal(expectedCategory, parsed.IconType);
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_Locke_Kill3DFirstLowHealth", "First Low Health", "KillFirstLowHealth")]
    [InlineData("Play_vo_DianaSkin77_Kill2DFirstLowHealth", "First Low Health", "KillFirstLowHealth")]
    [InlineData("Play_vo_Locke_Kill3DLowHealth", "Low Health", "KillLowHealth")]
    [InlineData("Play_vo_DianaSkin77_Kill2DLowHealth", "Low Health", "KillLowHealth")]
    public async Task KillLowHealthConditionsDoNotRequireChampionPortrait(
        string folder, string expectedText, string nonexistentRule)
    {
        string root = Directory.CreateTempSubdirectory("VG.KillLowHealth.").FullName;
        try
        {
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.False(EventIconResolutionService.RequiresIcon(parsed));
            Assert.Contains(expectedText, parsed.DisplayText, StringComparison.OrdinalIgnoreCase);

            // This recognition must not silently add a new Event Mapping rule.
            var rules = new RuleManager(new LogService(), Path.Combine(root, "event_rules.json"));
            Assert.DoesNotContain(rules.Rules, rule =>
                rule.Keyword.Equals(nonexistentRule, StringComparison.OrdinalIgnoreCase));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_Locke_Kill3DFirstLowHealth")]
    [InlineData("Play_vo_Locke_Kill3DLowHealth")]
    public async Task PersonalizedKillIconRuleIsNotOverriddenForLowHealthConditions(string folder)
    {
        string root = Directory.CreateTempSubdirectory("VG.KillLowHealthUserRule.").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "event_rules.json"),
                System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new VideoGenerator.Views.Models.EventRule
                    {
                        Keyword = "Kill",
                        TranslationKey = "interaction_kill_one",
                        Section = "COMBAT",
                        IconType = "system",
                        IconLookup = "Danger",
                        Type = VideoGenerator.Views.Models.RuleType.Target,
                        ExtractsTarget = true
                    }
                }));
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("system", parsed.IconType);
            Assert.Equal("Danger", parsed.IconLookupName);
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_MissFortuneSkin69_Kill3DQ2", "Kill with Q2")]
    [InlineData("Play_vo_MissFortuneSkin69_Kill2DW3", "Kill with W3")]
    [InlineData("Play_vo_Ahri_Kill3DE", "Kill with E")]
    [InlineData("Play_vo_Ahri_Kill3DR2", "Kill with R2")]
    [InlineData("Play_vo_Ahri_Kill3DQ", "Kill with Q")]
    public async Task KillUsingAnyAbilitySlotOrRecastIsGeneric(string folder, string expectedText)
    {
        string root = Directory.CreateTempSubdirectory("VG.SkillKill.").FullName;
        try
        {
            var parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal(expectedText, parsed.DisplayText);
            Assert.False(EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task AbilityKillUsesLocalizedTemplate()
    {
        string root = Directory.CreateTempSubdirectory("VG.LocalizedSkillKill.").FullName;
        try
        {
            var parsed = await CreateParser(root).ParseFolderNameAsync(
                "Play_vo_MissFortuneSkin69_Kill3DQ2", "ES");
            Assert.Equal("Asesinato con Q2", parsed.DisplayText);
            Assert.Equal("generic", parsed.IconType);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_ShyvanaSkin08_Dragon_Kill3DPenta", "Dragon: Pentakill")]
    [InlineData("Play_vo_ShyvanaSkin08_Human_Kill2DQuadra", "Human: Quadra Kill")]
    [InlineData("Play_vo_ShyvanaSkin08_Dragon_Kill3DDouble", "Dragon: Double Kill")]
    [InlineData("Play_vo_ShyvanaSkin08_Dragon_Kill3DTriple", "Dragon: Triple Kill")]
    [InlineData("Play_vo_ShyvanaSkin08_Kill3DPenta", "Pentakill")]
    public async Task MultiKillWithOptionalFormContextKeepsItsStreakMeaning(
        string folder, string expectedText)
    {
        string root = Directory.CreateTempSubdirectory("VG.MultiKillForm.").FullName;
        try
        {
            var parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal(expectedText, parsed.DisplayText);
            Assert.False(EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task PrefixedMultiKillRespectsCustomizedMultiKillIcon()
    {
        string root = Directory.CreateTempSubdirectory("VG.CustomMultiKillForm.").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "event_rules.json"),
                System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new VideoGenerator.Views.Models.EventRule
                    {
                        Keyword = "KillPenta",
                        TranslationKey = "event_penta_kill",
                        Section = "COMBAT",
                        IconType = "system",
                        IconLookup = "Danger",
                        Type = VideoGenerator.Views.Models.RuleType.Simple
                    }
                }));
            var parsed = await CreateParser(root).ParseFolderNameAsync(
                "Play_vo_ShyvanaSkin08_Dragon_Kill3DPenta", "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("Dragon: Pentakill", parsed.DisplayText);
            Assert.Equal("system", parsed.IconType);
            Assert.Equal("Danger", parsed.IconLookupName);
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_ShyvanaSkin08_EnemyTeamKill3DEpicMonster",
        "EN", "Enemy team kills an epic monster")]
    [InlineData("Play_vo_ShyvanaSkin08_EnemyTeamKill3DEpicMonster",
        "ES", "El equipo enemigo mata a un monstruo épico")]
    [InlineData("Play_vo_ShyvanaSkin08_EnemyTeamKill3DEpicMonster",
        "TR", "Rakip takım destansı bir canavar öldürür")]
    [InlineData("Play_vo_ShyvanaSkin08_AllyTeamKill2DEpicMonster",
        "EN", "Ally team kills an epic monster")]
    [InlineData("Play_vo_ShyvanaSkin08_Kill3DEpicMonster",
        "EN", "Kill an epic monster")]
    public async Task EpicMonsterKillShowsTheTeamAndNeverChoosesAnArbitraryBoss(
        string folder, string language, string expectedText)
    {
        string root = Directory.CreateTempSubdirectory("VG.EpicMonsterKill.").FullName;
        try
        {
            var parsed = await CreateParser(root).ParseFolderNameAsync(folder, language);
            Assert.True(parsed.IsMapped);
            Assert.Equal("monster", parsed.IconType);
            Assert.True(string.IsNullOrEmpty(parsed.IconLookupName));
            Assert.Equal(expectedText, parsed.DisplayText);
            Assert.False(EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_ShyvanaSkin08_Kill3DEpicMonsterAheadAllyTeam", "EN",
        "Kill an epic monster (ally team ahead)")]
    [InlineData("Play_vo_ShyvanaSkin08_Kill3DEpicMonsterBehindAllyTeam", "EN",
        "Kill an epic monster (ally team behind)")]
    [InlineData("Play_vo_ShyvanaSkin08_Kill2DEpicMonsterAheadAllyTeam", "ES",
        "Matar a un monstruo épico (equipo aliado por delante)")]
    [InlineData("Play_vo_ShyvanaSkin08_Kill3DEpicMonsterBehindAllyTeam", "ES",
        "Matar a un monstruo épico (equipo aliado por detrás)")]
    [InlineData("Play_vo_Aatrox_Kill3DEpicMonsterAheadAllyTeam", "TR",
        "Destansı canavar öldür (dost takım önde)")]
    [InlineData("Play_vo_Aatrox_Kill2DEpicMonsterBehindAllyTeam", "TR",
        "Destansı canavar öldür (dost takım geride)")]
    public async Task EpicMonsterScoreConditionsUseBuiltInMonsterRulesAndLocalizedText(
        string folder, string language, string expectedText)
    {
        string root = Directory.CreateTempSubdirectory("VG.EpicScore.").FullName;
        try
        {
            var parsed = await CreateParser(root).ParseFolderNameAsync(folder, language);
            Assert.True(parsed.IsMapped);
            Assert.Equal("monster", parsed.IconType);
            Assert.True(string.IsNullOrEmpty(parsed.IconLookupName));
            Assert.Equal(expectedText, parsed.DisplayText);
            Assert.False(EventIconResolutionService.RequiresIcon(parsed));

            // Event Mapping lists the two official semantic rules while the
            // monster catalog contains only actual named creatures.
            var rules = new RuleManager(new LogService(), Path.Combine(root, "event_rules.json"));
            foreach (string suffix in new[] { "Ahead", "Behind" })
            {
                var rule = Assert.Single(rules.Rules, r =>
                    r.Keyword == $"KillEpicMonster{suffix}AllyTeam");
                Assert.Equal("monster", rule.IconType);
                Assert.Equal("EpicMonster", rule.IconLookup);
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task SavedEpicMonsterRuleOverrideRemainsAuthoritative()
    {
        string root = Directory.CreateTempSubdirectory("VG.EpicScoreCustom.").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "event_rules.json"),
                System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new VideoGenerator.Views.Models.EventRule
                    {
                        Keyword = "KillEpicMonsterAheadAllyTeam",
                        TranslationKey = "event_kill_epic_monster_ahead_ally_team",
                        Section = "COMBAT",
                        IconType = "monster",
                        IconLookup = "Herald",
                        Type = VideoGenerator.Views.Models.RuleType.Simple
                    }
                }));
            var parsed = await CreateParser(root).ParseFolderNameAsync(
                "Play_vo_ShyvanaSkin08_Kill3DEpicMonsterAheadAllyTeam", "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("monster", parsed.IconType);
            Assert.Equal("Herald", parsed.IconLookupName);
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));

            var rules = new RuleManager(new LogService(), Path.Combine(root, "event_rules.json"));
            Assert.Single(rules.Rules, r =>
                r.Keyword == "KillEpicMonsterAheadAllyTeam" && r.IconLookup == "Herald");
            Assert.Contains(rules.Rules, r => r.Keyword == "KillEpicMonsterBehindAllyTeam");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_ShyvanaSkin08_Attack2DEpicMonster")]
    [InlineData("Play_vo_DianaSkin77_Attack3DEpicMonster")]
    public async Task UnspecifiedEpicMonsterIsNeverGivenARealBossPortrait(string folder)
    {
        string root = Directory.CreateTempSubdirectory("VG.EpicMonsterAttack.").FullName;
        try
        {
            var parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("monster", parsed.IconType);
            Assert.True(string.IsNullOrEmpty(parsed.IconLookupName));
            Assert.False(EventIconResolutionService.RequiresIcon(parsed));
            Assert.Equal("Attack Epic Monster", parsed.DisplayText);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_MissFortuneSkin69_Kill3DQ2")]
    [InlineData("Play_vo_ShyvanaSkin08_EnemyTeamKill3DEpicMonster")]
    public async Task ExplicitKillRuleIconOverrideRetainsUserChoice(string folder)
    {
        string root = Directory.CreateTempSubdirectory("VG.KillSemanticOverride.").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "event_rules.json"),
                System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new VideoGenerator.Views.Models.EventRule
                    {
                        Keyword = "Kill",
                        TranslationKey = "interaction_kill_one",
                        Section = "COMBAT",
                        IconType = "system",
                        IconLookup = "Danger",
                        Type = VideoGenerator.Views.Models.RuleType.Target,
                        ExtractsTarget = true
                    }
                }));
            var parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("system", parsed.IconType);
            Assert.Equal("Danger", parsed.IconLookupName);
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_DianaSkin77_FirstEncounter3DArcade", "Arcade")]
    [InlineData("Play_vo_DianaSkin77_Kill3DArcadeHeroes", "Arcade: Heroes")]
    [InlineData("Play_vo_DianaSkin77_FirstEncounter3DArcadeBattleBosses", "Arcade: Battle Bosses")]
    public async Task ArcadeFamilyAndSpecificSublineRouteToSkinline(string folder, string lookup)
    {
        string root = Directory.CreateTempSubdirectory("VG.ArcadeParser.").FullName;
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
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("skinline", parsed.IconType);
            Assert.Equal(lookup, parsed.IconLookupName);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_MissFortuneSkin69_FirstEncounter3DT1WorldsWinners", "World Champions: 2025")]
    [InlineData("Play_vo_MissFortuneSkin69_FirstEncounter3DWorldsWinners", "World Champions: 2025")]
    [InlineData("Play_vo_MissFortuneSkin69_Kill3DT1WorldsWinners", "World Champions: 2025")]
    [InlineData("Play_vo_JayceSkin16_FirstEncounter3DT1WorldsWinners", "World Champions: 2023")]
    [InlineData("Play_vo_AatroxSkin44_FirstEncounter3DWorldsWinners", "World Champions: 2022")]
    public async Task WorldsWinnerVoThemesUseTheirOwnersOfficialChampionshipLine(
        string folder, string expectedLine)
    {
        string root = Directory.CreateTempSubdirectory("VG.WorldsWinners.").FullName;
        try
        {
            SeedWorldChampionCatalog(root);
            var parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("skinline", parsed.IconType);
            Assert.Equal(expectedLine, parsed.IconLookupName);
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));
            Assert.DoesNotContain("champion:", parsed.IconLookupName);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_AatroxSkin44_FirstEncounter3DT1WorldsWinners")]
    [InlineData("Play_vo_DianaSkin77_FirstEncounter3DWorldsWinners")]
    [InlineData("Play_vo_Locke_FirstEncounter3DT1WorldsWinners")]
    public async Task WorldsWinnerThemeWithoutOfficialMatchingMembershipDoesNotBorrowAnIcon(string folder)
    {
        string root = Directory.CreateTempSubdirectory("VG.WorldsWinnersUnverified.").FullName;
        try
        {
            SeedWorldChampionCatalog(root);
            var parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("skinline", parsed.IconType);
            Assert.True(string.IsNullOrEmpty(parsed.IconLookupName));
            Assert.False(EventIconResolutionService.RequiresIcon(parsed));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ExplicitlyCustomizedWorldsRuleKeepsTheCustomIcon()
    {
        string root = Directory.CreateTempSubdirectory("VG.WorldsWinnersOverride.").FullName;
        try
        {
            SeedWorldChampionCatalog(root);
            File.WriteAllText(Path.Combine(root, "event_rules.json"),
                System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new VideoGenerator.Views.Models.EventRule
                    {
                        Keyword = "FirstEncounter",
                        TranslationKey = "interaction_first_encounter_one",
                        Section = "INTERACTIONS",
                        IconType = "system",
                        IconLookup = "Danger",
                        Type = VideoGenerator.Views.Models.RuleType.Interaction,
                        ExtractsTarget = true
                    }
                }));
            var parsed = await CreateParser(root).ParseFolderNameAsync(
                "Play_vo_MissFortuneSkin69_FirstEncounter3DT1WorldsWinners", "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("system", parsed.IconType);
            Assert.Equal("Danger", parsed.IconLookupName);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void SeedWorldChampionCatalog(string root)
    {
        string cache = Path.Combine(root, "Cache");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "skinlines_data_default.json"), """
            [{"id":194,"name":"World Champions: 2022"},
             {"id":207,"name":"World Champions: 2023"},
             {"id":232,"name":"World Champions: 2025"}]
            """);
        File.WriteAllText(Path.Combine(cache, "skins_data_default.json"), """
            {
              "21069": {"name":"MVP T1 Miss Fortune","splashPath":"ASSETS/Characters/MissFortune/Skins/Skin69/Images/missfortune_splash_centered_69.jpg","skinLines":[{"id":232}]},
              "126016":{"name":"T1 Jayce","splashPath":"ASSETS/Characters/Jayce/Skins/Skin16/Images/jayce_splash_centered_16.jpg","skinLines":[{"id":207}]},
              "266044":{"name":"DRX Aatrox","splashPath":"ASSETS/Characters/Aatrox/Skins/Skin44/Images/aatrox_splash_centered_44.jpg","skinLines":[{"id":194}]}
            }
            """);
    }

    [Theory]
    [InlineData("Play_vo_DianaSkin77_FirstEncounter3DEternalAspects")]
    [InlineData("Play_vo_DianaSkin77_Kill3DEternalAspects")]
    [InlineData("Play_vo_DianaSkin77_Kill3DBlueSentinel")]
    [InlineData("Play_vo_DianaSkin77_Kill3DRedBrambleback")]
    [InlineData("Play_vo_DianaSkin77_Kill3DRiftScuttler")]
    [InlineData("Play_vo_DianaSkin77_Kill3DAtakhan")]
    public async Task ResolvesCurrentRetailThematicAndMonsterTargets(string folder)
    {
        string root = Directory.CreateTempSubdirectory("VG.CurrentRetail.").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Cache"));
            File.WriteAllText(Path.Combine(root, "Cache", "skinlines_data_default.json"),
                """[{"id":222,"name":"The Eternal Aspects"}]""");
            File.WriteAllText(Path.Combine(root, "Cache", "skins_data_default.json"), """
                {"1001":{"splashPath":"ASSETS/Characters/Leona/Skins/Skin64/splash.jpg","skinLines":[{"id":222}]}}
                """);
            ParsedEvent parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            if (folder.Contains("BlueSentinel", StringComparison.Ordinal))
            {
                Assert.Equal("monster", parsed.IconType);
                Assert.Equal("Blue_Sentinel", parsed.IconLookupName);
            }
            else if (folder.Contains("RedBrambleback", StringComparison.Ordinal))
            {
                Assert.Equal("monster", parsed.IconType);
                Assert.Equal("Red_Brambleback", parsed.IconLookupName);
            }
            else if (folder.Contains("RiftScuttler", StringComparison.Ordinal))
            {
                Assert.Equal("monster", parsed.IconType);
                Assert.Equal("Rift_Scuttler", parsed.IconLookupName);
            }
            else if (folder.Contains("Atakhan", StringComparison.Ordinal))
            {
                Assert.Equal("monster", parsed.IconType);
                Assert.Equal("Atakhan", parsed.IconLookupName);
            }
            else
            {
                Assert.Equal("skinline", parsed.IconType);
                Assert.Equal("The Eternal Aspects", parsed.IconLookupName);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("BlueSentinel", "Blue_Sentinel")]
    [InlineData("RedBrambleback", "Red_Brambleback")]
    [InlineData("Gromp", "Gromp")]
    [InlineData("AncientKrug", "Ancient_Krug")]
    [InlineData("Razorbeak", "Razorbeak")]
    [InlineData("GreaterMurkWolf", "Greater_Murk_Wolf")]
    [InlineData("RiftScuttler", "Rift_Scuttler")]
    [InlineData("Atakhan", "Atakhan")]
    public async Task KillAndAttackUseTheSameOfficialMonsterVocabulary(
        string target, string expectedLookup)
    {
        string root = Directory.CreateTempSubdirectory("VG.MonsterActions.").FullName;
        try
        {
            var parser = CreateParser(root);
            foreach (string action in new[] { "Kill3D", "Attack2D" })
            {
                ParsedEvent parsed = await parser.ParseFolderNameAsync(
                    $"Play_vo_DianaSkin77_{action}{target}", "EN");
                Assert.True(parsed.IsMapped);
                Assert.Equal("monster", parsed.IconType);
                Assert.Equal(expectedLookup, parsed.IconLookupName);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("Play_vo_DianaSkin77_Kill3DJungleBuff")]
    [InlineData("Play_vo_DianaSkin77_Attack2DJungleBuff")]
    [InlineData("Play_vo_AatroxSkin33_Kill3DJungle_Buff")]
    public async Task UnspecifiedJungleBuffIsAMonsterWithoutAnInventedRedOrBluePortrait(string folder)
    {
        string root = Directory.CreateTempSubdirectory("VG.JungleBuff.").FullName;
        try
        {
            var parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("monster", parsed.IconType);
            Assert.True(string.IsNullOrWhiteSpace(parsed.IconLookupName));
            Assert.False(EventIconResolutionService.RequiresIcon(parsed));
            Assert.Contains("Jungle Buff", parsed.DisplayText, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("BlueBuff", "Blue_Sentinel")]
    [InlineData("RedBuff", "Red_Brambleback")]
    public async Task ExplicitJungleBuffColorRetainsItsOwnOfficialMonsterPortrait(
        string target, string expectedLookup)
    {
        string root = Directory.CreateTempSubdirectory("VG.ColoredJungleBuff.").FullName;
        try
        {
            var parser = CreateParser(root);
            foreach (string action in new[] { "Kill3D", "Attack2D" })
            {
                var parsed = await parser.ParseFolderNameAsync(
                    $"Play_vo_DianaSkin77_{action}{target}", "EN");
                Assert.True(parsed.IsMapped);
                Assert.Equal("monster", parsed.IconType);
                Assert.Equal(expectedLookup, parsed.IconLookupName);
                Assert.True(EventIconResolutionService.RequiresIcon(parsed));
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task NewlyConfiguredMonsterIsRecognizedWithoutCodeChange()
    {
        string root = Directory.CreateTempSubdirectory("VG.NewMonster.").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Config"));
            File.WriteAllText(Path.Combine(root, "Config", "monsters.json"),
                """{"Epic":[],"Large":["Dinosaurio"]}""");
            var parser = CreateParser(root);
            ParsedEvent result = await parser.ParseFolderNameAsync(
                "Play_vo_AatroxSkin33_Attack2DDinosaurio", "EN");
            Assert.True(result.IsMapped);
            Assert.Equal("monster", result.IconType);
            Assert.Equal("Dinosaurio", result.IconLookupName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task MappingEditRefreshesMonsterRecognitionInSameRunningParser()
    {
        string root = Directory.CreateTempSubdirectory("VG.MonsterRefresh.").FullName;
        try
        {
            string config = Path.Combine(root, "Config");
            Directory.CreateDirectory(config);
            string file = Path.Combine(config, "monsters.json");
            File.WriteAllText(file, """{"Epic":[],"Large":[]}""");
            var parser = CreateParser(root);
            const string attack = "Play_vo_AatroxSkin33_Attack2DDinosaurio";
            const string kill = "Play_vo_AatroxSkin33_Kill3DDinosaurio";
            Assert.False((await parser.ParseFolderNameAsync(attack, "EN")).IsMapped);
            File.WriteAllText(file, """{"Epic":[],"Large":["Dinosaurio"]}""");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(3));
            ParsedEvent resolvedAttack = await parser.ParseFolderNameAsync(attack, "EN");
            ParsedEvent resolvedKill = await parser.ParseFolderNameAsync(kill, "EN");
            Assert.Equal("monster", resolvedAttack.IconType);
            Assert.Equal("Dinosaurio", resolvedAttack.IconLookupName);
            Assert.Equal("monster", resolvedKill.IconType);
            Assert.Equal("Dinosaurio", resolvedKill.IconLookupName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("Play_vo_DianaSkin77_FirstEncounter3DDiana", "Diana")]
    [InlineData("Play_vo_DianaSkin77_FirstEncounter3DNami", "Nami")]
    [InlineData("Play_vo_Locke_FirstEncounter3DVayne", "Vayne")]
    public async Task ChampionEncountersDoNotBecomeFuzzyMatchedItems(
        string folder, string target)
    {
        string root = Directory.CreateTempSubdirectory("VG.ChampionTarget.").FullName;
        try
        {
            ParsedEvent result = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(result.IsMapped);
            Assert.Equal("champion", result.IconType);
            Assert.Equal(target, result.IconLookupName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("Play_vo_Aatrox_FirstEncounter3DZaahen")]
    [InlineData("Play_vo_Aatrox_Kill3DZaahen")]
    public async Task NewRiotChampionFromSummaryIsRecognizedWithoutHardcodedGroup(
        string folder)
    {
        string root = Directory.CreateTempSubdirectory("VG.NewChampion.").FullName;
        try
        {
            string cache = Path.Combine(root, "Cache");
            Directory.CreateDirectory(cache);
            File.WriteAllText(Path.Combine(cache, "champion_summary_data_default.json"), """
                [{"id":901,"name":"Zaahen","alias":"Zaahen","roles":["fighter","tank"]}]
                """);
            var parsed = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(parsed.IsMapped);
            Assert.Equal("champion", parsed.IconType);
            Assert.Equal("Zaahen", parsed.IconLookupName);
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));
            Assert.False(File.Exists(Path.Combine(root, "Config", "champions.json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("Play_vo_LucianSkin72_Move2DFirstEnemyNamiSkin68")]
    [InlineData("Play_vo_LucianSkin72_SpellBuffReceive2DNamiSkin68")]
    public async Task SkinInteractionRemovesCompleteActionPrefixBeforeChampionLookup(string folder)
    {
        string root = Directory.CreateTempSubdirectory("VG.SkinTarget.").FullName;
        try
        {
            ParsedEvent result = await CreateParser(root).ParseFolderNameAsync(folder, "EN");
            Assert.True(result.IsMapped);
            Assert.Equal("champion", result.IconType);
            Assert.Equal("NamiSkin68", result.IconLookupName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ParsesSpeakerSkinWithAnimaSquadTargetFromOfficialSkinlineMembers()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;
        try
        {
            string cache = Path.Combine(root, "Cache");
            string config = Path.Combine(root, "Config");
            Directory.CreateDirectory(cache);
            Directory.CreateDirectory(config);
            File.WriteAllText(Path.Combine(cache, "skinlines_data_default.json"),
                """[{"id":156,"name":"Anima Squad"},{"id":206,"name":"Primordian"}]""");
            File.WriteAllText(Path.Combine(cache, "skins_data_default.json"), """
                {
                  "33001":{"splashPath":"ASSETS/Characters/Aatrox/Skins/Skin33/Images/splash.jpg","skinLines":[{"id":206}]},
                  "44001":{"splashPath":"ASSETS/Characters/Riven/Skins/Skin44/Images/splash.jpg","skinLines":[{"id":156}]}
                }
                """);
            File.WriteAllText(Path.Combine(config, "skinlines.json"), """
                {"SkinlineMap":{"animasquad":{"DisplayName":"Anima Squad","Champions":[]}}}
                """);
            // A cache generated after the official files must not override
            // Riot's actual theme membership.
            DateTime old = DateTime.UtcNow.AddHours(-2);
            File.SetLastWriteTimeUtc(Path.Combine(cache, "skins_data_default.json"), old);
            File.SetLastWriteTimeUtc(Path.Combine(cache, "skinlines_data_default.json"), old);
            File.SetLastWriteTimeUtc(Path.Combine(config, "skinlines.json"), old.AddMinutes(5));

            var parser = CreateParser(root);
            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_AatroxSkin33_FirstEncounter3DAnimaSquad", "EN");

            Assert.True(parsed.IsMapped);
            Assert.Equal("skinline", parsed.IconType);
            Assert.Equal("Anima Squad", parsed.IconLookupName);
            Assert.Contains("Anima", parsed.DisplayText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EmptyThemeMembersNeverGenerateAChampionGeneralIconRequest()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;
        try
        {
            string config = Path.Combine(root, "Config");
            Directory.CreateDirectory(config);
            File.WriteAllText(Path.Combine(config, "skinlines.json"), """
                {"SkinlineMap":{"animasquad":{"DisplayName":"Anima Squad","Champions":[]}}}
                """);

            var parser = CreateParser(root);
            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_AatroxSkin33_FirstEncounter3DAnimaSquad", "EN");

            Assert.Equal("skinline", parsed.IconType);
            Assert.Equal("Anima Squad", parsed.IconLookupName);
            Assert.True(parsed.IsMapped);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ParsesGeneralKillUsingOnlyTemporaryConfiguration()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_Aatrox_Kill3DGeneral",
                "EN");

            Assert.Equal("Play_vo_Aatrox_Kill3DGeneral", parsed.OriginalFolder);
            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Contains("Kill", parsed.DisplayText, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(root, "event_rules.json")));
            Assert.True(File.Exists(Path.Combine(root, "groups.json")));
            Assert.True(File.Exists(Path.Combine(root, "champion_aliases.json")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ParsesGeneralAssistWithoutLeavingTargetPlaceholder()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_SeraphineSkin69_Assist3DGeneral",
                "EN");

            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal("Assist in General", parsed.DisplayText);
            Assert.DoesNotContain("{0}", parsed.DisplayText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ParsesGeneralIdleEvent()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_SeraphineSkin69_Idle3DGeneral",
                "EN");

            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal("Idle in General", parsed.DisplayText);
            Assert.True(parsed.IsMapped);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Play_vo_SeraphineSkin69_Joke3DGeneral")]
    [InlineData("Play_vo_SeraphineSkin69_Joke3DGeneral01")]
    [InlineData("Play_vo_SeraphineSkin69_Joke3DGeneral02")]
    public async Task ParsesNumberedGeneralJokeVariants(string folderName)
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(folderName, "EN");

            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal("Joke in General", parsed.DisplayText);
            Assert.True(parsed.IsMapped);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RoutesThreeDimensionalMonsterAttacksToMonsterParser()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_Aatrox_Attack3DBaron",
                "EN");

            Assert.Equal("monster", parsed.IconType);
            Assert.Equal("Baron", parsed.IconLookupName);
            Assert.False(string.IsNullOrWhiteSpace(parsed.DisplayText));
            Assert.True(parsed.IsMapped);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Play_vo_Aatrox_Attack3DLux")]
    [InlineData("Play_vo_Aatrox_Attack3DTurret")]
    [InlineData("Play_vo_Aatrox_Attack2D_Turret")]
    [InlineData("Play_vo_Aatrox_Attack3DUnknownMonster")]
    public async Task LeavesUnrecognizedAttackTargetsPendingMapping(string folderName)
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;
        try
        {
            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(folderName, "EN");

            Assert.False(parsed.IsMapped);
            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal(folderName, parsed.DisplayText);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("Lux")]
    [InlineData("Turret")]
    public async Task DoesNotTreatChampionsOrStructuresAsMonstersEvenIfCatalogContainsThem(string target)
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;
        try
        {
            string monsterPath = Path.Combine(root, "Config", "monsters.json");
            Directory.CreateDirectory(Path.GetDirectoryName(monsterPath)!);
            await File.WriteAllTextAsync(monsterPath, $$"""
                { "Epic": ["{{target}}", "Baron Nashor"], "Large": [] }
                """);
            var parser = CreateParser(root);
            string folderName = $"Play_vo_Aatrox_Attack3D{target}";

            ParsedEvent parsed = await parser.ParseFolderNameAsync(folderName, "EN");

            Assert.False(parsed.IsMapped);
            Assert.Equal("generic", parsed.IconType);
            Assert.Equal(folderName, parsed.DisplayText);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("Play_vo_Aatrox_Attack2DDrake", "Dragon")]
    [InlineData("Play_vo_Aatrox_Attack3DBaronNashor", "Baron_Nashor")]
    [InlineData("Play_vo_Aatrox_Attack3DRiftHerald", "Rift_Herald")]
    [InlineData("Play_vo_Aatrox_Attack2DCloudDragon", "Cloud_Drake")]
    [InlineData("Play_vo_Aatrox_Attack3DVoidGrub", "Void_Grub")]
    [InlineData("Play_vo_Aatrox_Attack3DAtakhanRuinous", "Ruinous_Atakhan")]
    [InlineData("Play_vo_Aatrox_Attack3DRaptor", "Raptor")]
    [InlineData("Play_vo_Aatrox_Attack2DKrug", "Krug")]
    public async Task RecognizesCommonMonsterAliasesWithoutDownloadedCatalog(string folderName, string expectedIcon)
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;
        try
        {
            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(folderName, "EN");

            Assert.True(parsed.IsMapped);
            Assert.Equal("monster", parsed.IconType);
            Assert.Equal(expectedIcon, parsed.IconLookupName);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("Play_vo_Aatrox_Kill3DVoidGrub", "Void_Grub")]
    [InlineData("Play_vo_Aatrox_Kill3DHextechDragon", "Hextech_Drake")]
    [InlineData("Play_vo_Aatrox_Kill3DAtakhanVoracious", "Voracious_Atakhan")]
    public async Task BuiltInMonsterVocabularyWorksForKillWithoutUserCatalog(
        string folderName, string iconLookup)
    {
        string root = Directory.CreateTempSubdirectory("VG.DefaultMonsters.").FullName;
        try
        {
            var parser = CreateParser(root);
            ParsedEvent result = await parser.ParseFolderNameAsync(folderName, "EN");
            Assert.True(result.IsMapped);
            Assert.Equal("monster", result.IconType);
            Assert.Equal(iconLookup, result.IconLookupName);
            Assert.True(EventIconResolutionService.RequiresIcon(result));
            Assert.False(File.Exists(Path.Combine(root, "Config", "monsters.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RecognizesCustomMonsterFromMonstersCatalog()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;
        try
        {
            string monsterPath = Path.Combine(root, "Config", "monsters.json");
            Directory.CreateDirectory(Path.GetDirectoryName(monsterPath)!);
            await File.WriteAllTextAsync(monsterPath,
                """{"Epic":["Void Leviathan"],"Large":[]}""");
            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_Aatrox_Attack3DVoidLeviathan", "EN");

            Assert.True(parsed.IsMapped);
            Assert.Equal("monster", parsed.IconType);
            Assert.Equal("Void_Leviathan", parsed.IconLookupName);
            Assert.Equal("Attack Void Leviathan", parsed.DisplayText);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReusesMonsterCatalogSnapshotAcrossFolderAnalysis()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;
        try
        {
            string monsterPath = Path.Combine(root, "Config", "monsters.json");
            Directory.CreateDirectory(Path.GetDirectoryName(monsterPath)!);
            await File.WriteAllTextAsync(monsterPath, """
                { "Epic": ["Void Leviathan"], "Large": [] }
                """);
            var parser = CreateParser(root);

            ParsedEvent first = await parser.ParseFolderNameAsync("Play_vo_Aatrox_Attack3DVoidLeviathan", "EN");
            File.Delete(monsterPath);
            ParsedEvent second = await parser.ParseFolderNameAsync("Play_vo_Lux_Attack2DVoidLeviathan", "EN");

            Assert.True(first.IsMapped);
            Assert.True(second.IsMapped);
            Assert.Equal("monster", second.IconType);
            Assert.Equal(first.IconLookupName, second.IconLookupName);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PreservesGeneralAttackRule()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;
        try
        {
            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync("Play_vo_Aatrox_Attack3DGeneral", "EN");

            Assert.True(parsed.IsMapped);
            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Attack in General", parsed.DisplayText);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PreservesExplicitRuleForOtherwiseUnknownAttackTarget()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "event_rules.json"), """
                [{ "Keyword": "AttackLux", "TranslationKey": "event_attack", "Section": "COMBAT", "IconType": "generic", "Type": 0 }]
                """);
            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync("Play_vo_Aatrox_Attack3DLux", "EN");

            Assert.True(parsed.IsMapped);
            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Attack", parsed.DisplayText);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RoutesItemEventsThroughTheCommunityDragonCache()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            string cachePath = Path.Combine(root, "Cache", "items_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, """
                [
                  { "id": 3031, "name": "Infinity Edge", "nameSlug": "InfinityEdge" }
                ]
                """);

            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_Aatrox_BuyItem3DInfinityEdgeR",
                "EN");

            Assert.Equal("item", parsed.IconType);
            Assert.Equal("3031", parsed.IconLookupName);
            Assert.Contains("Buy Item", parsed.DisplayText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Infinity Edge", parsed.DisplayText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RoutesWardKillEventsThroughTheItemResolver()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            string cachePath = Path.Combine(root, "Cache", "items_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, """
                [
                  { "id": 3340, "name": "Stealth Ward", "nameSlug": "StealthWard" }
                ]
                """);

            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_SeraphineSkin69_Kill3DWard",
                "EN");

            Assert.Equal("item", parsed.IconType);
            Assert.Equal("Ward", parsed.IconLookupName);
            Assert.Contains("Kill Ward", parsed.DisplayText, StringComparison.OrdinalIgnoreCase);
            Assert.True(parsed.IsMapped);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RoutesSkinInteractionsThroughTheTemporarySkinCache()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            string cachePath = Path.Combine(root, "Cache", "skins_data_default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, """
                {
                  "123": {
                    "name": "Primordian Bel'Veth",
                    "splashPath": "Characters/Belveth/Skins/Skin123/Belveth_Splash_Centered.jpg"
                  }
                }
                """);

            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_Belveth_FirstEncounterBelvethSkin123",
                "EN");

            Assert.Equal("champion", parsed.IconType);
            Assert.Equal("BelvethSkin123", parsed.IconLookupName);
            Assert.Contains("Primordian Bel'Veth", parsed.DisplayText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RoutesUnmappedSpellEventsToTheSpellParser()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_Aatrox_CustomSpell3D",
                "EN");

            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal("Custom Spell", parsed.DisplayText);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FlagsUnknownFolderNamesAsUnmapped()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            var parser = CreateParser(root);
            const string folderName = "Play_vo_Aatrox_UnknownEvent3D";

            ParsedEvent parsed = await parser.ParseFolderNameAsync(folderName, "EN");

            Assert.False(parsed.IsMapped);
            Assert.Equal(folderName, parsed.DisplayText);
            Assert.Equal("generic", parsed.IconType);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ParsesUltimateReadyMovementVariant()
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(
                "Play_vo_AatroxSkin33_Move2DRReady",
                "EN");

            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal("Movement (Ultimate Ready)", parsed.DisplayText);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Play_vo_Locke_Joke3DFail", "Joke (Failed)")]
    [InlineData("Play_vo_Locke_Joke3DFailEnd", "Joke (Failed)")]
    [InlineData("Play_vo_Locke_Joke3DSuccess", "Joke (Successful)")]
    [InlineData("Play_vo_Locke_Joke3DSuccessEnd", "Joke (Successful)")]
    public async Task ParsesJokeOutcomeVariants(string folderName, string expectedDisplayText)
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;

        try
        {
            var parser = CreateParser(root);

            ParsedEvent parsed = await parser.ParseFolderNameAsync(folderName, "EN");

            Assert.Equal("generic", parsed.IconType);
            Assert.Equal("Generic", parsed.IconLookupName);
            Assert.Equal(expectedDisplayText, parsed.DisplayText);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static EventNameParser CreateParser(string root)
    {
        var logger = new LogService();
        var dataFetcher = new DataFetcher(logger, root);
        File.WriteAllText(Path.Combine(root, "translations.json"), """
            {
              "EN": {
                "event_attack": "Attack",
                "event_receive_damage": "Receive Damage",
                "event_receive_heal_shield": "Receive Heal/Shield",
                "event_buy_item": "Buy Item {item_name}",
                "event_assist_general": "Assist in General",
                "event_idle": "Idle",
                "event_joke": "Joke",
                "event_joke_fail": "Joke (Failed)",
                "event_kill_general": "Kill in General",
                "event_double_kill": "Double Kill",
                "event_triple_kill": "Triple Kill",
                "event_quadra_kill": "Quadra Kill",
                "event_penta_kill": "Pentakill",
                "event_kill_with_ability": "Kill with {0}",
                "event_kill_epic_monster": "Kill an epic monster",
                "event_enemy_team_kill_epic_monster": "Enemy team kills an epic monster",
                "event_ally_team_kill_epic_monster": "Ally team kills an epic monster",
                "event_kill_epic_monster_ahead_ally_team": "Kill an epic monster (ally team ahead)",
                "event_kill_epic_monster_behind_ally_team": "Kill an epic monster (ally team behind)",
                "event_move_r_ready": "Movement (Ultimate Ready)",
                "event_joke_success": "Joke (Successful)",
                "interaction_attack_monster": "Attack {monster}",
                "interaction_first_encounter_one": "First Encounter with {0}",
                "interaction_kill_one": "Kill {0}",
                "suffix_in_general": " in General"
              },
              "ES": {
                "event_enemy_team_kill_epic_monster": "El equipo enemigo mata a un monstruo épico",
                "event_kill_with_ability": "Asesinato con {0}",
                "event_kill_epic_monster_ahead_ally_team": "Matar a un monstruo épico (equipo aliado por delante)",
                "event_kill_epic_monster_behind_ally_team": "Matar a un monstruo épico (equipo aliado por detrás)"
              },
              "TR": {
                "event_enemy_team_kill_epic_monster": "Rakip takım destansı bir canavar öldürür",
                "event_kill_epic_monster_ahead_ally_team": "Destansı canavar öldür (dost takım önde)",
                "event_kill_epic_monster_behind_ally_team": "Destansı canavar öldür (dost takım geride)"
              }
            }
            """);
        var translationService = new TranslationService(
            logger,
            Path.Combine(root, "translations.json"));
        var ruleManager = new RuleManager(
            logger,
            Path.Combine(root, "event_rules.json"));
        var groupManager = new GroupManager(
            logger,
            Path.Combine(root, "groups.json"));
        var aliasManager = new AliasManager(
            logger,
            Path.Combine(root, "champion_aliases.json"));
        var skinlineManager = new SkinlineManager(dataFetcher, aliasManager, logger);
        return new EventNameParser(
            translationService,
            dataFetcher,
            ruleManager,
            groupManager,
            aliasManager,
            skinlineManager);
    }
}
