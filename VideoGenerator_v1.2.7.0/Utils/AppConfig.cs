using System;
using System.IO;
using VideoGenerator.Services;

namespace VideoGenerator.Models
{
    public static class AppConfig
    {
        // Application directories
        public static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string ResourcesDir = Path.Combine(BaseDir, "Resources");
        public static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VideoGenerator");
        public static readonly string ConfigDir = Path.Combine(AppDataDir, "Config");
        public static readonly string CacheDir = Path.Combine(AppDataDir, "Cache");
        public static readonly string IconCacheDir = Path.Combine(CacheDir, "IconCache");
        public static readonly string ItemCacheDir = Path.Combine(CacheDir, "ItemCache");
        public static readonly string MonsterCacheDir = Path.Combine(CacheDir, "MonsterCache");
        public static readonly string OutputDir = Path.Combine(BaseDir, "Generated");
        public static readonly string OutputImagesDir = Path.Combine(OutputDir, "Images");
        public static readonly string OutputVideosDir = Path.Combine(OutputDir, "Media");
        public static readonly string LogsDir = Path.Combine(BaseDir, "logs");
        public static readonly string RuntimeFfmpegDir = Path.Combine(Path.GetTempPath(), "VideoGenerator_FFmpeg");
        public static readonly string AudioFamiliesDir = Path.Combine(CacheDir, "AudioFamilies");

        // Persistent files
        public static readonly string BackgroundPath = Path.Combine(ResourcesDir, "DefaultBackground.png");
        public static readonly string TranslationsPath = Path.Combine(ConfigDir, "translations.json");
        public static readonly string DialoguesPath = Path.Combine(ConfigDir, "dialogues.json");
        public static readonly string GroupsPath = Path.Combine(ConfigDir, "groups.json");
        public static readonly string MonstersPath = Path.Combine(ConfigDir, "monsters.json");
        public static readonly string StructuresPath = Path.Combine(ConfigDir, "structures.json");
        public static readonly string SkinlineCachePath = Path.Combine(ConfigDir, "skinlines.json");
        public static readonly string ChampionsPath = Path.Combine(ConfigDir, "champions.json");
        public static readonly string ItemsPath = Path.Combine(ConfigDir, "items.json");
        public static readonly string SettingsPath = Path.Combine(ConfigDir, "settings.json");
        public static readonly string EventRulesPath = Path.Combine(ConfigDir, "event_rules.json");
        public static readonly string AliasesPath = Path.Combine(ConfigDir, "champion_aliases.json");
        public static readonly string LocalVersionPath = Path.Combine(ConfigDir, "version.json");
        public static readonly string ApplicationLogPath = Path.Combine(LogsDir, "application_logs.log");
        public static readonly string ApplicationErrorsPath = Path.Combine(LogsDir, "application_errors.log");
        public static readonly string PreviewIconPlaceholderPath = Path.Combine(CacheDir, "preview_icon_placeholder.png");

        // Isolated storage helpers for deterministic tests
        public static string GetConfigDirectory(string storageRoot = null) =>
            string.IsNullOrWhiteSpace(storageRoot) ? ConfigDir : Path.Combine(storageRoot, "Config");

        public static string GetCacheDirectory(string storageRoot = null) =>
            string.IsNullOrWhiteSpace(storageRoot) ? CacheDir : Path.Combine(storageRoot, "Cache");

        public static string GetIconCacheDirectory(string storageRoot = null) =>
            Path.Combine(GetCacheDirectory(storageRoot), "IconCache");

        public static string GetChampionsFilePath(string storageRoot = null) =>
            Path.Combine(GetConfigDirectory(storageRoot), "champions.json");

        public static string GetItemsFilePath(string storageRoot = null) =>
            Path.Combine(GetConfigDirectory(storageRoot), "items.json");

        public static string GetMonstersFilePath(string storageRoot = null) =>
            Path.Combine(GetConfigDirectory(storageRoot), "monsters.json");

        public static string GetStructuresFilePath(string storageRoot = null) =>
            Path.Combine(GetConfigDirectory(storageRoot), "structures.json");

        public static string GetLocalVersionFilePath(string storageRoot = null) =>
            Path.Combine(GetConfigDirectory(storageRoot), "version.json");

        public static string GetSkinsCachePath(string locale, string storageRoot = null) =>
            Path.Combine(GetCacheDirectory(storageRoot), $"skins_data_{locale}.json");

        public static string GetSkinLinesCachePath(string locale, string storageRoot = null) =>
            Path.Combine(GetCacheDirectory(storageRoot), $"skinlines_data_{locale}.json");

        public static string GetItemsCachePath(string locale, string storageRoot = null) =>
            Path.Combine(GetCacheDirectory(storageRoot), $"items_data_{locale}.json");

        public static string GetWhisperModelPath(string modelFileName, string storageRoot = null) =>
            Path.Combine(GetCacheDirectory(storageRoot), modelFileName);

        public static string CreateTemporaryWavPath() =>
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");

        // URLs
        public static readonly string MonsterWikiUrl = "https://leagueoflegends.fandom.com/wiki/Monster";
        public static string GetSkinsDataUrl(string locale) => $"https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/{locale}/v1/skins.json";
        public static string GetSkinLinesUrl(string locale) => $"https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/{locale}/v1/skinlines.json";
        public static string GetItemsDataUrl(string locale) => $"https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/{locale}/v1/items.json";
        public static readonly string VersionsUrl = "https://ddragon.leagueoflegends.com/api/versions.json";

        public static string GetCdragonLocale(string language = null)
        {
            string targetLang = language ?? AppSettings.Instance.DefaultDictionaryLanguage;
            if (string.IsNullOrEmpty(targetLang) || targetLang.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                targetLang = "EN";
            }
            return targetLang.ToUpperInvariant() switch
            {
                "TR" => "tr_tr",
                "ES" => "es_es",
                _ => "default"
            };
        }
    }
}
