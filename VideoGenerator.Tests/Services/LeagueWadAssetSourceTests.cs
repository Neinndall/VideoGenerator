using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VideoGenerator.Services;
using Xunit;
using Xunit.Abstractions;

namespace VideoGenerator.Tests.Services;

public sealed class LeagueWadAssetSourceTests
{
    private readonly ITestOutputHelper _output;

    public LeagueWadAssetSourceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task FindsGameAndPluginWadsAndReadsDecompressedRawBytesByPathAndHash()
    {
        using var fixture = new WadFixture();
        byte[] content = Encoding.UTF8.GetBytes("binary asset bytes");
        fixture.AddWad("Game/DATA/champion.wad.client",
            ("ASSETS/Characters/Ahri/data/sample.bin", content));
        fixture.AddWad("Plugins/rcp-be-lol-game-data/default-assets.wad",
            ("assets/ui/extra.bin", new byte[] { 42, 43 }));

        var source = fixture.CreateSource();
        Assert.True(source.IsAvailable);
        Assert.Equal(2, source.WadFiles.Count);
        Assert.Equal(content, await source.ReadAssetBytesAsync(
            "ASSETS\\CHARACTERS\\AHRI\\DATA\\SAMPLE.BIN"));
        Assert.Equal(content, await source.ReadAssetBytesAsync(
            LeagueWadAssetSource.ComputePathHash("assets/characters/ahri/data/sample.bin")));
        Assert.Equal(new byte[] { 42, 43 }, await source.ReadAssetBytesAsync("assets/ui/extra.bin"));
        Assert.Null(await source.ReadAssetBytesAsync("assets/unknown.bin"));
    }

    [Fact]
    public async Task OpeningGameDirectoryAlsoDiscoversSiblingPluginWads()
    {
        using var fixture = new WadFixture();
        fixture.AddWad("Game/DATA/sample.wad.client", ("assets/sample.bin", new byte[] { 11 }));
        fixture.AddWad("Plugins/game-assets/another.wad", ("assets/another.bin", new byte[] { 12 }));

        var source = fixture.CreateSource(Path.Combine(fixture.Root, "Game"));
        Assert.Equal(2, source.WadFiles.Count);
        Assert.Equal(new byte[] { 12 }, await source.ReadAssetBytesAsync("assets/another.bin"));
    }

    [Fact]
    public async Task MissingDirectoryOrUnregisteredIconDoesNotGuessPathsOrDownload()
    {
        using var fixture = new WadFixture();
        var source = fixture.CreateSource(Path.Combine(fixture.Root, "missing"));

        Assert.False(source.IsAvailable);
        Assert.Null(await source.ReadAssetBytesAsync("assets/characters/ahri/hud/ahri_circle.png"));
        Assert.Null(await source.FindIconAsync("champion", "Ahri"));
        Assert.Null(await source.FindIconAsync(null!, null!));
    }

    [Fact]
    public async Task MissingConfiguredGameDirectoryNeverAutoDetectsExistingRiotArchives()
    {
        using var fixture = new WadFixture();
        string installedRoot = Path.Combine(fixture.Root, "Riot Games", "League of Legends");
        // The archive is real and would be discovered if this root were requested.
        fixture.AddWad("Riot Games/League of Legends/Game/DATA/champion.wad.client",
            ("assets/hero.bin", new byte[] { 1 }));

        const string variable = "LEAGUE_GAME_DIRECTORY";
        string? previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, installedRoot);
            foreach (string? unsetPath in new string?[] { null, "", "  " })
            {
                var source = new LeagueWadAssetSource(new LogService(), unsetPath,
                    Path.Combine(fixture.Root, "IconCache"), () => null!);
                Assert.Null(source.GameDirectory);
                Assert.False(source.IsAvailable);
                Assert.Empty(source.WadFiles);
                Assert.Null(await source.ReadAssetBytesAsync("assets/hero.bin"));
                Assert.False(source.RefreshArchives());
            }

