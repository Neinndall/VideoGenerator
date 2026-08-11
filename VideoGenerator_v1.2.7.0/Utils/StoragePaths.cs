using System;
using System.IO;

namespace VideoGenerator.Utils
{
    /// <summary>
    /// Resolves the application's persistent and runtime storage paths.
    /// A custom application-data root can be supplied by deterministic tests.
    /// </summary>
    public sealed class StoragePaths
    {
        public string BaseDirectory { get; }
        public string AppDataDirectory { get; }
        public string ResourcesDirectory => Path.Combine(BaseDirectory, "Resources");
        public string ConfigDirectory => Path.Combine(AppDataDirectory, "Config");
        public string CacheDirectory => Path.Combine(AppDataDirectory, "Cache");
        public string IconCacheDirectory => Path.Combine(CacheDirectory, "IconCache");
        public string ItemCacheDirectory => Path.Combine(CacheDirectory, "ItemCache");
        public string MonsterCacheDirectory => Path.Combine(CacheDirectory, "MonsterCache");
        public string OutputDirectory => Path.Combine(BaseDirectory, "Generated");
        public string OutputImagesDirectory => Path.Combine(OutputDirectory, "Images");
        public string OutputVideosDirectory => Path.Combine(OutputDirectory, "Media");
        public string LogsDirectory => Path.Combine(BaseDirectory, "logs");
        public string ApplicationLogPath => Path.Combine(LogsDirectory, "application_logs.log");
        public string ApplicationErrorsPath => Path.Combine(LogsDirectory, "application_errors.log");
        public string RuntimeFfmpegDirectory => Path.Combine(Path.GetTempPath(), "VideoGenerator_FFmpeg");
        public string AudioFamiliesDirectory => Path.Combine(CacheDirectory, "AudioFamilies");

        public string BackgroundPath => Path.Combine(ResourcesDirectory, "DefaultBackground.png");
        public string TranslationsPath => Path.Combine(ConfigDirectory, "translations.json");
        public string DialoguesPath => Path.Combine(ConfigDirectory, "dialogues.json");
        public string GroupsPath => Path.Combine(ConfigDirectory, "groups.json");
        public string MonstersPath => Path.Combine(ConfigDirectory, "monsters.json");
        public string StructuresPath => Path.Combine(ConfigDirectory, "structures.json");
        public string SkinlineCachePath => Path.Combine(ConfigDirectory, "skinlines.json");
        public string ChampionsPath => Path.Combine(ConfigDirectory, "champions.json");
        public string ItemsPath => Path.Combine(ConfigDirectory, "items.json");
        public string SettingsPath => Path.Combine(ConfigDirectory, "settings.json");
        public string EventRulesPath => Path.Combine(ConfigDirectory, "event_rules.json");
        public string AliasesPath => Path.Combine(ConfigDirectory, "champion_aliases.json");
        public string LocalVersionPath => Path.Combine(ConfigDirectory, "version.json");
        public string PreviewIconPlaceholderPath => Path.Combine(CacheDirectory, "preview_icon_placeholder.png");

        public StoragePaths(string appDataDirectory, string baseDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(appDataDirectory))
                throw new ArgumentException("An application-data directory is required.", nameof(appDataDirectory));

            AppDataDirectory = appDataDirectory;
            BaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : baseDirectory;
        }

        public static StoragePaths Create(string storageRoot = null)
        {
            string appDataDirectory = string.IsNullOrWhiteSpace(storageRoot)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VideoGenerator")
                : storageRoot;

            return new StoragePaths(appDataDirectory);
        }

        public string GetSkinsCachePath(string locale) =>
            Path.Combine(CacheDirectory, $"skins_data_{locale}.json");

        public string GetSkinLinesCachePath(string locale) =>
            Path.Combine(CacheDirectory, $"skinlines_data_{locale}.json");

        public string GetItemsCachePath(string locale) =>
            Path.Combine(CacheDirectory, $"items_data_{locale}.json");

        public string GetWhisperModelPath(string modelFileName) =>
            Path.Combine(CacheDirectory, modelFileName);

        public string CreateTemporaryWavPath() =>
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
    }
}
