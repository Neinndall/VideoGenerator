using VideoGenerator.Models;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class EventNameParserIntegrationTests
{
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
    [InlineData("Shop", "system", "Gold")]
    [InlineData("ShopOpen", "system", "Gold")]
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
            Assert.True(EventIconResolutionService.RequiresIcon(parsed));
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
    [InlineData("{\"Epic\":[\"Void Leviathan\"],\"Large\":[]}")]
    [InlineData("[\"Void Leviathan\"]")]
    public async Task RecognizesCustomMonsterFromCurrentOrLegacyCatalog(string monsterJson)
    {
        string root = Directory.CreateTempSubdirectory("VideoGenerator.ParserIntegration.").FullName;
        try
        {
            string monsterPath = Path.Combine(root, "Config", "monsters.json");
            Directory.CreateDirectory(Path.GetDirectoryName(monsterPath)!);
            await File.WriteAllTextAsync(monsterPath, monsterJson);
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
                "event_move_r_ready": "Movement (Ultimate Ready)",
                "event_joke_success": "Joke (Successful)",
                "interaction_attack_monster": "Attack {monster}",
                "interaction_first_encounter_one": "First Encounter with {0}",
                "interaction_kill_one": "Kill {0}",
                "suffix_in_general": " in General"
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
