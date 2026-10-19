using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Human-friendly syntax for the local Game Splash picker. Skin IDs are
    /// Riot numeric IDs from skins.json, not virtual file names or WAD hashes.
    /// No implicit network lookup is performed.
    /// </summary>
    public sealed record LeagueSplashQuery(string Champion, IReadOnlyList<int> SkinIds)
    {
        public bool HasSkinIds => SkinIds.Count > 0;
    }

    public static class LeagueSplashQueryParser
    {
        private static readonly Regex SkinSuffix = new(
            @"^(?<champion>.+?)(?:Skin|[_\s-]+)?(?<ids>\d{1,4}(?:\s*,\s*\d{1,4})*)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ChampionName = new(
            @"^[\p{L}][\p{L}\p{Nd}\s.'’\-]*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static bool TryParse(string input, out LeagueSplashQuery result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string value = input.Trim();
            Match match = SkinSuffix.Match(value);
            string champion = match.Success ? match.Groups["champion"].Value.Trim() : value;
            if (!ChampionName.IsMatch(champion)) return false;

            var ids = new List<int>();
            if (match.Success)
            {
                string[] tokens = match.Groups["ids"].Value.Split(',');
                if (tokens.Length > 24) return false;
                foreach (string token in tokens)
                {
                    if (!int.TryParse(token.Trim(), out int id) || id < 0) return false;
                    if (!ids.Contains(id)) ids.Add(id);
                }
            }
            result = new LeagueSplashQuery(champion, ids.ToArray());
            return true;
        }
    }
}
