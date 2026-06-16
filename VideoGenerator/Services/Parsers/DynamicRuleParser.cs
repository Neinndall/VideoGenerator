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
        private static readonly Random _random = new();

        public DynamicRuleParser(
            TranslationService translationService,
            RuleManager ruleManager,
            GroupManager groupManager,
            AliasManager aliasManager,
            SkinlineManager skinlineManager)
        {
            _translationService = translationService;
            _ruleManager = ruleManager;
            _groupManager = groupManager;
            _aliasManager = aliasManager;
            _skinlineManager = skinlineManager;
        }

        public bool CanParse(string folderName)
        {
            return true;
        }

        public async Task<ParsedEvent> ParseAsync(string folderName, string language)
        {
            string cleanFolder = StripOwnerPrefix(folderName);
            string normalizedFolder = NormalizeFolderName(cleanFolder);
            
            foreach (var rule in _ruleManager.Rules.OrderByDescending(r => r.Keyword.Length))
            {
                string normalizedKeyword = NormalizeFolderName(rule.Keyword);
                
                if (rule.Type == RuleType.Simple)
                {
                    bool isExactMatch = normalizedFolder.Equals(normalizedKeyword, StringComparison.OrdinalIgnoreCase);
                    bool isGeneralMatch = normalizedFolder.Equals(normalizedKeyword + "General", StringComparison.OrdinalIgnoreCase) ||
                                          normalizedFolder.Equals(normalizedKeyword + "inGeneral", StringComparison.OrdinalIgnoreCase) ||
                                          normalizedFolder.Equals(normalizedKeyword + "3DGeneral", StringComparison.OrdinalIgnoreCase) ||
                                          normalizedFolder.Equals(normalizedKeyword + "2DGeneral", StringComparison.OrdinalIgnoreCase);

                    if (!isExactMatch && !isGeneralMatch) continue;
                }
                else
                {
                    if (!normalizedFolder.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)) continue;
                }

                var parsed = await ProcessRuleEventAsync(folderName, rule, normalizedFolder, normalizedKeyword, language);
                if (parsed != null) return parsed;
            }

            return null;
        }

        private async Task<ParsedEvent> ProcessRuleEventAsync(string folderName, EventRule rule, string normalizedFolder, string normalizedKeyword, string language)
        {
            string rawWithoutPrefix = StripOwnerPrefix(folderName);
            
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

            if (iconTarget.Equals("inGeneral", StringComparison.OrdinalIgnoreCase) || 
                iconTarget.Equals("3DGeneral", StringComparison.OrdinalIgnoreCase) || 
                iconTarget.Equals("2DGeneral", StringComparison.OrdinalIgnoreCase) ||
                iconTarget.Equals("General", StringComparison.OrdinalIgnoreCase))
            {
                iconTarget = "General";
            }

            string iconType = rule.IconType;
            string displayText;

            // --- CASE A: GENERAL EVENT ---
            if (iconTarget.Equals("General", StringComparison.OrdinalIgnoreCase))
            {
                if (iconType != "system" && iconType != "item") iconType = "generic";
                
                iconTarget = string.IsNullOrEmpty(rule.IconLookup) ? "Generic" : rule.IconLookup;

                bool isTrulyGeneral = folderName.Contains("General", StringComparison.OrdinalIgnoreCase) ||
                                      folderName.Contains("inGeneral", StringComparison.OrdinalIgnoreCase);

                if (rule.Keyword.Equals("FirstEncounter", StringComparison.OrdinalIgnoreCase))
                    displayText = _translationService.GetText(language, "event_first_encounter_general");
                else if (rule.Keyword.Equals("Kill", StringComparison.OrdinalIgnoreCase))
                    displayText = _translationService.GetText(language, "event_kill_general");
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
                
                if (matchedGroup != null)
                {
                    if (matchedGroup.Category.Equals("Region", StringComparison.OrdinalIgnoreCase))
                    {
                        iconTarget = displayTargetName;
                        iconType = "region";
                    }
                    else
                    {
                        var candidates = matchedGroup.GetChampionsList();
                        iconTarget = candidates.Count > 0 ? candidates[_random.Next(candidates.Count)] : "General";
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
                    var candidates = _skinlineManager.GetChampionsWithSkin(displayTargetName);
                    var chosen = candidates.Count > 0 ? candidates[_random.Next(candidates.Count)] : null;
                    iconTarget = chosen != null ? chosen.ToString() : "General";
                    iconType = "champion";

                    string themeKey = $"skinline_{displayTargetName.ToLower().Replace(" ", "_")}";
                    string themeDisplayName = _translationService.GetText(language, themeKey);
                    if (themeDisplayName == themeKey)
                    {
                        // No localized name available: use the official skinline name
                        themeDisplayName = _skinlineManager.GetDisplayName(displayTargetName);
                    }
                    displayText = _translationService.GetText(language, rule.TranslationKey, themeDisplayName);
                }
                else
                {
                    if (_aliasManager.IsValidChampion(iconTarget))
                    {
                        iconTarget = _aliasManager.GetInternalName(iconTarget);
                        iconType = "champion";
                        displayTargetName = cleanDisplayName;
                    }
                    else if (IsMonster(iconTarget))
                    {
                        iconType = "monster";
                        displayTargetName = Regex.Replace(iconTarget, @"(?<!^)(?=[A-Z])", " ").Trim();
                    }
                    else if (IsStructure(iconTarget))
                    {
                        iconType = "structure";
                        iconTarget = GetStructureLookupName(iconTarget);
                        displayTargetName = iconTarget;
                    }
                    else if (iconType != "system" && iconType != "item")
                    {
                        iconType = "generic";
                    }

                    displayText = _translationService.GetText(language, rule.TranslationKey, displayTargetName);
                }
            }

            return new ParsedEvent { OriginalFolder = folderName, DisplayText = displayText, IconLookupName = iconTarget, IconType = iconType };
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

        private List<string> _cachedMonsters;
        private readonly object _monsterLock = new();

        private bool IsMonster(string target)
        {
            if (string.IsNullOrEmpty(target)) return false;
            if (_cachedMonsters == null)
            {
                lock (_monsterLock)
                {
                    if (_cachedMonsters == null) _cachedMonsters = LoadMonstersList();
                }
            }
            return _cachedMonsters.Any(kw => target.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        private List<string> LoadMonstersList()
        {
            try
            {
                string path = AppConfig.MonstersPath;
                if (File.Exists(path))
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    string json = reader.ReadToEnd();
                    var db = JsonSerializer.Deserialize<MonsterDatabase>(json);
                    if (db != null) return db.All;
                }
            }
            catch
            {
                // Fallback to legacy flat list format
                try
                {
                    string path = AppConfig.MonstersPath;
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path);
                        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                    }
                }
                catch { }
            }
            return new List<string>();
        }

        private string NormalizeFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;
            string normalized = Regex.Replace(folderName, @"\b(2D|3D)\b", "", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"2D|3D", "", RegexOptions.IgnoreCase);
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
