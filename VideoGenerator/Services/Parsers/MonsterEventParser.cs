using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services.Parsers
{
    public class MonsterEventParser : IEventParser
    {
        private static readonly HashSet<string> StructureNames = new(
            DefaultStructures.Entries.Select(entry => DefaultStructures.Normalize(entry.Keyword)),
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> BundledChampionNames = new(
            DefaultGroups.Get().SelectMany(group => group.GetChampionsList()).Select(NormalizeMonsterName),
            StringComparer.OrdinalIgnoreCase);

        private readonly TranslationService _translationService;
        private readonly DataFetcher _dataFetcher;
        private readonly AliasManager _aliasManager;
        private readonly object _monsterCatalogLock = new();
        private IReadOnlyDictionary<string, string> _monsterCatalog;
        private string _monsterCatalogRevision;

        public MonsterEventParser(TranslationService translationService, DataFetcher dataFetcher, AliasManager aliasManager)
        {
            _translationService = translationService;
            _dataFetcher = dataFetcher;
            _aliasManager = aliasManager;
        }

        public bool CanParse(string folderName)
        {
            var match = GetAttackMatch(folderName);
            if (!match.Success) return false;

            string target = match.Groups[1].Value.Trim('_');
            return IsGeneralTarget(target) || TryResolveMonsterName(target, out _);
        }

        private string StripOwnerPrefix(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;
            var prefixMatch = Regex.Match(folderName, @"^_?(?:Play_vo_|Play_|vo_)([A-Za-z0-9]+?)(Skin\d+)?_", RegexOptions.IgnoreCase);
            if (prefixMatch.Success)
            {
                return folderName.Substring(prefixMatch.Length);
            }
            return folderName;
        }

        public Task<ParsedEvent> ParseAsync(string folderName, string language)
        {
            var match = GetAttackMatch(folderName);
            if (!match.Success) return Task.FromResult<ParsedEvent>(null);

            string monsterNameRaw = match.Groups[1].Value.Trim('_');

            if (IsGeneralTarget(monsterNameRaw))
            {
                string actionText = _translationService.GetText(language, "event_attack");
                string suffixText = _translationService.GetText(language, "suffix_in_general");
                string generalDisplayText = $"{actionText}{suffixText}";

                return Task.FromResult(new ParsedEvent
                {
                    OriginalFolder = folderName,
                    DisplayText = generalDisplayText,
                    IconLookupName = "Generic",
                    IconType = "generic"
                });
            }
            
            if (!TryResolveMonsterName(monsterNameRaw, out string monsterName))
                return Task.FromResult<ParsedEvent>(null);

            // Add underscores before capitals (e.g., BlueSentinel -> Blue_Sentinel)
            string monsterNameFormatted = Regex.Replace(monsterName.Replace(' ', '_'), @"(?<=[a-z0-9])(?=[A-Z])", "_");
            // Normalize multiple underscores (e.g. Ancient__Krug to Ancient_Krug)
            monsterNameFormatted = Regex.Replace(monsterNameFormatted, @"_+", "_").Trim('_');
            string displayMonsterName = monsterNameFormatted.Replace("_", " ");

            string displayText = _translationService.GetText(language, "interaction_attack_monster", 
                new Dictionary<string, string> { { "monster", displayMonsterName } });

            return Task.FromResult(new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                // JungleBuff means an unspecified jungle buff, not necessarily
                // Blue Sentinel or Red Brambleback. Never fabricate one of
                // their portraits for an ambiguous source event.
                IconLookupName = IsUnspecifiedJungleBuff(monsterNameRaw) ||
                    IsUnspecifiedEpicMonster(monsterNameRaw)
                    ? null : monsterNameFormatted,
                IconType = "monster"
            });
        }

        private Match GetAttackMatch(string folderName)
        {
            string cleanTarget = StripOwnerPrefix(folderName);
            return Regex.Match(cleanTarget, @"^Attack(?:2D|3D)_?([A-Za-z0-9_]+)$", RegexOptions.IgnoreCase);
        }

        private static bool IsGeneralTarget(string target) =>
            target.Equals("General", StringComparison.OrdinalIgnoreCase);

        private bool TryResolveMonsterName(string rawTarget, out string monsterName)
        {
            monsterName = null;
            if (Regex.IsMatch(rawTarget, @"Skin\d+", RegexOptions.IgnoreCase)) return false;

            // Square is an optional artwork suffix, never part of a monster's identity.
            string target = Regex.Replace(rawTarget, @"_?Square$", "", RegexOptions.IgnoreCase).Trim('_');
            string normalized = NormalizeMonsterName(target);
            if (IsUnspecifiedJungleBuff(normalized))
            {
                monsterName = "Jungle Buff";
                return true;
            }
            if (IsUnspecifiedEpicMonster(normalized))
            {
                // A generic epic objective is not Baron, Dragon or Herald
                // specifically. Preserve its monster category without borrowing
                // the portrait of any one real creature.
                monsterName = "Epic Monster";
                return true;
            }
            if (normalized.Length == 0 || StructureNames.Contains(normalized) ||
                BundledChampionNames.Contains(normalized) || _aliasManager.IsValidChampion(target))
                return false;

            // Custom monsters in the synchronized or user-maintained database are valid too.
            if (CurrentMonsterCatalog().TryGetValue(normalized, out string fromDatabase))
            {
                monsterName = fromDatabase;
                return true;
            }

            // DefaultMonsters is the application's versioned vocabulary, independent
            // of the personal catalog and whether the installed WAD has artwork.
            if (DefaultMonsters.TryResolveName(normalized, out string knownName))
            {
                monsterName = knownName;
                return true;
            }

            // Preserve any verified WAD-only aliases until they are rolled into
            // a future default-catalog update.
            if (LeagueMonsterAssets.TryResolveName(normalized, out string fallbackName))
            {
                monsterName = fallbackName;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Shared target authority for ALL actions, not just Attack. Dynamic
        /// Kill/Encounter rules must never use a separate incomplete monster
        /// vocabulary and accidentally classify BlueSentinel as a champion.
        /// </summary>
        public bool TryResolveTarget(string rawTarget, out string monsterName) =>
            TryResolveMonsterName(rawTarget, out monsterName);

        // Riot VO occasionally names only the type of jungle objective. This
        // is intentionally not a distinct creature in DefaultMonsters, and
        // there is no verified unique WAD image for that collective category.
        public static bool IsUnspecifiedJungleBuff(string target) =>
            NormalizeMonsterName(target).Equals("JungleBuff", StringComparison.OrdinalIgnoreCase);

        public static bool IsUnspecifiedEpicMonster(string target) =>
            NormalizeMonsterName(target).Equals("EpicMonster", StringComparison.OrdinalIgnoreCase);

        private IReadOnlyDictionary<string, string> CurrentMonsterCatalog()
        {
            string revision = _dataFetcher.MonsterCatalogRevision;
            lock (_monsterCatalogLock)
            {
                // If the user config is temporarily unavailable during an
                // atomic edit or folder scan, preserve last valid membership.
                if (_monsterCatalog != null &&
                    (revision is "<missing>" or "<unavailable>"))
                    return _monsterCatalog;
                if (_monsterCatalog == null || !string.Equals(_monsterCatalogRevision,
                    revision, StringComparison.Ordinal))
                {
                    _monsterCatalog = LoadMonsterCatalog();
                    _monsterCatalogRevision = revision;
                }
                return _monsterCatalog;
            }
        }

        private IReadOnlyDictionary<string, string> LoadMonsterCatalog()
        {
            var database = _dataFetcher.LoadMonsterDatabase();
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in (database.Epic ?? new List<string>()).Concat(database.Large ?? new List<string>()))
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                string normalized = NormalizeMonsterName(name);
                if (normalized.Length > 0 && !result.ContainsKey(normalized))
                    result[normalized] = name;
            }
            return result;
        }

        private static string NormalizeMonsterName(string name) =>
            Regex.Replace(name, @"[^A-Za-z0-9]", "");
    }
}