            // Explicitly selected locations still work, including this installation.
            var configured = new LeagueWadAssetSource(new LogService(), installedRoot,
                Path.Combine(fixture.Root, "IconCache"), () => null!);
            Assert.True(configured.IsAvailable);
            Assert.Equal(new byte[] { 1 },
                await configured.ReadAssetBytesAsync("assets/hero.bin"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public async Task UpdateGameDirectoryDynamicallyActivatesAndUnloadsArchives()
    {
        using var fixture = new WadFixture();
        fixture.AddWad("Game/data/champion.wad.client",
            ("assets/hero.bin", new byte[] { 42 }));
        string installedRoot = fixture.Root;

        // Initially created with no game directory
        var source = new LeagueWadAssetSource(new LogService(), null,
            Path.Combine(fixture.Root, "IconCache"), () => null!);
        Assert.Null(source.GameDirectory);
        Assert.False(source.IsAvailable);
        Assert.Empty(source.WadFiles);
        Assert.Null(await source.ReadAssetBytesAsync("assets/hero.bin"));

        // Dynamically activate by setting game directory
        bool updated = source.UpdateGameDirectory(installedRoot);
        Assert.True(updated);
        Assert.True(source.IsAvailable);
        Assert.NotEmpty(source.WadFiles);
        Assert.Equal(installedRoot, source.GameDirectory);
        byte[]? read = await source.ReadAssetBytesAsync("assets/hero.bin");
        Assert.NotNull(read);
        Assert.Equal(new byte[] { 42 }, read);

        // Dynamically unload
        source.UpdateGameDirectory(null);
        Assert.False(source.IsAvailable);
        Assert.Empty(source.WadFiles);
        Assert.Null(source.GameDirectory);
    }

    [Fact]
    public async Task RegisteredPngConvertsAndCachesWithoutReopeningMissingWad()
    {
        using var fixture = new WadFixture();
        byte[] rawPng = CreatePng();
        string wadPath = fixture.AddWad("Game/data/champion.wad.client",
            ("assets/characters/ahri/hud/ahri_circle.png", rawPng));
        var source = fixture.CreateSource();
        source.RegisterIconAssetPath("champion", "Ahri", "assets/characters/ahri/hud/ahri_circle.png");

        string icon = await source.FindIconAsync("champion", "Ahri");

        Assert.NotNull(icon);
        Assert.True(File.Exists(icon));
        Assert.EndsWith(".png", icon, StringComparison.OrdinalIgnoreCase);
        using (var image = Image.Load(icon))
        {
            Assert.Equal(2, image.Width);
            Assert.Equal(2, image.Height);
        }

        File.Delete(wadPath);
        Assert.Equal(icon, await source.FindIconAsync("champion", "Ahri"));
    }

    [Fact]
    public async Task ConvertsKnownTEXBgraFixtureToPngViaLeagueToolkit()
    {
        using var fixture = new WadFixture();
        fixture.AddWad("Game/data/texture.wad", ("assets/characters/ahri/skins/skin0/hud.tex", MakeBgraTex()));
        var source = fixture.CreateSource();
        ulong hash = LeagueWadAssetSource.ComputePathHash("assets/characters/ahri/skins/skin0/hud.tex");
        source.RegisterIconAssetHash("champion", "AhriSkin0", hash);

        string icon = await source.FindIconAsync("champion", "AhriSkin0");

        Assert.NotNull(icon);
        using var image = Image.Load(icon);
        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
    }

    [Fact]
    public async Task ConvertsKnownDdsBc1FixtureToPngViaLeagueToolkit()
    {
        using var fixture = new WadFixture();
        fixture.AddWad("Game/data/texture.wad.client", ("assets/test/hero.dds", MakeBc1Dds()));
        var source = fixture.CreateSource();
        source.RegisterIconAssetPath("champion", "Hero", "assets/test/hero.dds");

        string icon = await source.FindIconAsync("champion", "Hero");

        Assert.NotNull(icon);
        using var image = Image.Load(icon);
        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
    }

    [Fact]
    public async Task IgnoresBadWadAndBadImageAndTriesNextRegisteredAsset()
    {
        using var fixture = new WadFixture();
        string invalidPath = Path.Combine(fixture.Root, "Game", "invalid.wad.client");
        Directory.CreateDirectory(Path.GetDirectoryName(invalidPath)!);
        File.WriteAllBytes(invalidPath, [0, 1, 2, 3]);
        fixture.AddWad("Game/data/valid.wad.client",
            ("assets/broken.tex", [0, 1, 2, 3]),
            ("assets/valid.png", CreatePng()));
        var source = fixture.CreateSource();
        source.RegisterIconAssetPath("champion", "Ahri", "assets/broken.tex");
        source.RegisterIconAssetPath("champion", "Ahri", "assets/valid.png");

        string icon = await source.FindIconAsync("champion", "Ahri");

        Assert.NotNull(icon);
        using var image = Image.Load(icon);
        Assert.Equal(2, image.Width);
    }

    [Fact]
    public async Task HonorsCancellationAndDoesNotChangeRawArchive()
    {
        using var fixture = new WadFixture();
        byte[] payload = [1, 2, 3, 4];
        string archive = fixture.AddWad("Plugins/sample.wad", ("assets/icon.bin", payload));
        byte[] original = await File.ReadAllBytesAsync(archive);
        var source = fixture.CreateSource();
        using var tokenSource = new CancellationTokenSource();
        tokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            source.ReadAssetBytesAsync("assets/icon.bin", tokenSource.Token));
        Assert.Equal(original, await File.ReadAllBytesAsync(archive));
    }

    [Fact]
    public async Task RefreshArchivesDetectsNewAndRemovedGameAndPluginWadsWithoutReconstruction()
    {
        using var fixture = new WadFixture();
        string original = fixture.AddWad("Game/DATA/old.wad.client",
            ("assets/old.bin", new byte[] { 1 }));
        var source = fixture.CreateSource();
        string firstRevision = source.ArchiveRevision;
        Assert.Equal(new byte[] { 1 }, await source.ReadAssetBytesAsync("assets/old.bin"));

        string added = fixture.AddWad("Plugins/rcp-be-lol-game-data/new.wad",
            ("assets/new.bin", new byte[] { 2 }));
        Assert.Single(source.WadFiles);
        Assert.Null(await source.ReadAssetBytesAsync("assets/new.bin"));

        Assert.True(source.RefreshArchives());
        Assert.NotEqual(firstRevision, source.ArchiveRevision);
        Assert.Equal(2, source.WadFiles.Count);
        Assert.Contains(added, source.WadFiles);
        Assert.Contains(LeagueWadAssetSource.ComputePathHash("assets/new.bin"),
            source.GetAvailableAssetHashes());
        Assert.Equal(new byte[] { 2 }, await source.ReadAssetBytesAsync("assets/new.bin"));
        Assert.False(source.RefreshArchives());
        Assert.Equal(source.ArchiveRevision, fixture.CreateSource().ArchiveRevision);

        File.Delete(original);
        Assert.True(source.RefreshArchives());
        Assert.Single(source.WadFiles);
        Assert.Null(await source.ReadAssetBytesAsync("assets/old.bin"));
        Assert.Equal(new byte[] { 2 }, await source.ReadAssetBytesAsync("assets/new.bin"));
    }

    [Fact]
    public async Task RefreshArchivesFindsPluginFolderCreatedAfterOpeningGameDirectory()
    {
        using var fixture = new WadFixture();
        fixture.AddWad("Game/DATA/base.wad.client",
            ("assets/base.bin", new byte[] { 1 }));
        var source = fixture.CreateSource(Path.Combine(fixture.Root, "Game"));
        Assert.Single(source.WadFiles);

        fixture.AddWad("Plugins/rcp-be-lol-game-data/default-assets.wad",
            ("assets/plugin.bin", new byte[] { 2 }));
        Assert.True(source.RefreshArchives());
        Assert.Equal(2, source.WadFiles.Count);
        Assert.Equal(new byte[] { 2 }, await source.ReadAssetBytesAsync("assets/plugin.bin"));
    }

    [Fact]
    public async Task RefreshArchivesRecognizesSameSizedAssetReplacementByModifiedTimestamp()
    {
        using var fixture = new WadFixture();
        string path = fixture.AddWad("Game/DATA/replaced.wad.client",
            ("assets/same.bin", new byte[] { 1, 2, 3, 4 }));
        var source = fixture.CreateSource();
        Assert.Equal(new byte[] { 1, 2, 3, 4 },
            await source.ReadAssetBytesAsync("assets/same.bin"));
        string originalRevision = source.ArchiveRevision;
        long originalLength = new FileInfo(path).Length;

        fixture.AddWad("Game/DATA/replaced.wad.client",
            ("assets/same.bin", new byte[] { 5, 6, 7, 8 }));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(10));
        Assert.Equal(originalLength, new FileInfo(path).Length);
        Assert.True(source.RefreshArchives());
        Assert.NotEqual(originalRevision, source.ArchiveRevision);
        Assert.Equal(new byte[] { 5, 6, 7, 8 },
            await source.ReadAssetBytesAsync("assets/same.bin"));
    }

