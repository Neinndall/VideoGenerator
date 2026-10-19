using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services.Parsers
{
    public class DynamicRuleParser : IEventParser
    {
        private readonly TranslationService _translationService;
        private readonly RuleManager _ruleManager;
        private readonly GroupManager _groupManager;
        private readonly AliasManager _aliasManager;
        private readonly SkinlineManager _skinlineManager;
        private readonly DataFetcher _dataFetcher;
        private readonly MonsterEventParser _monsterResolver;

        public DynamicRuleParser(
            TranslationService translationService,
            DataFetcher dataFetcher,
            RuleManager ruleManager,
            GroupManager groupManager,
            AliasManager aliasManager,
            SkinlineManager skinlineManager,
            MonsterEventParser monsterResolver = null)
        {
            _translationService = translationService;
            _dataFetcher = dataFetcher;
            _ruleManager = ruleManager;
            _groupManager = groupManager;
            _aliasManager = aliasManager;
            _skinlineManager = skinlineManager;
            _monsterResolver = monsterResolver ??
                new MonsterEventParser(translationService, dataFetcher, aliasManager);
        }

        public bool CanParse(string folderName)
        {
            return true;
        }

        public async Task<ParsedEvent> ParseAsync(string folderName, string language)
        {
            string cleanFolder = StripOwnerPrefix(folderName);
            string normalizedFolder = NormalizeFolderName(cleanFolder);

            foreach (var rule in _ruleManager.Rules.OrderByDescending(r => r.Keyword.Length).ThenBy(r => r.Type))
            {
                string normalizedKeyword = NormalizeFolderName(rule.Keyword);
                string prefix = "";

                if (rule.Type == RuleType.Simple)
                {
                    // Strip 2D/3D infixes for matching (e.g. Kill3DBaronSteal -> KillBaronSteal)
                    string cleanedFolder = Regex.Replace(normalizedFolder, @"(2D|3D)", "", RegexOptions.IgnoreCase);
                    bool isNumberedGeneralMatch = Regex.IsMatch(
                        cleanedFolder,
                        $@"^{Regex.Escape(normalizedKeyword)}(?:General|inGeneral)\d+$",
                        RegexOptions.IgnoreCase);

                    bool isExactMatch = cleanedFolder.Equals(normalizedKeyword, StringComparison.OrdinalIgnoreCase);
                    bool isGeneralMatch = cleanedFolder.Equals(normalizedKeyword + "General", StringComparison.OrdinalIgnoreCase) ||
                                          cleanedFolder.Equals(normalizedKeyword + "inGeneral", StringComparison.OrdinalIgnoreCase) ||
                                          normalizedFolder.Equals(normalizedKeyword + "3DGeneral", StringComparison.OrdinalIgnoreCase) ||
                                          normalizedFolder.Equals(normalizedKeyword + "2DGeneral", StringComparison.OrdinalIgnoreCase) ||
                                          isNumberedGeneralMatch;

                    bool isPrefixedGeneralMatch = !isExactMatch && !isGeneralMatch &&
                        (cleanedFolder.EndsWith("General", StringComparison.OrdinalIgnoreCase) ||
                         cleanedFolder.EndsWith("inGeneral", StringComparison.OrdinalIgnoreCase)) &&
                        Regex.IsMatch(cleanedFolder, $@"(^|_){Regex.Escape(normalizedKeyword)}(?=_|$|General|inGeneral)", RegexOptions.IgnoreCase);

                    if (isPrefixedGeneralMatch)
                    {
                        int kwIndex = cleanedFolder.IndexOf(normalizedKeyword, StringComparison.OrdinalIgnoreCase);
                        if (kwIndex > 0 && cleanedFolder[kwIndex - 1] == '_')
                        {
                            prefix = cleanedFolder.Substring(0, kwIndex - 1);
                        }
                    }

                    if (!isExactMatch && !isGeneralMatch && !isPrefixedGeneralMatch) continue;
                }
                else
                {
                    if (!normalizedFolder.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)) continue;
                }

                var parsed = await ProcessRuleEventAsync(folderName, rule, normalizedFolder, normalizedKeyword, language, prefix);
                if (parsed != null) return parsed;
            }

            return null;
        }

        private async Task<ParsedEvent> ProcessRuleEventAsync(string folderName, EventRule rule, string normalizedFolder, string normalizedKeyword, string language, string prefix = "")
        {
            string rawWithoutPrefix = StripOwnerPrefix(folderName);
            // Keep the un-normalized target to preserve the semantic
            // ChampionSkinlineTheme boundary; NormalizeFolderName removes
            // "Skinline" for legacy generic matching.
            int rawKeywordIndex = rawWithoutPrefix.IndexOf(rule.Keyword, StringComparison.OrdinalIgnoreCase);
            string rawTarget = rawKeywordIndex >= 0
                ? rawWithoutPrefix[(rawKeywordIndex + rule.Keyword.Length)..]
                : "";
            rawTarget = Regex.Replace(rawTarget, @"^(2D|3D|_)+", "", RegexOptions.IgnoreCase).Trim('_');
            
            // 1. Extraction logic for targets
            int index = normalizedFolder.IndexOf(normalizedKeyword, StringComparison.OrdinalIgnoreCase);
            string targetName = "";
            if (index >= 0)
            {
                targetName = normalizedFolder.Substring(index + normalizedKeyword.Length);
            }

            // Fallback for shifted indices
            if (string.IsNullOrEmpty(targetName) && rawWithoutPrefix.Contains(rule.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                int rawIndex = rawWithoutPrefix.IndexOf(rule.Keyword, StringComparison.OrdinalIgnoreCase);
                targetName = rawWithoutPrefix.Substring(rawIndex + rule.Keyword.Length);
            }

            // Clean target name
            targetName = Regex.Replace(targetName, @"^(2D|3D|_)+", "", RegexOptions.IgnoreCase).Trim('_');
            // Remove the explicit "Skinline" token so AnimaSquad/Primordian resolve as real skinlines
            targetName = Regex.Replace(targetName, @"^(SkinLine|Skinline|skinline)+_?", "", RegexOptions.IgnoreCase).Trim('_');
            // Strip a trailing single uppercase letter suffix that Riot adds to item event folder names
            // (e.g. UseItem3DGuardianAngelR -> GuardianAngel)
            if (rule.IconType.Equals("item", StringComparison.OrdinalIgnoreCase) &&
                targetName.Length > 1 &&
                char.IsUpper(targetName[^1]) &&
                targetName[..^1].Any(char.IsLower))
            {
                targetName = targetName[..^1];
            }

            if (rule.Keyword.Equals("Unique", StringComparison.OrdinalIgnoreCase))
            {
                targetName = Regex.Replace(targetName, "Emote$", "", RegexOptions.IgnoreCase).Trim('_');
            }

            // --- IMPORTANT: Priority to Rule's IconLookup ---
            // If the rule already defines a specific icon (like 'Gold' or 'Assist Me'), we use it.
            string iconTarget;
            if (!string.IsNullOrEmpty(rule.IconLookup))
            {
                iconTarget = rule.IconLookup;
            }
            else
            {
                iconTarget = string.IsNullOrEmpty(targetName) ? "General" : targetName;
            }

            if (Regex.IsMatch(iconTarget, @"^(?:in)?General\d*$", RegexOptions.IgnoreCase))
            {
                iconTarget = "General";
            }

            string iconType = rule.IconType;
            string displayText;

            // --- CASE A: GENERAL EVENT ---
            if (iconTarget.Equals("General", StringComparison.OrdinalIgnoreCase))
            {
                if (iconType != "system" && iconType != "item" && iconType != "structure") iconType = "generic";
                
                iconTarget = string.IsNullOrEmpty(rule.IconLookup) ? "Generic" : rule.IconLookup;

                bool isTrulyGeneral = folderName.Contains("General", StringComparison.OrdinalIgnoreCase) ||
                                      folderName.Contains("inGeneral", StringComparison.OrdinalIgnoreCase);

                if (rule.Keyword.Equals("FirstEncounter", StringComparison.OrdinalIgnoreCase))
                    displayText = _translationService.GetText(language, "event_first_encounter_general");
                else if (rule.Keyword.Equals("Assist", StringComparison.OrdinalIgnoreCase))
                    displayText = _translationService.GetText(language, "event_assist_general");
                else if (rule.Keyword.Equals("Kill", StringComparison.OrdinalIgnoreCase))
                    displayText = _translationService.GetText(language, "event_kill_general");
                else if (rule.Keyword.Equals("Death", StringComparison.OrdinalIgnoreCase))
                    displayText = _translationService.GetText(language, "event_death");
                else if (rule.Keyword.Equals("MoveFirst", StringComparison.OrdinalIgnoreCase))
                    displayText = _translationService.GetText(language, "event_move_first");
                else if (isTrulyGeneral)
                {
                    string baseText = _translationService.GetText(language, rule.TranslationKey);
                    string suffix = _translationService.GetText(language, "suffix_in_general");
                    displayText = $"{baseText}{suffix}";
                }
                else
                {
                    displayText = _translationService.GetText(language, rule.TranslationKey);
                }

                if (!string.IsNullOrEmpty(prefix)) displayText = $"{prefix}: {displayText}";
            }
            // --- CASE B: SPECIFIC TARGET ---
            else
            {
                // If the target is another champion with a specific skin (e.g. RivenSkin44),
                // let SkinInteractionParser handle the full interaction instead.
                if (Regex.IsMatch(iconTarget, @"^[A-Za-z]+Skin\d+$", RegexOptions.IgnoreCase))
                {
                    return null;
                }

                string displayTargetName = Regex.Replace(iconTarget, @"(?<!^)(?=[A-Z])", " ").Trim();
                string cleanDisplayName = Regex.Replace(displayTargetName, @"Skin\d+", "", RegexOptions.IgnoreCase).Trim();

                var matchedGroup = _groupManager.Groups.FirstOrDefault(g => g.Name.Equals(displayTargetName, StringComparison.OrdinalIgnoreCase));
                
                if (string.IsNullOrWhiteSpace(rule.IconLookup) &&
                    LeagueSkinlineIdentity.TrySplitChampionQualified(
                        rawTarget, out string themeChampion, out string themeToken) &&
                    _skinlineManager.IsKnownSkinline(themeToken))
                {
                    // Official line identity is sufficient for routing.
                    // champions.json (and therefore IsValidChampion) may be
                    // absent on fresh installs. The WAD resolver validates
                    // the requested champion against actual skinline members
                    // and does not substitute an unrelated champion's icon.
                    string officialTheme = _skinlineManager.GetDisplayName(themeToken);
                    string officialChampion = _aliasManager.GetInternalName(themeChampion);
                    iconType = "skinline";
                    iconTarget = $"{officialChampion}Skinline{officialTheme}";
                    displayText = _translationService.GetText(
                        language, rule.TranslationKey, $"{officialTheme} {officialChampion}");
                }
                else if (matchedGroup != null)
                {
                    if (matchedGroup.Category.Equals("Region", StringComparison.OrdinalIgnoreCase))
                    {
                        iconTarget = displayTargetName;
                        iconType = "region";
                    }
                    else
                    {
                        var candidates = matchedGroup.GetChampionsList();
                        iconTarget = ChooseRepresentative(candidates, matchedGroup.Name) ?? "General";
                        iconType = "champion";
                    }

                    string themeKey = $"{matchedGroup.Category.ToLower()}_{displayTargetName.ToLower().Replace(" ", "_")}";
                    string themeDisplayName = _translationService.GetText(language, themeKey);
                    string specificRuleKey = rule.TranslationKey.Replace("_one", $"_{matchedGroup.Category.ToLower()}");
                    string specificDisplayText = _translationService.GetText(language, specificRuleKey, themeDisplayName);

                    displayText = (specificDisplayText != specificRuleKey) ? specificDisplayText : _translationService.GetText(language, rule.TranslationKey, themeDisplayName);
                }
                else if (_skinlineManager.IsKnownSkinline(displayTargetName))
                {
                    // Preserve thematic identity all the way to the icon
                    // resolver. It knows which skin IDs actually belong to
                    // this line and can try another member if a tile is absent.
                    string officialName = _skinlineManager.GetDisplayName(displayTargetName);
                    iconTarget = officialName;
                    iconType = "skinline";

                    string themeKey = $"skinline_{officialName.ToLower().Replace(" ", "_")}";
                    string themeDisplayName = _translationService.GetText(language, themeKey);
                    if (themeDisplayName == themeKey)
                    {
                        themeDisplayName = await _skinlineManager.GetLocalizedDisplayNameAsync(officialName, language);
                    }
                    displayText = _translationService.GetText(language, rule.TranslationKey, themeDisplayName);
                }
                else
                {
                    // If the rule already defines a specific icon type with a lookup, trust it
                    bool ruleHasExplicitIcon = !string.IsNullOrEmpty(rule.IconLookup) &&
                        (rule.IconType.Equals("monster", StringComparison.OrdinalIgnoreCase) ||
                         rule.IconType.Equals("structure", StringComparison.OrdinalIgnoreCase) ||
                         rule.IconType.Equals("system", StringComparison.OrdinalIgnoreCase) ||
                         rule.IconType.Equals("skinline", StringComparison.OrdinalIgnoreCase));

                    if (!ruleHasExplicitIcon)
                    {
                        if (rule.Keyword.Equals("Kill", StringComparison.OrdinalIgnoreCase) &&
                            iconTarget.Equals("Ward", StringComparison.OrdinalIgnoreCase))
                        {
                            // Destroying a ward uses the installed game's Warding
                            // Totem item icon, not a fictitious Ward champion.
                            iconType = "item";
                        }
                        else if (_aliasManager.IsValidChampion(iconTarget))
                        {
                            iconTarget = _aliasManager.GetInternalName(iconTarget);
                            iconType = "champion";
                            displayTargetName = cleanDisplayName;
                        }
                        else if (_monsterResolver.TryResolveTarget(iconTarget, out string monsterName))
                        {
                            iconType = "monster";
                            iconTarget = Regex.Replace(monsterName.Replace(' ', '_'),
                                @"(?<=[a-z0-9])(?=[A-Z])", "_");
                            displayTargetName = monsterName;
                        }
                        else if (IsStructure(iconTarget))
                        {
                            iconType = "structure";
                            iconTarget = GetStructureLookupName(iconTarget);
                            displayTargetName = iconTarget;
                        }
                        else if (iconType != "system" &&
                                 iconType != "item" &&
                                 iconType != "champion" &&
                                 await IsKnownItemAsync(iconTarget))
                        {
                            iconType = "item";
                        }
                        else if (iconType != "system" && iconType != "item" &&
                                 iconType != "champion")
                        {
                            iconType = "generic";
                        }
                    }

                    string cleanTargetKey = iconTarget.ToLowerInvariant().Replace(" ", "_").Replace("_", "");
                    string targetKey = $"target_{cleanTargetKey}";
                    string translatedTarget = _translationService.GetText(language, targetKey);
                    if (translatedTarget == targetKey)
                    {
                        // Prevent splitting acronyms (e.g., "ADC" shouldn't become "A D C")
                        if (iconTarget.Length <= 4 && iconTarget.All(char.IsUpper))
                        {
                            translatedTarget = iconTarget;
                        }
                        else
                        {
                            translatedTarget = displayTargetName;
                        }
                    }

                    // Special case for Spanish (ES): change "Matar a {0}" to "Matar al {0}" for specific roles
                    if (language.Equals("ES", StringComparison.OrdinalIgnoreCase) && 
                        rule.TranslationKey.Equals("interaction_kill_one", StringComparison.OrdinalIgnoreCase) &&
                        (cleanTargetKey == "adc" || cleanTargetKey == "apc" || cleanTargetKey == "support" || cleanTargetKey == "jungle"))
                    {
                        string template = _translationService.GetText(language, rule.TranslationKey);
                        template = template.Replace("Matar a {0}", "Matar al {0}").Replace("a {0}", "al {0}");
                        displayText = template.Replace("{0}", translatedTarget);
                    }
                    else
                    {
                        displayText = _translationService.GetText(language, rule.TranslationKey, translatedTarget);
                    }
                }
            }

            return new ParsedEvent { OriginalFolder = folderName, DisplayText = displayText, IconLookupName = iconTarget, IconType = iconType };
        }

        // A thematic event must retain the same representative icon on every analysis.
        // Stable ordering also keeps user-defined group list reordering from changing its icon.
        private static string ChooseRepresentative(IEnumerable<string> candidates, string themeName)
        {
            var ordered = candidates
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (ordered.Count == 0) return null;

            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in (themeName ?? string.Empty).ToUpperInvariant())
                {
                    hash ^= character;
                    hash *= 16777619;
                }

                return ordered[(int)(hash % (uint)ordered.Count)];
            }
        }

        private async Task<bool> IsKnownItemAsync(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return false;

            string resolvedItemId = await _dataFetcher.ResolveItemNameToIdAsync(target);
            return !string.IsNullOrEmpty(resolvedItemId);
        }

        private List<StructureMapping> _cachedStructures;
        private readonly object _structureLock = new();

        private bool IsStructure(string target)
        {
            EnsureStructuresLoaded();
            return _cachedStructures.Any(s => target.Contains(s.Keyword, StringComparison.OrdinalIgnoreCase));
        }

        private string GetStructureLookupName(string target)
        {
            EnsureStructuresLoaded();
            var match = _cachedStructures.FirstOrDefault(s => target.Contains(s.Keyword, StringComparison.OrdinalIgnoreCase));
            return match?.TargetName ?? "Turret";
        }

        private void EnsureStructuresLoaded()
        {
            if (_cachedStructures == null)
            {
                lock (_structureLock)
                {
                    if (_cachedStructures == null) _cachedStructures = LoadStructuresList();
                }
            }
        }

        private List<StructureMapping> LoadStructuresList()
        {
            try
            {
                string path = AppConfig.StructuresPath;
                if (File.Exists(path))
                {
                    // Use FileStream with ReadWrite share to avoid "File in use" errors during sync
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    string json = reader.ReadToEnd();
                    return JsonSerializer.Deserialize<List<StructureMapping>>(json) ?? new List<StructureMapping>();
                }
            }
            catch { }
            return new List<StructureMapping>();
        }

        private string NormalizeFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;
            string normalized = Regex.Replace(folderName, @"\b(2D|3D)\b", "", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"2D|3D", "", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, "Reaspawn", "Respawn", RegexOptions.IgnoreCase);
            // Strip the "Skinline" prefix that Riot embeds before thematic names (e.g. FirstEncounterSkinlineAnimaSquad)
            normalized = Regex.Replace(normalized, @"\b(SkinLine|Skinline|skinline)\b", "", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"SkinLine|Skinline|skinline", "", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, "Darking", "Darkin", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"_+", "_");
            return normalized.Trim('_');
        }

        private string StripOwnerPrefix(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;
            string pattern = @"^(_)?(Play_vo_|Play_|vo_|Play_vo_)([A-Za-z0-9]+?)(Skin\d+)?_";
            string stripped = Regex.Replace(folderName, pattern, "", RegexOptions.IgnoreCase);
            return stripped.TrimStart('_');
        }
    }
}
