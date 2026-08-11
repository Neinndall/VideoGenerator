using System;
using VideoGenerator.Services;
using VideoGenerator.Utils;

namespace VideoGenerator.Models
{
    public static class AppConfig
    {
        public static readonly StoragePaths Paths = StoragePaths.Create();

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