    [Fact]
    public async Task RefreshArchivesReplacesExistingWadAndDoesNotServePreviousPng()
    {
        using var fixture = new WadFixture();
        const string assetPath = "assets/characters/ahri/hud/icon.png";
        string path = fixture.AddWad("Game/DATA/champion.wad.client",
            (assetPath, CreatePng(new Rgba32(255, 0, 0))));
        var source = fixture.CreateSource();
        source.RegisterIconAssetPath("champion", "Ahri", assetPath);

        string previousIcon = await source.FindIconAsync("champion", "Ahri");
        Assert.NotNull(previousIcon);
        using (var previous = Image.Load<Rgba32>(previousIcon))
            Assert.Equal((byte)255, previous[0, 0].R);

        string revision = source.ArchiveRevision;
        // Same WAD filename, same asset hash. A patch can replace the archive in place.
        fixture.AddWad("Game/DATA/champion.wad.client",
            (assetPath, CreatePng(new Rgba32(0, 0, 255))));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));

        // The caller explicitly chooses when to rescan the installation.
        Assert.Equal(previousIcon, await source.FindIconAsync("champion", "Ahri"));
        Assert.True(source.RefreshArchives());
        Assert.NotEqual(revision, source.ArchiveRevision);
        string updatedIcon = await source.FindIconAsync("champion", "Ahri");
        Assert.NotNull(updatedIcon);
        Assert.NotEqual(previousIcon, updatedIcon);
        using (var updated = Image.Load<Rgba32>(updatedIcon))
        {
            Assert.Equal((byte)0, updated[0, 0].R);
            Assert.Equal((byte)255, updated[0, 0].B);
        }
        Assert.Equal(updatedIcon, await source.FindIconAsync("champion", "Ahri"));
    }

    [Fact]
    public async Task RefreshArchivesInvalidatesCachedPngWhenWadIsRemoved()
    {
        using var fixture = new WadFixture();
        const string asset = "assets/icon.png";
        string path = fixture.AddWad("Game/DATA/icon.wad.client", (asset, CreatePng()));
        var source = fixture.CreateSource();
        source.RegisterIconAssetPath("champion", "Icon", asset);
        Assert.NotNull(await source.FindIconAsync("champion", "Icon"));

        File.Delete(path);
        Assert.True(source.RefreshArchives());
        Assert.False(source.IsAvailable);
        Assert.Empty(source.WadFiles);
        Assert.Null(await source.FindIconAsync("champion", "Icon"));
    }

    [Fact]
    public async Task RefreshArchivesBoundsRetainedPngRevisionsAcrossRepeatedPatches()
    {
        using var fixture = new WadFixture();
        const string asset = "assets/icon.png";
        string path = fixture.AddWad("Game/DATA/icons.wad.client",
            (asset, CreatePng(new Rgba32(1, 0, 0))));
        var source = fixture.CreateSource();
        source.RegisterIconAssetPath("champion", "Icon", asset);
        string currentIcon = await source.FindIconAsync("champion", "Icon");

        for (int version = 2; version <= 6; version++)
        {
            fixture.AddWad("Game/DATA/icons.wad.client",
                (asset, CreatePng(new Rgba32((byte)version, 0, 0))));
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(version));
            Assert.True(source.RefreshArchives());
            currentIcon = await source.FindIconAsync("champion", "Icon");
            using var image = Image.Load<Rgba32>(currentIcon);
            Assert.Equal((byte)version, image[0, 0].R);
        }

        Assert.Equal(3, Directory.GetFiles(Path.GetDirectoryName(currentIcon)!, "*.png").Length);
    }

    [Fact]
    public async Task RefreshArchivesNeverDeletesManuallySelectedBackgroundPng()
    {
        using var fixture = new WadFixture();
        const string asset = "assets/splash.png";
        string wad = fixture.AddWad("Game/DATA/splashes.wad.client",
            (asset, CreatePng(new Rgba32(1, 0, 0))));
        string? selectedBackground = null;
        var source = fixture.CreateSource(protectedBackgroundPath: () => selectedBackground);
        source.RegisterIconAssetPath("splash", "Hero", asset);

        string firstIcon = await source.FindIconAsync("splash", "Hero");
        Assert.NotNull(firstIcon);
        File.SetLastWriteTimeUtc(firstIcon, DateTime.UtcNow.AddDays(-2));
        // Resolve an equivalent path through '.' as a real settings path can vary.
        selectedBackground = Path.Combine(Path.GetDirectoryName(firstIcon)!, ".",
            Path.GetFileName(firstIcon));

        string currentIcon = firstIcon;
        for (int version = 2; version <= 6; version++)
        {
            fixture.AddWad("Game/DATA/splashes.wad.client",
                (asset, CreatePng(new Rgba32((byte)version, 0, 0))));
            File.SetLastWriteTimeUtc(wad, DateTime.UtcNow.AddMinutes(version));
            Assert.True(source.RefreshArchives());
            currentIcon = await source.FindIconAsync("splash", "Hero");
        }

        Assert.True(File.Exists(firstIcon));
        Assert.NotEqual(firstIcon, currentIcon);
        Assert.Equal(4, Directory.GetFiles(Path.GetDirectoryName(firstIcon)!, "*.png").Length);

        selectedBackground = null;
        fixture.AddWad("Game/DATA/splashes.wad.client",
            (asset, CreatePng(new Rgba32(7, 0, 0))));
        File.SetLastWriteTimeUtc(wad, DateTime.UtcNow.AddMinutes(7));
        Assert.True(source.RefreshArchives());
        Assert.NotNull(await source.FindIconAsync("splash", "Hero"));
        Assert.False(File.Exists(firstIcon));
        Assert.Equal(3, Directory.GetFiles(Path.GetDirectoryName(currentIcon)!, "*.png").Length);
    }

    [Fact]
    public async Task CatalogGenerationDoesNotCreateDuplicatePngForSameWadRevision()
    {
        using var fixture = new WadFixture();
        const string asset = "assets/hero.png";
        fixture.AddWad("Game/DATA/hero.wad.client", (asset, CreatePng()));
        var source = fixture.CreateSource();

        source.RegisterIconAssetPath("champion", "Hero@catalog1", asset);
        string first = await source.FindIconAsync("champion", "Hero@catalog1");
        source.RegisterIconAssetPath("champion", "Hero@catalog2", asset);
        string second = await source.FindIconAsync("champion", "Hero@catalog2");

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(first)!, "*.png"));
    }

    [Fact]
    public async Task OptionalLocalRetailSmokeReadsPluginWadByRealHash()
    {
        // Opt-in only: default automated test runs never access an installed game client.
        if (Environment.GetEnvironmentVariable("VG_TEST_RETAIL_WAD") != "1") return;

        const string root = @"C:\Riot Games\League of Legends\Plugins\rcp-be-lol-game-data";
        const string skinsPath = "plugins/rcp-be-lol-game-data/global/default/v1/skins.json";
        const string tilePath =
            "plugins/rcp-be-lol-game-data/global/default/assets/characters/ahri/skins/skin07/images/ahri_splash_tile_7.jpg";
        string outputRoot = Path.Combine(Path.GetTempPath(), "VideoGenerator.WadRetailSmoke." + Guid.NewGuid().ToString("N"));
        try
        {
            var source = new LeagueWadAssetSource(new LogService(), root, outputRoot);
            Assert.True(source.IsAvailable, "Retail game-data plugin WAD not installed at the supplied path.");
            _output.WriteLine($"Discovered {source.WadFiles.Count} plugin WAD archives.");

            Assert.Equal(0x893FFB0B2DB8ECFAUL, LeagueWadAssetSource.ComputePathHash(skinsPath));
            var skinsBytes = await source.ReadAssetBytesAsync(skinsPath);
            Assert.NotNull(skinsBytes);
            using (var skins = JsonDocument.Parse(skinsBytes))
                Assert.Equal(JsonValueKind.Object, skins.RootElement.ValueKind);
            _output.WriteLine($"Retail skins.json: {skinsBytes.Length} uncompressed bytes.");

            byte[]? tileBytes = await source.ReadAssetBytesAsync(tilePath);
            Assert.NotNull(tileBytes);
            _output.WriteLine($"Retail Ahri skin tile: {tileBytes.Length} raw bytes.");

            source.RegisterIconAssetPath("champion", "AhriSkin7", tilePath);
            string? icon = await source.FindIconAsync("champion", "AhriSkin7");
            Assert.NotNull(icon);
            using var image = Image.Load(icon);
            Assert.True(image.Width > 0 && image.Height > 0);
            _output.WriteLine($"Retail Ahri skin tile decoded to PNG: {image.Width}x{image.Height}.");
        }
        finally
        {
            if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, recursive: true);
        }
    }

    private static byte[] CreatePng(Rgba32? color = null)
    {
        using var image = new Image<Rgba32>(2, 2);
        if (color.HasValue) image[0, 0] = color.Value;
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }

    private static byte[] MakeBgraTex()
    {
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("TEX\0"u8);
            writer.Write((ushort)2);
            writer.Write((ushort)2);
            writer.Write((byte)0);
            writer.Write((byte)20); // BGRA8, inspected from Texture.MapExtendedTextureFormat
            writer.Write((byte)0);
            writer.Write((byte)0); // No mipmaps
            for (int i = 0; i < 4; i++) writer.Write(new byte[] { 0, 0, 255, 255 });
        }
        return output.ToArray();
    }

    private static byte[] MakeBc1Dds()
    {
        // DDS 4x4, one BC1/DXT1 block (solid red): deterministic valid DDS fixture.
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("DDS "u8);
            writer.Write(124); // DDS_HEADER size
            writer.Write(0x00081007u); // CAPS | HEIGHT | WIDTH | PIXELFORMAT | LINEARSIZE
            writer.Write(4); // height
            writer.Write(4); // width
            writer.Write(8); // compressed data length
            writer.Write(0); // depth
            writer.Write(1); // mip count
            for (int i = 0; i < 11; i++) writer.Write(0); // reserved
            writer.Write(32); // DDS_PIXELFORMAT size
            writer.Write(4); // FOURCC flag
            writer.Write("DXT1"u8);
            for (int i = 0; i < 5; i++) writer.Write(0); // channel fields
            writer.Write(0x1000); // DDSCAPS_TEXTURE
            for (int i = 0; i < 4; i++) writer.Write(0);
            writer.Write((ushort)0xf800); // RGB565 red
            writer.Write((ushort)0x07e0); // RGB565 green
            writer.Write(0u); // all pixels use color 0
        }
        return output.ToArray();
    }

    private sealed class WadFixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("VideoGenerator.Wad.").FullName;

        public string AddWad(string relative, params (string Path, byte[] Data)[] contents)
        {
            string output = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            // Deterministic WAD v3.1 fixture using the descriptor layout documented by
            // LeagueToolkit's WadChunk/WadFile sources. No project reference in the test csproj.
            using var file = File.Create(output);
            using var writer = new BinaryWriter(file);
            writer.Write("RW"u8);
            writer.Write((byte)3);
            writer.Write((byte)1);
            writer.Write(new byte[256]);
            writer.Write(0UL);
            writer.Write(contents.Length);
            int offset = 272 + 32 * contents.Length;
            foreach (var item in contents)
            {
                writer.Write(LeagueWadAssetSource.ComputePathHash(item.Path));
                writer.Write((uint)offset);
                writer.Write(item.Data.Length);
                writer.Write(item.Data.Length);
                writer.Write((byte)0); // WadChunkCompression.None
                writer.Write((byte)0);
                writer.Write((ushort)0);
                writer.Write(0UL);
                offset += item.Data.Length;
            }
            foreach (var item in contents) writer.Write(item.Data);
            return output;
        }

        public LeagueWadAssetSource CreateSource(string? directory = null,
            Func<string>? protectedBackgroundPath = null) =>
            new(new LogService(), directory ?? Root, Path.Combine(Root, "IconCache"),
                protectedBackgroundPath);

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
