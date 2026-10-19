using System;
using System.IO;
using System.Linq;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests.Services;

public sealed class StructureCatalogEditorTests
{
    [Fact]
    public void BuiltInStructuresAreSavedAlongsideCustomMappingsInOneJson()
    {
        var rows = StructureCatalogEditor.CreateRows(Array.Empty<StructureMapping>());
        Assert.Equal(DefaultStructures.Entries.Count, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.True(row.IsOfficial);
            Assert.False(row.CanDelete);
        });
        Assert.Contains(rows, row => row.Keyword == "BlueTurret" && row.TargetName == "Turret");
        Assert.Contains(rows, row => row.Keyword == "InhibitorTurret" && row.TargetName == "Turret");
        Assert.Contains(rows, row => row.Keyword == "BlueNexus" && row.TargetName == "Nexus");
        Assert.Equal(DefaultStructures.Entries.Count,
            StructureCatalogEditor.ToDatabaseMappings(rows).Count);
        Assert.NotSame(DefaultStructures.Get(), DefaultStructures.Get());
    }

    [Fact]
    public void LegacyDefaultCopiesAreNotSavedBackButCustomMappingsArePreserved()
    {
        var oldFile = new[]
        {
            new StructureMapping { Keyword = "Tower", TargetName = "Turret" },
            new StructureMapping { Keyword = "ObsidianGate", TargetName = "Nexus" }
        };
        var rows = StructureCatalogEditor.CreateRows(oldFile);
        Assert.True(Assert.Single(rows, row => row.Keyword == "Tower").IsOfficial);
        Assert.True(Assert.Single(rows, row => row.Keyword == "ObsidianGate").CanDelete);

        var serialized = StructureCatalogEditor.ToDatabaseMappings(rows);
        Assert.Equal(DefaultStructures.Entries.Count + 1, serialized.Count);
        Assert.Equal("Nexus", Assert.Single(serialized, x => x.Keyword == "ObsidianGate").TargetName);
    }

    [Fact]
    public void ExplicitUserOverrideIsPreservedAndResolvesBeforeBuiltInKeyword()
    {
        var personal = new[]
        {
            new StructureMapping { Keyword = "BlueTurret", TargetName = "Nexus" }
        };
        var rows = StructureCatalogEditor.CreateRows(personal);
        Assert.True(Assert.Single(rows, row => row.Keyword == "BlueTurret").CanDelete);
        Assert.Equal(DefaultStructures.Entries.Count,
            StructureCatalogEditor.ToDatabaseMappings(rows).Count);

        var active = StructureCatalogEditor.ForRecognition(personal);
        Assert.Equal("Nexus", active.First(m => m.Keyword == "BlueTurret").TargetName);
        Assert.Equal("Turret", active.First(m => m.Keyword == "Turret").TargetName);
    }

    [Theory]
    [InlineData("{ broken")]
    [InlineData("{}")]
    [InlineData("[{\"Keyword\":\"Turret\",\"TargetName\":\"Nexus\",\"Extra\":true}]")]
    [InlineData("[{\"Keyword\":\"\",\"TargetName\":\"Turret\"}]")]
    public void MalformedUserDataIsNeverOverwritten(string json)
    {
        string root = Directory.CreateTempSubdirectory("VG.StructureConfig.").FullName;
        try
        {
            string path = Path.Combine(root, "Config", "structures.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
            var data = new DataFetcher(new LogService(), root);
            Assert.False(data.TryLoadStructureMappings(out _));
            Assert.Equal(json, File.ReadAllText(path));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void UserStructureFileUsesSameIsolatedConfigAsTheParser()
    {
        string root = Directory.CreateTempSubdirectory("VG.StructureConfig.").FullName;
        try
        {
            var data = new DataFetcher(new LogService(), root);
            Assert.True(data.TryLoadStructureMappings(out var empty));
            Assert.Equal(DefaultStructures.Entries.Count, empty.Count);
            string path = Path.Combine(root, "Config", "structures.json");
            Assert.False(File.Exists(path));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                "[{\"Keyword\":\"ObsidianGate\",\"TargetName\":\"Nexus\"}]");
            Assert.True(data.TryLoadStructureMappings(out var personal));
            Assert.Equal("Nexus",
                Assert.Single(personal, x => x.Keyword == "ObsidianGate").TargetName);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
