using System;
using System.IO;
using System.Linq;
using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests.Services;

public sealed class MonsterCatalogEditorTests
{
    [Theory]
    [InlineData("{ invalid monster json")]
    [InlineData("{\"Epic\": 123, \"Large\": []}")]
    [InlineData("{\"Epic\": [], \"UnknownField\": [\"My Custom Data\"]}")]
    public void InvalidPersonalCatalogIsDetectedWithoutBeingChanged(string json)
    {
        string root = Directory.CreateTempSubdirectory("VG.MonsterEditor.").FullName;
        try
        {
            string path = AppConfig.GetMonstersFilePath(root);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
            var fetcher = new DataFetcher(new LogService(), root);
            Assert.False(fetcher.TryLoadMonsterDatabase(out _));
            Assert.Equal(json, File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExistingSavedGroupsRemainUnchangedWhenEditingMonsters()
    {
        var saved = new MonsterDatabase
        {
            Epic = new() { "My Custom Boss", "Blue Sentinel" },
            Large = new() { "Dinosaurio", "Rift Herald" }
        };

        var rows = MonsterCatalogEditor.CreateRows(saved).ToList();
        Assert.Contains(rows, row => row.Name == "Baron" && row.IsOfficial);
        Assert.Contains(rows, row => row.Name == "Blue Sentinel" && row.IsOfficial &&
                                      row.Section == MonsterCatalogSection.Epic);
        Assert.Contains(rows, row => row.Name == "Rift Herald" && row.IsOfficial &&
                                      row.Section == MonsterCatalogSection.Large);
        Assert.Contains(rows, row => row.Name == "Dinosaurio" && row.CanDelete);
        Assert.Contains(rows, row => row.Name == "My Custom Boss" && row.CanDelete);

        rows.Add(new MonsterCatalogRow("User New", false, MonsterCatalogSection.Large));
        MonsterDatabase roundTrip = MonsterCatalogEditor.ToDatabase(rows);

        Assert.Contains("Blue Sentinel", roundTrip.Epic);
        Assert.Contains("My Custom Boss", roundTrip.Epic);
        Assert.Contains("Dinosaurio", roundTrip.Large);
        Assert.Contains("Rift Herald", roundTrip.Large);
        Assert.Contains("User New", roundTrip.Large);
        Assert.Equal(DefaultMonsters.Entries.Count + 3, roundTrip.All.Count);
    }

    [Fact]
    public void BuiltInMonstersAreWrittenToTheSameUnifiedCatalog()
    {
        var rows = MonsterCatalogEditor.CreateRows(new MonsterDatabase());
        Assert.Equal(DefaultMonsters.Entries.Count, rows.Count);
        Assert.True(rows.Count > LeagueMonsterAssets.OfficialNames.Count);
        Assert.Contains(rows, row => row.Name == "Void Grub" && row.IsOfficial);
        Assert.Contains(rows, row => row.Name == "Hextech Drake" && row.IsOfficial);
        Assert.All(rows, row =>
        {
            Assert.True(row.IsOfficial);
            Assert.False(row.CanDelete);
            Assert.Equal(MonsterCatalogSection.OfficialOnly, row.Section);
        });
        MonsterDatabase saved = MonsterCatalogEditor.ToDatabase(rows);
        Assert.Equal(DefaultMonsters.Get().Epic.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
            saved.Epic.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(DefaultMonsters.Get().Large.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
            saved.Large.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefaultMonsterDatabaseIsVersionedIndependentlyOfUserMonsters()
    {
        MonsterDatabase defaults = DefaultMonsters.Get();
        Assert.Contains("Rift Herald", defaults.Epic);
        Assert.Contains("Ocean Drake", defaults.Epic);
        Assert.Contains("Greater Murk Wolf", defaults.Large);
        Assert.Contains("Raptor", defaults.Large);
        defaults.Epic.Clear();
        Assert.Contains("Rift Herald", DefaultMonsters.Get().Epic);

        foreach (string name in LeagueMonsterAssets.OfficialNames)
            Assert.True(DefaultMonsters.TryResolveName(name, out _),
                $"The verified WAD asset '{name}' must be in DefaultMonsters.");
    }

    [Theory]
    [InlineData("ElderDrake", "Elder Dragon")]
    [InlineData("InfernalDragon", "Infernal Drake")]
    [InlineData("VoidGrubs", "Void Grub")]
    [InlineData("AtakhanRuinous", "Ruinous Atakhan")]
    [InlineData("Raptor", "Raptor")]
    [InlineData("Krug", "Krug")]
    public void BuiltInAliasesResolveWithoutManualEventMapping(string input, string expected)
    {
        Assert.True(DefaultMonsters.TryResolveName(input, out string name));
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData("Raptor")]
    [InlineData("Krug")]
    [InlineData("Murk Wolf")]
    [InlineData("Infernal Drake")]
    [InlineData("Ruinous Atakhan")]
    public void UnknownSpecificHudArtworkIsNotSubstitutedWithAnotherCreature(string name)
    {
        Assert.True(DefaultMonsters.TryResolveName(name, out _));
        Assert.False(LeagueMonsterAssets.TryGetPath(name, out _));
    }

    [Fact]
    public void PersonalNamesDoNotDuplicateOfficialNamesWithDifferentPunctuation()
    {
        var rows = MonsterCatalogEditor.CreateRows(new MonsterDatabase
        {
            Large = new() { "blue_sentinel", "Dinosaurio", "DINOSAURIO", "Drake" }
        });
        Assert.Equal(1, rows.Count(row => LeagueMonsterAssets.Normalize(row.Name) == "bluesentinel"));
        Assert.Equal(1, rows.Count(row => row.Name == "Dragon"));
        Assert.DoesNotContain(rows, row => row.Name == "Drake");
        Assert.Equal(1, rows.Count(row => row.Name.Equals("Dinosaurio",
            StringComparison.OrdinalIgnoreCase)));
        Assert.Single(MonsterCatalogEditor.ToDatabase(rows).Large,
            name => name.Equals("Dinosaurio", StringComparison.OrdinalIgnoreCase));
    }
}
