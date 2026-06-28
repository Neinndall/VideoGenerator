using System;
using System.Collections.Generic;
using System.IO;
using VideoGenerator.Services;

namespace VideoGenerator.Models
{
    public static class AppConfig
    {
        public static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string ResourcesDir = Path.Combine(BaseDir, "Resources");
        public static readonly string AppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoGenerator");
        public static readonly string CacheDir = Path.Combine(AppDataDir, "Cache");
        public static readonly string IconCacheDir = Path.Combine(CacheDir, "IconCache");
        public static readonly string ItemCacheDir = Path.Combine(CacheDir, "ItemCache");
        public static readonly string MonsterCacheDir = Path.Combine(CacheDir, "MonsterCache");
        public static readonly string OutputDir = Path.Combine(BaseDir, "Generated");
        public static readonly string OutputImagesDir = Path.Combine(OutputDir, "Images");
        public static readonly string OutputVideosDir = Path.Combine(OutputDir, "Media");

        public static readonly string BackgroundPath = Path.Combine(ResourcesDir, "DefaultBackground.png");
        public static readonly string ConfigDir = Path.Combine(AppDataDir, "Config");
        // Config Files
        public static readonly string TranslationsPath = Path.Combine(ConfigDir, "translations.json");
        public static readonly string DialoguesPath = Path.Combine(ConfigDir, "dialogues.json");
        public static readonly string GroupsPath = Path.Combine(ConfigDir, "groups.json");
        public static readonly string MonstersPath = Path.Combine(ConfigDir, "monsters.json");
        public static readonly string StructuresPath = Path.Combine(ConfigDir, "structures.json");
        public static readonly string SkinlineCachePath = Path.Combine(ConfigDir, "skinlines.json");
        public static readonly string ChampionsPath = Path.Combine(ConfigDir, "champions.json");
        public static readonly string ItemsPath = Path.Combine(ConfigDir, "items.json");
        public static readonly string LocalVersionPath = Path.Combine(ConfigDir, "version.json");

        // Cache Paths (Language-specific Community Dragon files)
        public static string SkinsCachePath => Path.Combine(CacheDir, $"skins_data_{GetCdragonLocale()}.json");
        public static string SkinLinesCachePath => Path.Combine(CacheDir, $"skinlines_data_{GetCdragonLocale()}.json");
        public static string ItemsCachePath => Path.Combine(CacheDir, $"items_data_{GetCdragonLocale()}.json");

        // URLs
        public static readonly string MonsterWikiUrl = "https://leagueoflegends.fandom.com/wiki/Monster";
        public static string SkinsDataUrl => $"https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/{GetCdragonLocale()}/v1/skins.json";
        public static string SkinLinesUrl => $"https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/{GetCdragonLocale()}/v1/skinlines.json";
        public static string ItemsDataUrl => $"https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/{GetCdragonLocale()}/v1/items.json";
        public static readonly string VersionsUrl = "https://ddragon.leagueoflegends.com/api/versions.json";

        public static string GetCdragonLocale()
        {
            string defaultLang = AppSettings.Instance.DefaultDictionaryLanguage?.ToUpperInvariant() ?? "EN";
            return defaultLang switch
            {
                "TR" => "tr_tr",
                "ES" => "es_es",
                _ => "default"
            };
        }
    }
}
