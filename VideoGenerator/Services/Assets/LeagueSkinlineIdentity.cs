using System;
using System.Linq;

namespace VideoGenerator.Services
{
    /// <summary>
    /// One identity rule for the event parser, the saved Riot skinline catalog,
    /// and direct WAD icon resolution. Hashes identify assets, not skinlines.
    /// A Riot line such as "Arcade: Heroes" also belongs to the "Arcade"
    /// family, but its members never get mixed with unrelated themes.
    /// </summary>
    public static class LeagueSkinlineIdentity
    {
        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            name = name.Trim();
            // Do not strip "the" from unrelated names such as "Theater".
            // Riot's display-name prefix is a separate word, while folder
            // tokens may also use PascalCase ("TheEternalAspects").
            if (name.Length > 3 && name.StartsWith("The", StringComparison.OrdinalIgnoreCase) &&
                (char.IsWhiteSpace(name[3]) || name[3] is '_' or '-' || char.IsUpper(name[3])))
                name = name[3..];
            string key = new string(name.Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray());
            // A currently shipped VO filename omits the 't' in HEARTSTEEL.
            // Normalize the event spelling to Riot's official skinline ID.
            return key == "hearsteel" ? "heartsteel" : key;
        }

        /// <summary>
        /// League VO may identify a specific champion in a skinline, e.g.
        /// EkkoSkinlineTrueDamage. Preserve both identities rather than
        /// constructing the nonexistent champion name EkkoTrueDamage.
        /// </summary>
        public static bool TrySplitChampionQualified(
            string target, out string champion, out string theme)
        {
            champion = null;
            theme = null;
            if (string.IsNullOrWhiteSpace(target)) return false;
            int marker = target.IndexOf("Skinline", StringComparison.OrdinalIgnoreCase);
            if (marker <= 0 || marker + "Skinline".Length >= target.Length) return false;
            champion = target[..marker].Trim('_', ' ');
            theme = target[(marker + "Skinline".Length)..].Trim('_', ' ');
            return champion.Length > 0 && theme.Length > 0;
        }

        public static bool Matches(string requested, string official)
        {
            string query = Normalize(requested);
            if (query.Length == 0) return false;
            if (query == Normalize(official)) return true;
            // Riot's recent VO uses TrueDamageX although its local skinline
            // metadata still names this family True Damage. Apply this only
            // to an existing official name, never strip X from arbitrary
            // champion/asset names.
            if (query.Length > 1 && query.EndsWith("x", StringComparison.Ordinal) &&
                query[..^1] == Normalize(official))
                return true;

            // An unqualified family name is a valid umbrella when Riot uses
            // colon-separated sub-lines. The caller can combine their members.
            int colon = official?.IndexOf(':') ?? -1;
            return colon > 0 && query == Normalize(official[..colon]);
        }

        public static string GetFamilyName(string official)
        {
            int colon = official?.IndexOf(':') ?? -1;
            return colon > 0 ? official[..colon].Trim() : null;
        }

        /// <summary>
        /// Riot can ship a named collection of skins before assigning it a
        /// separate skinline ID (PBE K/DA FIRESTARTER is one such example).
        /// A VO token may identify a *leading* portion of a Riot skin name,
        /// but only inside an existing official skinline family. Require a
        /// real skin owner after the requested prefix, so a fake champion or
        /// arbitrary text fragment can never be mistaken for a theme.
        /// </summary>
        public static bool TryMatchNamedCollection(string requested, string skinName,
            string officialFamily, string champion, out string collection)
        {
            collection = null;
            string family = Normalize(officialFamily);
            string wanted = Normalize(requested);
            string owner = Normalize(champion);
            if (family.Length == 0 || owner.Length == 0 ||
                wanted.Length <= family.Length ||
                !wanted.StartsWith(family, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(skinName) ||
                !skinName.StartsWith(officialFamily + " ", StringComparison.OrdinalIgnoreCase) ||
                !Normalize(skinName).EndsWith(owner, StringComparison.Ordinal))
                return false;

            string[] words = skinName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Leave one or more words for the champion / skin variant.
            for (int count = 1; count < words.Length; count++)
            {
                string candidate = string.Join(" ", words.Take(count));
                if (Normalize(candidate) != wanted) continue;
                collection = candidate;
                return true;
            }
            return false;
        }
    }
}
