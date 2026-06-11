using System;
using System.Collections.Generic;
using System.IO;

namespace VideoGenerator.Models
{
    public static class AppConfig
    {
        public static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string ResourcesDir = Path.Combine(BaseDir, "Resources");
        public static readonly string CacheDir = Path.Combine(BaseDir, "Cache");
        public static readonly string IconCacheDir = Path.Combine(CacheDir, "IconCache");
        public static readonly string ItemCacheDir = Path.Combine(CacheDir, "ItemCache");
        public static readonly string MonsterCacheDir = Path.Combine(CacheDir, "MonsterCache");
        public static readonly string OutputDir = Path.Combine(BaseDir, "Output");
        public static readonly string OutputImagesDir = Path.Combine(OutputDir, "Images");
        public static readonly string OutputVideosDir = Path.Combine(OutputDir, "Videos");

        public static readonly string BackgroundPath = Path.Combine(ResourcesDir, "DefaultBackground.jpg");
        public static readonly string ConfigDir = Path.Combine(BaseDir, "Config");
        public static readonly string TranslationsPath = Path.Combine(ConfigDir, "translations.json");

        // URLs
        public static readonly string MonsterWikiUrl = "https://wiki.leagueoflegends.com/en-us/Monster";
        public static readonly string SkinsDataUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/skins.json";
        public static readonly string SkinLinesUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/skinlines.json";
        public static readonly string VersionsUrl = "https://ddragon.leagueoflegends.com/api/versions.json";
    }
}
