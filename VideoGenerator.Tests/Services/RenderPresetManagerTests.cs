using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using Xunit;

namespace VideoGenerator.Tests.Services;

public sealed class RenderPresetManagerTests : IDisposable
{
    private readonly string _tempDir;

    public RenderPresetManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vg_test_presets_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    [Fact]
    public void BuiltInPresets_AreValidAndHaveDescriptions()
    {
        var builtIns = TimelineExportPreset.BuiltInPresets;
        Assert.NotEmpty(builtIns);
        Assert.Contains(builtIns, p => p.Width == 1920 && p.Height == 1080 && p.Fps == 60);
        Assert.Contains(builtIns, p => p.Width == 1080 && p.Height == 1920); // TikTok vertical
        Assert.All(builtIns, p =>
        {
            Assert.Empty(p.Validate());
            Assert.True(p.IsBuiltIn);
            Assert.False(string.IsNullOrWhiteSpace(p.SummaryDisplay));
            Assert.False(string.IsNullOrWhiteSpace(p.AspectRatioDisplay));
        });
    }

    [Fact]
    public async Task SavePresetAsync_PersistsCustomPresetToIsolatedStorage()
    {
        var manager = new RenderPresetManager();
        await manager.InitializeAsync(_tempDir);

        var custom = new TimelineExportPreset(
            Name: "Mi Preset 1440p Custom",
            Width: 2560,
            Height: 1440,
            Fps: 60,
            Crf: 18,
            AudioBitrate: "320k",
            AudioSampleRate: 48000,
            Encoder: "libx264",
            FfmpegPreset: "slow",
            IsBuiltIn: false,
            Description: "Preset para gaming ultra"
        );

        var saved = await manager.SavePresetAsync(custom, _tempDir);
        Assert.False(saved.IsBuiltIn);

        var all = manager.GetAllPresets();
        Assert.Contains(all, p => p.Name == "Mi Preset 1440p Custom" && p.Width == 2560 && p.AudioBitrate == "320k");

        var customList = manager.GetCustomPresets();
        Assert.Single(customList);
        Assert.Equal("Mi Preset 1440p Custom", customList[0].Name);

        // Reload fresh from disk to verify atomic persistence
        var manager2 = new RenderPresetManager();
        await manager2.InitializeAsync(_tempDir);
        var loaded = manager2.GetCustomPresets();
        Assert.Single(loaded);
        Assert.Equal("Mi Preset 1440p Custom", loaded[0].Name);
        Assert.Equal(2560, loaded[0].Width);
    }

    [Fact]
    public async Task DeleteCustomPresetAsync_CannotDeleteBuiltIn_CanDeleteCustom()
    {
        var manager = new RenderPresetManager();
        await manager.InitializeAsync(_tempDir);

        // Attempting to delete a built-in should return false and not remove it
        bool builtInDeleted = await manager.DeleteCustomPresetAsync("YouTube 1080p · 60 FPS", _tempDir);
        Assert.False(builtInDeleted);
        Assert.Contains(manager.GetAllPresets(), p => p.Name == "YouTube 1080p · 60 FPS");

        // Save a custom preset then delete it
        var custom = new TimelineExportPreset("Temporal", 1280, 720, 30, 22, IsBuiltIn: false);
        await manager.SavePresetAsync(custom, _tempDir);
        Assert.Single(manager.GetCustomPresets());

        bool customDeleted = await manager.DeleteCustomPresetAsync("Temporal", _tempDir);
        Assert.True(customDeleted);
        Assert.Empty(manager.GetCustomPresets());
    }

    [Fact]
    public async Task SavePresetAsync_ThrowsWhenInvalid()
    {
        var manager = new RenderPresetManager();
        await manager.InitializeAsync(_tempDir);

        var invalid = new TimelineExportPreset("", 1921, 1080, 200, 99);
        await Assert.ThrowsAsync<InvalidDataException>(() => manager.SavePresetAsync(invalid, _tempDir));
    }
}
