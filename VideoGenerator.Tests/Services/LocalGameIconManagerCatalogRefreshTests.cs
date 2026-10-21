using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VideoGenerator.Models;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Services;

public sealed class LocalGameIconManagerCatalogRefreshTests
{
    private const string GameData = "plugins/rcp-be-lol-game-data/global/default/v1/";
    private const string GameAssets = "plugins/rcp-be-lol-game-data/global/default/";

    [Fact]
    public async Task ReadsVerifiedBaronAndDragonHudSquaresInsteadOfImaginaryUiPaths()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            additionalIcons: new[]
            {
                "assets/characters/sru_baron/hud/baron_square.tex",
                "assets/characters/sru_dragon/hud/dragon_square.tex",
                "assets/characters/sru_riftherald/hud/sruriftherald_square.tex",
                "assets/characters/sru_dragon_elder/hud/dragon_square_elder.tex"
            });
        // Use an actual PNG payload in the synthetic WAD for each exact path:
        // the runtime's asset decoder accepts PNG, DDS and League TEX bytes.
        var source = fixture.CreateSource();
        var manager = fixture.CreateManager(source);
        AssertSize(await manager.FindIconAsync("monster", "Baron"), 6);
        AssertSize(await manager.FindIconAsync("monster", "Dragon"), 7);
        AssertSize(await manager.FindIconAsync("monster", "Rift_Herald"), 8);
        AssertSize(await manager.FindIconAsync("monster", "HERALD"), 8);
        AssertSize(await manager.FindIconAsync("monster", "Elder_Dragon"), 9);
    }

    [Fact]
    public async Task ResolvesRealJungleMonsterHudNamesInsteadOfImaginaryGenericPaths()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            additionalIcons: new[]
            {
                "assets/characters/sru_blue/hud/bluesentinel_square.tex",
                "assets/characters/sru_red/hud/brambleback_square.tex",
                "assets/characters/sru_gromp/hud/gromp_square.tex",
                "assets/characters/sru_krug/hud/ancientkrug_square.tex",
                "assets/characters/sru_razorbeak/hud/razorbeak_square.tex",
                "assets/characters/sru_murkwolf/hud/greatermurkwolf_square.tex",
                "assets/characters/sru_crab/hud/crab_square_0.tex",
                "assets/characters/sru_atakhan/hud/atakhan_r_square_128.tex"
            });
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("monster", "Blue_Sentinel"), 6);
        AssertSize(await manager.FindIconAsync("monster", "Red_Brambleback"), 7);
        AssertSize(await manager.FindIconAsync("monster", "Gromp"), 8);
        AssertSize(await manager.FindIconAsync("monster", "Ancient_Krug"), 9);
        AssertSize(await manager.FindIconAsync("monster", "Razorbeak"), 10);
        AssertSize(await manager.FindIconAsync("monster", "Greater_Murk_Wolf"), 11);
        AssertSize(await manager.FindIconAsync("monster", "Rift_Scuttler"), 12);
        AssertSize(await manager.FindIconAsync("monster", "Atakhan"), 13);
    }

    [Fact]
    public async Task TheEternalAspectsResolvesFromAliasWithoutAChampionFallback()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 7, 8,
            skinlineName: "The Eternal Aspects");
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("skinline", "EternalAspects"), 7);
        AssertSize(await manager.FindIconAsync("skinline", "The Eternal Aspects"), 7);
    }

    [Fact]
    public async Task ArcadeFamilyUsesOnlyOfficialWadMembershipAndTriesNextValidSkin()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            thematicSkins: new[]
            {
                new ThematicSkin(10, "Arcade: Heroes", "Ahri", 5,
                    "assets/characters/ahri/skins/skin5/arcade-tile.png", 0),
                new ThematicSkin(11, "Arcade: Battle Bosses", "Yasuo", 13,
                    "assets/characters/yasuo/skins/skin13/battleboss-tile.png", 12)
            });
        var manager = fixture.CreateManager(fixture.CreateSource());

        // First official member is known, but its asset is deliberately not
        // shipped in the fixture: the umbrella must continue to the other line.
        Assert.Null(await manager.FindIconAsync("skinline", "ArcadeHeroes"));
        AssertSize(await manager.FindIconAsync("skinline", "Arcade"), 12);
        AssertSize(await manager.FindIconAsync("skinline", "ArcadeBattleBosses"), 12);
        Assert.Null(await manager.FindIconAsync("skinline", "ArcadeUnknown"));
    }

    [Fact]
    public async Task WorldChampionsYearUsesOnlyThatYearsRealWadSkinTiles()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            thematicSkins: new[]
            {
                new ThematicSkin(194, "World Champions: 2022", "Aatrox", 44,
                    "assets/characters/aatrox/skins/skin44/tile.png", 6),
                new ThematicSkin(232, "World Champions: 2025", "MissFortune", 69,
                    "assets/characters/missfortune/skins/skin69/tile.png", 9)
            });
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("skinline", "World Champions: 2025"), 9);
        AssertSize(await manager.FindIconAsync("skinline", "World Champions: 2022"), 6);
        Assert.Null(await manager.FindIconAsync("skinline", "T1WorldsWinners"));
        Assert.Null(await manager.FindIconAsync("champion", "T1WorldsWinners"));
    }

    [Fact]
    public async Task RiotChampionQualifiedSkinlineUsesOnlyMatchingChampionSkins()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            thematicSkins: new[]
            {
                new ThematicSkin(120, "True Damage", "Ekko", 19,
                    "assets/characters/ekko/skins/skin19/tile.png", 8),
                new ThematicSkin(120, "True Damage", "Qiyana", 2,
                    "assets/characters/qiyana/skins/skin2/tile.png", 9),
                new ThematicSkin(199, "HEARTSTEEL", "Kayn", 20,
                    "assets/characters/kayn/skins/skin20/tile.png", 10)
            });
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("skinline", "EkkoSkinlineTrue Damage"), 8);
        AssertSize(await manager.FindIconAsync("skinline", "QiyanaSkinlineTrueDamage"), 9);
        AssertSize(await manager.FindIconAsync("skinline", "KaynSkinlineHearsteel"), 10);
        AssertSize(await manager.FindIconAsync("skinline", "TrueDamageX"), 8);
        Assert.Null(await manager.FindIconAsync("skinline", "KaynSkinlineTrueDamage"));
    }

    [Fact]
    public async Task AkaliSkin102TargetsUseKdaAndPentakillMemberTilesWithoutJadeAliases()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            thematicSkins: new[]
            {
                new ThematicSkin(91, "K/DA", "Ahri", 15,
                    "assets/characters/ahri/skins/skin15/tile.png", 8),
                new ThematicSkin(91, "K/DA", "Evelynn", 65,
                    "assets/characters/evelynn/skins/skin65/tile.png", 9),
                new ThematicSkin(91, "K/DA", "Evelynn", 66,
                    "assets/characters/evelynn/skins/skin66/tile.png", 10),
                new ThematicSkin(16, "Pentakill", "Sona", 2,
                    "assets/characters/sona/skins/skin2/tile.png", 12),
                new ThematicSkin(17, "Solo Sona", "Sona", 6,
                    "assets/characters/sona/skins/skin6/tile.png", 11)
            });
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("skinline", "AhriSkinlineKDA"), 8);
        AssertSize(await manager.FindIconAsync("skinline", "EvelynnSkinlineK/DA"), 9);
        AssertSize(await manager.FindIconAsync("skinline", "Pentakill"), 12);
        AssertSize(await manager.FindIconAsync("champion", "EvelynnSkin65"), 9);
        AssertSize(await manager.FindIconAsync("champion", "EvelynnSkin66"), 10);
        AssertSize(await manager.FindIconAsync("champion", "SonaSkin06"), 11);
        Assert.Null(await manager.FindIconAsync("skinline", "SonaSkinlineKDA"));
    }

    [Fact]
    public async Task FirestarterUsesOnlyActualNewKdaSkinsEvenWithoutSeparatePbeSkinlineId()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            thematicSkins: new[]
            {
                new ThematicSkin(91, "K/DA", "Ahri", 15,
                    "assets/characters/ahri/skins/skin15/tile.png", 6, "K/DA Ahri"),
                new ThematicSkin(0, "", "Ahri", 97,
                    "assets/characters/ahri/skins/skin97/tile.png", 0, "K/DA FIRESTARTER Ahri"),
                new ThematicSkin(0, "", "Akali", 102,
                    "assets/characters/akali/skins/skin102/tile.png", 0, "K/DA FIRESTARTER Ignition Akali"),
                new ThematicSkin(0, "", "Evelynn", 65,
                    "assets/characters/evelynn/skins/skin65/tile.png", 9, "K/DA FIRESTARTER Evelynn")
            });
        var manager = fixture.CreateManager(fixture.CreateSource());
        // A missing first/second tile must not cause a fallback to an older K/DA icon.
        AssertSize(await manager.FindIconAsync("skinline", "K/DA FIRESTARTER"), 9);
        AssertSize(await manager.FindIconAsync("skinline", "KDA"), 6);
        Assert.Null(await manager.FindIconAsync("skinline", "K/DA Unknown Project"));
    }

    [Fact]
    public async Task NamedCollectionFallbackUsesTheSameMetadataPatternForOtherFamilies()
    {
        using var fixture = new CatalogFixture();
        // Synthetic future collection: proves this works without any hardcoded
        // family, collection, champion or skin ID in the production resolver.
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            thematicSkins: new[]
            {
                new ThematicSkin(16, "Pentakill", "Sona", 2,
                    "assets/characters/sona/skins/skin2/tile.png", 6, "Pentakill Sona"),
                new ThematicSkin(0, "", "Sona", 88,
                    "assets/characters/sona/skins/skin88/tile.png", 11,
                    "Pentakill NEW WAVE Sona")
            });
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("skinline", "PentakillNewWave"), 11);
        AssertSize(await manager.FindIconAsync("skinline", "Pentakill"), 6);
        Assert.Null(await manager.FindIconAsync("skinline", "PentakillUnknown"));
    }

    [Fact]
    public async Task UnreleasedSkinAbsentFromRiotMetadataDoesNotBorrowAnotherSonaPortrait()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            thematicSkins: new[]
            {
                new ThematicSkin(0, "", "Sona", 75,
                    "assets/characters/sona/skins/skin75/tile.png", 8,
                    "Prestige Requiem Sona")
            });
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("champion", "SonaSkin75"), 8);
        // Riot's actual PBE metadata currently has no SonaSkin77; preserving
        // the requested skin identity is safer than silently using Sona 75.
        Assert.Null(await manager.FindIconAsync("champion", "SonaSkin77"));
    }

    [Fact]
    public async Task WardKillUsesInstalledWardingTotemItemInsteadOfNonexistentWardName()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            itemId: 3340, itemIcon: "assets/items/icons2d/warding-totem.png", itemSize: 6);
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("item", "Ward"), 6);
    }

    [Fact]
    public async Task ResolvesPingTexturesFromInstalledGlobalWadPaths()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            additionalIcons: new[]
            {
                "assets/shared/particles/ping-danger.tex",
                "assets/ux/minimap/pings/on_my_way_new.tex",
                "assets/shared/particles/pingupdate/need/assist_me.tex",
                "assets/shared/particles/pingupdate/need/enemy_missing.tex",
                "assets/shared/particles/pingupdate/need/target.tex",
                "assets/ux/minimap/pings/all_in.tex",
                "assets/ux/minimap/pings/bait.tex",
                "assets/ux/minimap/pings/hold.tex",
                "assets/ux/minimap/pings/push.tex",
                "assets/ux/minimap/pings/retreat.tex"
            });

        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("system", "Danger"), 6);
        AssertSize(await manager.FindIconAsync("system", "On My Way"), 7);
        AssertSize(await manager.FindIconAsync("system", "Assist Me"), 8);
        AssertSize(await manager.FindIconAsync("system", "Enemy Missing"), 9);
        AssertSize(await manager.FindIconAsync("system", "Target"), 10);
        AssertSize(await manager.FindIconAsync("system", "All In"), 11);
        AssertSize(await manager.FindIconAsync("system", "Bait"), 12);
        AssertSize(await manager.FindIconAsync("system", "Hold"), 13);
        AssertSize(await manager.FindIconAsync("system", "Push"), 14);
        AssertSize(await manager.FindIconAsync("system", "Retreat"), 15);
    }

    [Fact]
    public async Task TurretAndNexusUseVerifiedLocalGameHudAssets()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            additionalIcons: new[]
            {
                "assets/characters/turret/hud/turret_blue_square.tex",
                "assets/characters/nexus/hud/nexus_blue_square.tex"
            });
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("structure", "Turret"), 6);
        AssertSize(await manager.FindIconAsync("structure", "Nexus"), 7);
    }

    [Fact]
    public async Task ConfiguredNewMonsterCanResolveConventionalWadHudWithoutHardcodedSwitch()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            additionalIcons: new[]
            {
                "assets/characters/sru_dinosaurio/hud/dinosaurio_square.tex"
            });
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("monster", "Dinosaurio"), 6);
    }

    [Theory]
    [InlineData("On My Way")]
    [InlineData("on_my_way")]
    [InlineData("onmyway")]
    [InlineData("OMW")]
    public async Task OnMyWayVariantsReadSameWadPingTexture(string lookup)
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            additionalIcons: new[] { "assets/ux/minimap/pings/on_my_way_new.tex" });
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("system", lookup), 6);
    }

    [Fact]
    public async Task OnMyWayPrefersMinimapPingInsteadOfGreenParticleWhenBothExist()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            additionalIcons: new[]
            {
                "assets/ux/minimap/pings/on_my_way_new.tex",
                "assets/shared/particles/ping-omw-arrow.tex"
            });
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("system", "On My Way"), 6);
        Assert.Equal(0x1beaaba4d704f784UL,
            LeagueWadAssetSource.ComputePathHash(
                "assets/ux/minimap/pings/on_my_way_new.tex"));
    }

    [Fact]
    public async Task OnMyWayFallsBackToOldParticleIfNewPingIsMissing()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1,
            additionalIcons: new[] { "assets/shared/particles/ping-omw-arrow.tex" });
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("system", "On My Way"), 6);
    }

    [Fact]
    public async Task NewWadSkin0WinsOverStaleLocalMetadataAndTileCacheAfterPatch()
    {
        using var fixture = new CatalogFixture();
        const string oldTile = "assets/characters/ahri/skins/skin0/old-tile.png";
        const string newTile = "assets/characters/ahri/skins/skin0/new-tile.png";
        const string oldSplash = "assets/characters/ahri/skins/skin0/old-splash.png";
        const string newSplash = "assets/characters/ahri/skins/skin0/new-splash.png";

        fixture.WriteLegacySkins(oldTile, oldSplash);
        fixture.WriteLegacyIcon("tiles", "Ahri_0.png", 9, 9);
        fixture.WriteLegacyIcon("splash", "Ahri_0.png", 9, 9);
        fixture.WriteWad(oldTile, oldSplash, 2, 3);

        var source = fixture.CreateSource();
        var manager = fixture.CreateManager(source);

        string? tileBefore = await manager.FindIconAsync("champion", "AhriSkin0");
        string? splashBefore = await manager.FindSplashAsync("Ahri", 0);
        AssertSize(tileBefore, 2);
        AssertSize(splashBefore, 3);

        fixture.WriteWad(newTile, newSplash, 4, 5);
        Assert.True(source.RefreshArchives());
        manager.RefreshCatalogs();

        string? tileAfter = await manager.FindIconAsync("champion", "AhriSkin0");
        string? splashAfter = await manager.FindSplashAsync("Ahri", 0);
        AssertSize(tileAfter, 4);
        AssertSize(splashAfter, 5);
        Assert.NotEqual(tileBefore, tileAfter);
        Assert.NotEqual(splashBefore, splashAfter);
        Assert.NotEqual(Path.Combine(fixture.IconCache, "tiles", "Ahri_0.png"), tileAfter);
    }

    [Fact]
    public async Task RiotBaseSkinDirectoryResolvesFullSplashAndIsBrowsable()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad(
            "assets/characters/aatrox/skins/base/images/aatrox_splash_tile_0.jpg",
            "assets/characters/aatrox/skins/base/images/aatrox_splash_centered_0.jpg",
            7, 19);
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindSplashAsync("Aatrox", 0), 19);
        AssertSize(await manager.FindIconAsync("champion", "AatroxSkin0"), 7);
        var choices = await manager.GetAvailableSplashSkinsAsync("Aatrox");
        Assert.Equal(0, Assert.Single(choices).SkinId);
        Assert.Empty(await manager.GetAvailableSplashSkinsAsync("Akali"));
    }

    [Fact]
    public async Task AMissingFullSplashDoesNotUseTheChampionsSquareTile()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteWad(
            "assets/characters/aatrox/skins/base/images/aatrox_splash_tile_0.jpg",
            "assets/characters/aatrox/skins/base/images/aatrox_splash_centered_0.jpg",
            7, 19, includeSplash: false);
        var manager = fixture.CreateManager(fixture.CreateSource());
        AssertSize(await manager.FindIconAsync("champion", "AatroxSkin0"), 7);
        Assert.Null(await manager.FindSplashAsync("Aatrox", 0));
        Assert.Empty(await manager.GetAvailableSplashSkinsAsync("Aatrox"));
    }

    [Fact]
    public async Task RefreshArchivesAutomaticallyReloadsItemIdAndPathWithoutRestart()
    {
        using var fixture = new CatalogFixture();
        const string oldIcon = "assets/items/boot-old.png";
        const string newIcon = "assets/items/boot-current.png";
        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1, 1001, oldIcon, 2);

        var source = fixture.CreateSource();
        var manager = fixture.CreateManager(source);
        string? before = await manager.FindIconAsync("item", "Boots");
        AssertSize(before, 2);

        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile.png",
            "assets/characters/ahri/skins/skin0/splash.png", 1, 1, 2001, newIcon, 6);
        Assert.True(source.RefreshArchives());

        // No RefreshCatalogs call: a changed ArchiveRevision invalidates automatically.
        string? after = await manager.FindIconAsync("item", "Boots");
        AssertSize(after, 6);
        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task ExplicitRefreshReevaluatesUnavailableWadAndConcurrentRequests()
    {
        using var fixture = new CatalogFixture();
        fixture.WriteLegacyIcon("tiles", "Ahri_0.png", 8, 8);
        var source = fixture.CreateSource();
        var manager = fixture.CreateManager(source);
        AssertSize(await manager.FindIconAsync("champion", "AhriSkin0"), 8);

        fixture.WriteWad("assets/characters/ahri/skins/skin0/tile-latest.png",
            "assets/characters/ahri/skins/skin0/splash-latest.png", 3, 4);
        Assert.True(source.RefreshArchives());
        manager.RefreshCatalogs();

        string?[] resolved = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => manager.FindIconAsync("champion", "AhriSkin0")));
        foreach (string? path in resolved)
            AssertSize(path, 3);
    }

    [Fact]
    public async Task PatchRefreshReadsCurrentWadSkinlineMembershipDespiteStaleDiskCatalogs()
    {
        using var fixture = new CatalogFixture();
        const string tile = "assets/characters/ahri/skins/skin0/current-tile.png";
        const string splash = "assets/characters/ahri/skins/skin0/current-splash.png";
        fixture.WriteLegacySkins(tile, splash);
        fixture.WriteLegacySkinlines("Old Dynasty");
        fixture.WriteWad(tile, splash, 2, 3, skinlineName: "Old Dynasty");

        var source = fixture.CreateSource();
        var manager = fixture.CreateManager(source);
        AssertSize(await manager.FindIconAsync("champion", "Old Dynasty"), 2);

        fixture.WriteWad(tile, splash, 5, 6, skinlineName: "New Dynasty");
        Assert.True(source.RefreshArchives());
        manager.RefreshCatalogs();

        AssertSize(await manager.FindIconAsync("champion", "New Dynasty"), 5);
        Assert.Null(await manager.FindIconAsync("champion", "Old Dynasty"));
    }

    private static void AssertSize(string? path, int expected)
    {
        Assert.False(string.IsNullOrEmpty(path));
        using var image = Image.Load(path!);
        Assert.Equal(expected, image.Width);
        Assert.Equal(expected, image.Height);
    }

    private sealed record ThematicSkin(
        int LineId, string LineName, string Champion, int SkinId, string Tile, int Size,
        string? SkinName = null);

    private sealed class CatalogFixture : IDisposable
    {
        private int _revision;
        public string Root { get; } = Directory.CreateTempSubdirectory("VG.IconPatch.").FullName;
        public string IconCache => Path.Combine(Root, "IconCache");

        public LeagueWadAssetSource CreateSource() => new(new LogService(), Root, IconCache);

        public LocalGameIconManager CreateManager(LeagueWadAssetSource source)
        {
            var logger = new LogService();
            var data = new DataFetcher(logger, Root);
            var aliases = new AliasManager(logger, Path.Combine(Root, "aliases.json"));
            var groups = new GroupManager(logger, Path.Combine(Root, "groups.json"));
            var skinlines = new SkinlineManager(data, aliases, logger);
            return new LocalGameIconManager(source, data, aliases, groups, skinlines,
                logger, IconCache, offlineSkins: new OfflineSkinCatalog(Root, logger));
        }

        public void WriteLegacySkins(string tile, string splash)
        {
            string path = AppConfig.GetSkinsCachePath("default", Root);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["1000"] = new { tilePath = tile, splashPath = splash }
            }));
        }

        public void WriteLegacySkinlines(string name)
        {
            string path = AppConfig.GetSkinLinesCachePath("default", Root);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new[] { new { id = 123, name } }));
        }

        public void WriteLegacyIcon(string category, string name, int width, int height)
        {
            string file = Path.Combine(IconCache, category, name);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            using var image = new Image<Rgba32>(width, height);
            image.SaveAsPng(file);
        }

        public void WriteWad(string tile, string splash, int tileSize, int splashSize,
            int itemId = 0, string? itemIcon = null, int itemSize = 1,
            string? skinlineName = null, IReadOnlyList<string>? additionalIcons = null,
            IReadOnlyList<ThematicSkin>? thematicSkins = null,
            bool includeSplash = true)
        {
            var skins = new Dictionary<string, object>
            {
                ["1000"] = new
                {
                    tilePath = tile, splashPath = splash,
                    skinLines = skinlineName == null ? Array.Empty<object>() : new object[] { new { id = 123 } }
                }
            };
            var lines = new List<object>();
            if (skinlineName != null) lines.Add(new { id = 123, name = skinlineName });
            if (thematicSkins != null)
            {
                int index = 0;
                foreach (var member in thematicSkins)
                {
                    skins[(2000 + index++).ToString()] = new
                    {
                        name = member.SkinName,
                        tilePath = member.Tile,
                        splashPath = member.Tile.Replace("tile.png", "splash.png"),
                        skinLines = member.LineId > 0
                            ? new object[] { new { id = member.LineId } }
                            : Array.Empty<object>()
                    };
                    if (member.LineId > 0 && !lines.Any(line =>
                        JsonSerializer.Serialize(line).Contains($"\"id\":{member.LineId}", StringComparison.Ordinal)))
                        lines.Add(new { id = member.LineId, name = member.LineName });
                }
            }
            string skinsJson = JsonSerializer.Serialize(skins);
            string itemsJson = itemIcon == null ? "[]" : JsonSerializer.Serialize(new[]
            {
                new { id = itemId, name = "Boots", nameSlug = "boots", iconPath = itemIcon }
            });
            var assets = new List<(string Path, byte[] Data)>
            {
                (GameData + "skins.json", Encoding.UTF8.GetBytes(skinsJson)),
                (GameData + "skinlines.json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(lines))),
                (GameData + "items.json", Encoding.UTF8.GetBytes(itemsJson)),
                (GameAssets + tile, CreatePng(tileSize))
            };
            if (includeSplash)
                assets.Add((GameAssets + splash, CreatePng(splashSize)));
            if (itemIcon != null)
                assets.Add((GameAssets + itemIcon, CreatePng(itemSize)));
            if (additionalIcons != null)
            {
                int size = 6;
                foreach (string assetPath in additionalIcons)
                    assets.Add((assetPath, CreatePng(size++)));
            }
            if (thematicSkins != null)
                foreach (var member in thematicSkins.Where(member => member.Size > 0))
                    assets.Add((GameAssets + member.Tile, CreatePng(member.Size)));

            string output = Path.Combine(Root, "Plugins", "test-patch.wad.client");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            using (var file = File.Create(output))
            using (var writer = new BinaryWriter(file))
            {
                writer.Write("RW"u8);
                writer.Write((byte)3);
                writer.Write((byte)1);
                writer.Write(new byte[256]);
                writer.Write(0UL);
                writer.Write(assets.Count);
                int offset = 272 + 32 * assets.Count;
                foreach (var asset in assets)
                {
                    writer.Write(LeagueWadAssetSource.ComputePathHash(asset.Path));
                    writer.Write((uint)offset);
                    writer.Write(asset.Data.Length);
                    writer.Write(asset.Data.Length);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((ushort)0);
                    writer.Write(0UL);
                    offset += asset.Data.Length;
                }
                foreach (var asset in assets) writer.Write(asset.Data);
            }

            // Filesystem timestamps may otherwise have coarse resolution on Windows.
            File.SetLastWriteTimeUtc(output, DateTime.UtcNow.AddMinutes(++_revision));
        }

        private static byte[] CreatePng(int size)
        {
            using var image = new Image<Rgba32>(size, size);
            using var output = new MemoryStream();
            image.SaveAsPng(output);
            return output.ToArray();
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
