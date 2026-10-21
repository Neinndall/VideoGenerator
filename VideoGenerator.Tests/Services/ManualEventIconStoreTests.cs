using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Services;

public sealed class ManualEventIconStoreTests
{
    [Fact]
    public async Task ImportPersistsSquarePngAcrossReloadAndPreservesSource()
    {
        string root = Directory.CreateTempSubdirectory("VG.ManualIcons.").FullName;
        try
        {
            string sourceFolder = Path.Combine(root, "Events", "AkaliSkin102_EvelynnSkin65");
            string sourceImage = Path.Combine(root, "custom image.png");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFolder)!);
            using (var image = new Image<Rgba32>(800, 400))
                await image.SaveAsPngAsync(sourceImage);
            byte[] original = await File.ReadAllBytesAsync(sourceImage);

            var store = new ManualEventIconStore(root);
            string first = await store.ImportAsync(sourceFolder, sourceImage);
            Assert.True(File.Exists(first));
            using (var image = await Image.LoadAsync<Rgba32>(first))
                Assert.Equal((512, 512), (image.Width, image.Height));
            Assert.Equal(original, await File.ReadAllBytesAsync(sourceImage));

            var reloaded = new ManualEventIconStore(root);
            Assert.Equal(first, reloaded.GetIconPath(sourceFolder));
            Assert.Null(reloaded.GetIconPath(Path.Combine(root, "Events", "Other")));
            string second = await reloaded.ImportAsync(sourceFolder, sourceImage);
            Assert.NotEqual(first, second);
            Assert.Equal(second, store.GetIconPath(sourceFolder));
            reloaded.RemoveOverride(sourceFolder);
            Assert.Null(store.GetIconPath(sourceFolder));
            Assert.True(File.Exists(first)); // Never clean unknown/shared personal files.
            Assert.True(File.Exists(second));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CorruptedIndexIsNotReplacedAndSourceFileIsNotTouched()
    {
        string root = Directory.CreateTempSubdirectory("VG.ManualIcons.").FullName;
        try
        {
            string config = Path.Combine(root, "Config");
            Directory.CreateDirectory(config);
            string index = Path.Combine(config, "manual_event_icons.json");
            await File.WriteAllTextAsync(index, "{malformed json");
            string picture = Path.Combine(root, "icon.png");
            using (var image = new Image<Rgba32>(16, 16))
                await image.SaveAsPngAsync(picture);

            var store = new ManualEventIconStore(root);
            await Assert.ThrowsAnyAsync<Exception>(() =>
                store.ImportAsync(Path.Combine(root, "Event"), picture));
            Assert.Equal("{malformed json", await File.ReadAllTextAsync(index));
            Assert.True(File.Exists(picture));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsOversizeSourceBeforeChangingStoredOverride()
    {
        string root = Directory.CreateTempSubdirectory("VG.ManualIcons.").FullName;
        try
        {
            string source = Path.Combine(root, "icon.png");
            using (var image = new Image<Rgba32>(64, 64))
                await image.SaveAsPngAsync(source);
            var store = new ManualEventIconStore(root);
            string folder = Path.Combine(root, "Folder");
            string existing = await store.ImportAsync(folder, source);
            string oversized = Path.Combine(root, "large.txt");
            await File.WriteAllBytesAsync(oversized, new byte[20 * 1024 * 1024 + 1]);
            await Assert.ThrowsAsync<InvalidDataException>(() => store.ImportAsync(folder, oversized));
            Assert.Equal(existing, store.GetIconPath(folder));
        }
        finally { Directory.Delete(root, true); }
    }
}
