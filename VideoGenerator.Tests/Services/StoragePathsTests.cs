using VideoGenerator.Models;
using VideoGenerator.Utils;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class StoragePathsTests
{
    [Fact]
    public void BuildsTheCompleteStorageLayoutFromOneRoot()
    {
        var paths = StoragePaths.Create(Path.Combine(Path.GetTempPath(), "VideoGenerator.StoragePaths.Tests"));

        Assert.EndsWith(Path.Combine("Config", "settings.json"), paths.SettingsPath);
        Assert.EndsWith(Path.Combine("Config", "event_rules.json"), paths.EventRulesPath);
        Assert.EndsWith(Path.Combine("Config", "champion_aliases.json"), paths.AliasesPath);
        Assert.EndsWith(Path.Combine("Cache", "IconCache"), paths.IconCacheDirectory);
        Assert.EndsWith(Path.Combine("Cache", "AudioFamilies"), paths.AudioFamiliesDirectory);
        Assert.EndsWith("skins_data_default.json", paths.GetSkinsCachePath("default"));
        Assert.EndsWith("ggml-base.bin", paths.GetWhisperModelPath("ggml-base.bin"));
    }

    [Fact]
    public void AppConfigUsesTheDefaultStorageLayout()
    {
        var expected = StoragePaths.Create();

        Assert.Equal(expected.ConfigDirectory, AppConfig.Paths.ConfigDirectory);
        Assert.Equal(expected.CacheDirectory, AppConfig.Paths.CacheDirectory);
        Assert.Equal(expected.OutputVideosDirectory, AppConfig.Paths.OutputVideosDirectory);
        Assert.Equal(expected.LogsDirectory, AppConfig.Paths.LogsDirectory);
        Assert.Equal(expected.SettingsPath, AppConfig.Paths.SettingsPath);
        Assert.Equal(expected.RuntimeFfmpegDirectory, AppConfig.Paths.RuntimeFfmpegDirectory);
    }
}
