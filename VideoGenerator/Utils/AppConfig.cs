using System;
using System.Collections.Generic;
using System.IO;

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
        // URLs
        public static readonly string MonsterWikiUrl = "https://leagueoflegends.fandom.com/wiki/Monster";
        public static readonly string SkinsDataUrl = "https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/default/v1/skins.json";
        public static readonly string SkinLinesUrl = "https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/default/v1/skinlines.json";
        public static readonly string ItemsDataUrl = "https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/default/v1/items.json";
        public static readonly string VersionsUrl = "https://ddragon.leagueoflegends.com/api/versions.json";
    }
}
