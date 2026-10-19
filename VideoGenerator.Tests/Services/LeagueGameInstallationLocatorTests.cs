using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Services;

public sealed class LeagueGameInstallationLocatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Game")]
    [InlineData("Game/DATA")]
    [InlineData("Game/DATA/FINAL")]
    [InlineData("Plugins")]
    [InlineData("Plugins/rcp-be-lol-game-data")]
    public void FindInstallationRootAcceptsOnlyKnownInstallationSubfolders(string relativeFolder)
    {
        using var fixture = new InstallationFixture();

        string selected = relativeFolder.Length == 0
            ? fixture.Root
            : fixture.PathAt(relativeFolder);

        Assert.Equal(fixture.Root, LeagueGameInstallationLocator.FindInstallationRoot(selected));
        Assert.Equal(fixture.Root, LeagueGameInstallationLocator.FindInstallationRoot(
            selected + Path.DirectorySeparatorChar));
    }

    [Theory]
    [InlineData("Game/DATA/Unknown")]
    [InlineData("Game/DATA/FINAL/More")]
    [InlineData("Plugins/OtherPlugin")]
    [InlineData("Plugins/rcp-be-lol-game-data/Unknown")]
    public void FindInstallationRootRejectsOtherNestedFolders(string relativeFolder)
    {
        using var fixture = new InstallationFixture();
        string nested = fixture.CreateFolder(relativeFolder);

        Assert.Null(LeagueGameInstallationLocator.FindInstallationRoot(nested));
    }

    [Fact]
    public void FindInstallationRootRejectsInvalidAndPartialInstallations()
    {
        using var fixture = new InstallationFixture();
        Assert.Null(LeagueGameInstallationLocator.FindInstallationRoot(null!));
        Assert.Null(LeagueGameInstallationLocator.FindInstallationRoot(""));
        Assert.Null(LeagueGameInstallationLocator.FindInstallationRoot("  "));
        Assert.Null(LeagueGameInstallationLocator.FindInstallationRoot("\0"));
        Assert.Null(LeagueGameInstallationLocator.FindInstallationRoot(fixture.PathAt("missing")));

        string onlyGame = fixture.CreateFolder("Incomplete/Game");
        Assert.Null(LeagueGameInstallationLocator.FindInstallationRoot(onlyGame));
        Assert.Null(LeagueGameInstallationLocator.FindInstallationRoot(fixture.PathAt("Incomplete")));

        string onlyPlugins = fixture.CreateFolder("Other/Plugins");
        Assert.Null(LeagueGameInstallationLocator.FindInstallationRoot(onlyPlugins));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Game")]
    [InlineData("Game/DATA")]
    [InlineData("Game/DATA/FINAL")]
    [InlineData("Plugins")]
    [InlineData("Plugins/rcp-be-lol-game-data")]
    public async Task InspectAsyncCountsWadsAcrossGameAndPluginsRegardlessOfSelectedFolder(string relativeFolder)
    {
        using var fixture = new InstallationFixture();
        fixture.WriteArchive("Game/DATA/FINAL/champion.wad.client");
        fixture.WriteArchive("Game/DATA/FINAL/other.WAD.CLIENT");
        fixture.WriteArchive("Plugins/rcp-be-lol-game-data/default-assets.wad");
        fixture.WriteArchive("Plugins/AnotherPlugin/assets.WAD");
        fixture.WriteArchive("Other/not-a-game.wad");
        fixture.WriteArchive("Game/DATA/FINAL/readme.txt");

        var result = await LeagueGameInstallationLocator.InspectAsync(
            relativeFolder.Length == 0 ? fixture.Root : fixture.PathAt(relativeFolder));

        Assert.Equal(fixture.Root, result.Root);
        Assert.Equal(2, result.GameWadCount);
        Assert.Equal(2, result.PluginWadCount);
        Assert.True(result.HasGameDataCatalog);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task InspectAsyncAcceptsSecondGameDataCatalogWithoutOpeningWad()
    {
        using var fixture = new InstallationFixture();
        string gameArchive = fixture.WriteArchive("Game/DATA/game.wad.client", new byte[] { 1, 2, 3 });
        string pluginArchive = fixture.WriteArchive(
            "Plugins/rcp-be-lol-game-data/default-assets2.wad", new byte[] { 4, 5, 6 });
        byte[] beforeGame = await File.ReadAllBytesAsync(gameArchive);
        byte[] beforePlugin = await File.ReadAllBytesAsync(pluginArchive);
        DateTime gameModified = File.GetLastWriteTimeUtc(gameArchive);
        DateTime pluginModified = File.GetLastWriteTimeUtc(pluginArchive);

        var result = await LeagueGameInstallationLocator.InspectAsync(fixture.Root);

        Assert.True(result.IsValid);
        Assert.Equal(1, result.GameWadCount);
        Assert.Equal(1, result.PluginWadCount);
        Assert.Equal(beforeGame, await File.ReadAllBytesAsync(gameArchive));
        Assert.Equal(beforePlugin, await File.ReadAllBytesAsync(pluginArchive));
        Assert.Equal(gameModified, File.GetLastWriteTimeUtc(gameArchive));
        Assert.Equal(pluginModified, File.GetLastWriteTimeUtc(pluginArchive));
    }

    [Fact]
    public async Task InspectAsyncReportsMissingGameArchivesWithoutTreatingPluginAsEnough()
    {
        using var fixture = new InstallationFixture();
        fixture.WriteArchive("Plugins/rcp-be-lol-game-data/default-assets.wad");

        var result = await LeagueGameInstallationLocator.InspectAsync(fixture.PathAt("Game/DATA"));

        Assert.Equal(fixture.Root, result.Root);
        Assert.Equal(0, result.GameWadCount);
        Assert.Equal(1, result.PluginWadCount);
        Assert.True(result.HasGameDataCatalog);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task InspectAsyncReportsMissingPluginArchives()
    {
        using var fixture = new InstallationFixture();
        fixture.WriteArchive("Game/DATA/FINAL/champion.wad.client");

        var result = await LeagueGameInstallationLocator.InspectAsync(fixture.Root);

        Assert.Equal(fixture.Root, result.Root);
        Assert.Equal(1, result.GameWadCount);
        Assert.Equal(0, result.PluginWadCount);
        Assert.False(result.HasGameDataCatalog);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task InspectAsyncRejectsUnrelatedPluginArchivesWithoutTheRequiredCatalog()
    {
        using var fixture = new InstallationFixture();
        fixture.WriteArchive("Game/DATA/FINAL/champion.wad.client");
        fixture.WriteArchive("Plugins/OtherPlugin/assets.wad");
        fixture.WriteArchive("Plugins/rcp-be-lol-game-data/unknown.wad");

        var result = await LeagueGameInstallationLocator.InspectAsync(fixture.Root);

        Assert.Equal(1, result.GameWadCount);
        Assert.Equal(2, result.PluginWadCount);
        Assert.False(result.HasGameDataCatalog);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task InspectAsyncReturnsMissingStateForUnknownPaths()
    {
        using var fixture = new InstallationFixture();
        var missing = await LeagueGameInstallationLocator.InspectAsync(fixture.PathAt("missing"));

        Assert.Null(missing.Root);
        Assert.Equal(0, missing.GameWadCount);
        Assert.Equal(0, missing.PluginWadCount);
        Assert.False(missing.HasGameDataCatalog);
        Assert.False(missing.IsValid);
    }

    [Fact]
    public async Task InspectAsyncHonorsAlreadyCancelledTokens()
    {
        using var fixture = new InstallationFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LeagueGameInstallationLocator.InspectAsync(fixture.Root, cancellation.Token));
    }

    private sealed class InstallationFixture : IDisposable
    {
        public string Root { get; } =
            Directory.CreateTempSubdirectory("VideoGenerator.LeagueLocator.").FullName;

        public InstallationFixture()
        {
            CreateFolder("Game/DATA/FINAL");
            CreateFolder("Plugins/rcp-be-lol-game-data");
        }

        public string PathAt(string relative) => Path.Combine(Root,
            relative.Replace('/', Path.DirectorySeparatorChar));

        public string CreateFolder(string relative) =>
            Directory.CreateDirectory(PathAt(relative)).FullName;

        public string WriteArchive(string relative, byte[]? contents = null)
        {
            string file = PathAt(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            // Deliberately not a valid WAD: installation scanning must read filenames,
            // never open the archive, parse assets or send a network request.
            File.WriteAllBytes(file, contents ?? new byte[] { 1, 2, 3, 4 });
            return file;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
