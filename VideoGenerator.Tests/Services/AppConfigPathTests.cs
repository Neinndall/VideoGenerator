using VideoGenerator.Models;
using Xunit;

namespace VideoGenerator.Tests;

public sealed class AppConfigPathTests
{
    [Fact]
    public void ExposesTheCompleteDefaultStorageLayout()
    {
        Assert.EndsWith(Path.Combine("Config", "settings.json"), AppConfig.SettingsPath);
        Assert.EndsWith(Path.Combine("Config", "event_rules.json"), AppConfig.EventRulesPath);
        Assert.EndsWith(Path.Combine("Config", "champion_aliases.json"), AppConfig.AliasesPath);
        Assert.EndsWith(Path.Combine("Cache", "IconCache"), AppConfig.IconCacheDir);
        Assert.EndsWith(Path.Combine("Cache", "AudioFamilies"), AppConfig.AudioFamiliesDir);
        Assert.EndsWith("skins_data_default.json", AppConfig.GetSkinsCachePath("default"));
        Assert.EndsWith("ggml-base.bin", AppConfig.GetWhisperModelPath("ggml-base.bin"));
    }

    [Fact]
    public void ResolvesTheSameLayoutUnderAnIsolatedRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "VideoGenerator.AppConfig.Tests");

        Assert.Equal(Path.Combine(root, "Config"), AppConfig.GetConfigDirectory(root));
        Assert.Equal(Path.Combine(root, "Cache"), AppConfig.GetCacheDirectory(root));
        Assert.Equal(
            Path.Combine(root, "Cache", "items_data_default.json"),
            AppConfig.GetItemsCachePath("default", root));
        Assert.Equal(
            Path.Combine(root, "Config", "monsters.json"),
            AppConfig.GetMonstersFilePath(root));
    }
}
