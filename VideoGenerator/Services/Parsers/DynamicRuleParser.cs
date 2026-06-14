using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private readonly DataFetcher _dataFetcher;
        private static readonly Random _random = new();

        public DynamicRuleParser(
            TranslationService translationService,
            RuleManager ruleManager,
            GroupManager groupManager,
            AliasManager aliasManager,
            DataFetcher dataFetcher)
        {
            _translationService = translationService;
            _ruleManager = ruleManager;
            _groupManager = groupManager;
            _aliasManager = aliasManager;
            _dataFetcher = dataFetcher;
        }

        public bool CanParse(string folderName)
        {
            // Always run check dynamically inside ParseAsync or greedily match
            return true;
        }

        public async Task<ParsedEvent> ParseAsync(string folderName, string language)
        {
            string cleanFolder = StripOwnerPrefix(folderName);
            string normalizedFolder = NormalizeFolderName(cleanFolder);
            
            foreach (var rule in _ruleManager.Rules.OrderByDescending(r => r.Keyword.Length))
            {
                string normalizedKeyword = NormalizeFolderName(rule.Keyword);
                
                // Simple rules must match exactly (or with General/inGeneral suffixes) 
                // so they don't greedily swallow complex sub-events (e.g. SpellREndNoKill shouldn't match simple Spell)
                if (rule.Type == RuleType.Simple)
                {
                    bool isExactMatch = normalizedFolder.Equals(normalizedKeyword, StringComparison.OrdinalIgnoreCase);
                    bool isGeneralMatch = normalizedFolder.Equals(normalizedKeyword + "General", StringComparison.OrdinalIgnoreCase) ||
                                          normalizedFolder.Equals(normalizedKeyword + "inGeneral", StringComparison.OrdinalIgnoreCase) ||
                                          normalizedFolder.Equals(normalizedKeyword + "3DGeneral", StringComparison.OrdinalIgnoreCase) ||
                                          normalizedFolder.Equals(normalizedKeyword + "2DGeneral", StringComparison.OrdinalIgnoreCase);

                    if (!isExactMatch && !isGeneralMatch)
                    {
                        continue;
                    }
                }
                else
                {
                    if (!normalizedFolder.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                var parsed = await ProcessRuleEventAsync(folderName, rule, normalizedFolder, normalizedKeyword, language);
                if (parsed != null) return parsed;
            }

            return null;
        }

        private async Task<ParsedEvent> ProcessRuleEventAsync(string folderName, EventRule rule, string normalizedFolder, string normalizedKeyword, string language)
        {
            if (rule.Type == RuleType.Simple)
            {
                string simpleDisplayText = _translationService.GetText(language, rule.TranslationKey);
                if (normalizedFolder.EndsWith("General", StringComparison.OrdinalIgnoreCase) || 
                    normalizedFolder.EndsWith("inGeneral", StringComparison.OrdinalIgnoreCase))
                {
                    string suffix = _translationService.GetText(language, "suffix_in_general");
                    simpleDisplayText = $"{simpleDisplayText}{suffix}";
                }

                string iconLookup = "Generic";
                if (rule.IconType == "structure")
                {
                    if (normalizedFolder.Contains("Turret", StringComparison.OrdinalIgnoreCase) || 
                        normalizedFolder.Contains("Tower", StringComparison.OrdinalIgnoreCase))
                    {
                        iconLookup = "Turret";
                    }
                    else if (normalizedFolder.Contains("Inhibitor", StringComparison.OrdinalIgnoreCase))
                    {
                        iconLookup = "Inhibitor";
                    }
                    else if (normalizedFolder.Contains("Nexus", StringComparison.OrdinalIgnoreCase))
                    {
                        iconLookup = "Nexus";
                    }
                }

                return new ParsedEvent
                {
                    OriginalFolder = folderName,
                    DisplayText = simpleDisplayText,
                    IconLookupName = iconLookup,
                    IconType = rule.IconType
                };
            }

            // Extraction logic for interaction targets
            int index = normalizedFolder.IndexOf(normalizedKeyword, StringComparison.OrdinalIgnoreCase);
            string targetName = "";
            if (index >= 0)
            {
                targetName = normalizedFolder.Substring(index + normalizedKeyword.Length);
            }

            string iconTarget = string.IsNullOrEmpty(targetName) ? "General" : targetName;
            if (iconTarget.Equals("inGeneral", StringComparison.OrdinalIgnoreCase) || 
                iconTarget.Equals("3DGeneral", StringComparison.OrdinalIgnoreCase) || 
                iconTarget.Equals("2DGeneral", StringComparison.OrdinalIgnoreCase))
            {
                iconTarget = "General";
            }

            string iconType = rule.IconType;
            string displayText;

            if (iconTarget.Equals("General", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(iconTarget))
            {
                iconType = "generic";
                iconTarget = "Generic";

                if (rule.Keyword.Equals("FirstEncounter", StringComparison.OrdinalIgnoreCase))
                {
                    displayText = _translationService.GetText(language, "event_first_encounter_general");
                }
                else if (rule.Keyword.Equals("Kill", StringComparison.OrdinalIgnoreCase))
                {
                    displayText = _translationService.GetText(language, "event_kill_general");
                }
                else if (rule.Keyword.Equals("MoveFirst", StringComparison.OrdinalIgnoreCase))
                {
                    displayText = _translationService.GetText(language, "event_move_first");
                }
                else
                {
                    string baseText = _translationService.GetText(language, rule.TranslationKey);
                    string suffix = _translationService.GetText(language, "suffix_in_general");
                    displayText = $"{baseText}{suffix}";
                }
            }
            else
            {
                // Format targetName for display (e.g., "TahmKench" -> "Tahm Kench")
                string displayTargetName = Regex.Replace(iconTarget, @"(?<!^)(?=[A-Z])", " ").Trim();

                // Check dynamic groups from GroupManager
                var matchedGroup = _groupManager.Groups.FirstOrDefault(g => g.Name.Equals(displayTargetName, StringComparison.OrdinalIgnoreCase));
                if (matchedGroup != null)
                {
                    var candidates = matchedGroup.GetChampionsList();
                    iconTarget = candidates.Count > 0 ? championsRandomLookup(candidates) : "General";
                    iconType = "champion";

                    string themeKey = $"{matchedGroup.Category.ToLower()}_{displayTargetName.ToLower().Replace(" ", "_")}";
                    string themeDisplayName = _translationService.GetText(language, themeKey);
                    displayText = _translationService.GetText(language, rule.TranslationKey, themeDisplayName);
                }
                else if (await IsCommunityDragonSkinlineAsync(displayTargetName))
                {
                    var candidates = await GetCommunityDragonSkinlineChampionsAsync(displayTargetName);
                    iconTarget = candidates.Count > 0 ? championsRandomLookup(candidates) : "General";
                    iconType = "champion";

                    string themeKey = $"skinline_{displayTargetName.ToLower().Replace(" ", "_")}";
                    string themeDisplayName = _translationService.GetText(language, themeKey);
                    displayText = _translationService.GetText(language, rule.TranslationKey, themeDisplayName);
                }
                else
                {
                    // Clean target through AliasManager if champion
                    if (rule.IconType == "champion")
                    {
                        if (_aliasManager.IsValidChampion(iconTarget))
                        {
                            iconTarget = _aliasManager.GetInternalName(iconTarget);
                            displayTargetName = Regex.Replace(iconTarget, @"(?<!^)(?=[A-Z])", " ").Trim();
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
                        else
                        {
                            iconType = "generic";
                            iconTarget = "Generic";
                        }
                    }
                    displayText = _translationService.GetText(language, rule.TranslationKey, displayTargetName);
                }
            }

            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = iconTarget,
                IconType = iconType
            };
        }

        private List<StructureMapping> _cachedStructures;
        private readonly object _structureLock = new();

        private bool IsStructure(string target)
        {
            if (string.IsNullOrEmpty(target)) return false;

            EnsureStructuresLoaded();

            foreach (var mapping in _cachedStructures)
            {
                if (target.Contains(mapping.Keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private string GetStructureLookupName(string target)
        {
            if (string.IsNullOrEmpty(target)) return "Turret";

            EnsureStructuresLoaded();

            foreach (var mapping in _cachedStructures)
            {
                if (target.Contains(mapping.Keyword, StringComparison.OrdinalIgnoreCase))
                    return mapping.TargetName;
            }
            return "Turret";
        }

        private void EnsureStructuresLoaded()
        {
            if (_cachedStructures == null)
            {
                lock (_structureLock)
                {
                    if (_cachedStructures == null)
                    {
                        _cachedStructures = LoadStructuresList();
                    }
                }
            }
        }

        private List<StructureMapping> LoadStructuresList()
        {
            var defaults = new List<StructureMapping> {
                new StructureMapping { Keyword = "Turret", TargetName = "Turret" },
                new StructureMapping { Keyword = "Tower", TargetName = "Turret" },
                new StructureMapping { Keyword = "Inhibitor", TargetName = "Inhibitor" },
                new StructureMapping { Keyword = "Nexus", TargetName = "Nexus" }
            };

            try
            {
                string path = AppConfig.StructuresPath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var list = JsonSerializer.Deserialize<List<StructureMapping>>(json);
                    if (list != null && list.Count > 0)
                    {
                        return list;
                    }
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    string json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(path, json);
                }
            }
            catch { }

            return defaults;
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
                    if (_cachedMonsters == null)
                    {
                        _cachedMonsters = LoadMonstersList();
                    }
                }
            }

            foreach (var kw in _cachedMonsters)
            {
                if (target.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private List<string> LoadMonstersList()
        {
            var defaults = new List<string> {
                "Baron", "Nashor", "Dragon", "Drake", "Herald", "Sentinel", "Brambleback", 
                "Voidgrub", "Scuttle", "Crab", "Krug", "Wolf", "Wolves", "Murkwolf", 
                "Raptor", "Raptors", "Gromp", "Vilemaw", "Atakhan"
            };

            try
            {
                string path = AppConfig.MonstersPath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null && list.Count > 0)
                    {
                        return list;
                    }
                }
                else
                {
                    // Create the default file so the user can easily see and edit it
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    string json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(path, json);
                }
            }
            catch { }

            return defaults;
        }

        private async Task<bool> IsCommunityDragonSkinlineAsync(string targetName)
        {
            try
            {
                var skinLines = await _dataFetcher.GetSkinLinesAsync();
                return skinLines.Any(sl => 
                    sl.GetProperty("name").GetString().Equals(targetName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private async Task<List<string>> GetCommunityDragonSkinlineChampionsAsync(string targetName)
        {
            var champions = new List<string>();
            try
            {
                var skinLines = await _dataFetcher.GetSkinLinesAsync();
                var matchingLines = skinLines.Where(sl => 
                    sl.GetProperty("name").GetString().Equals(targetName, StringComparison.OrdinalIgnoreCase)).ToList();

                if (matchingLines.Count > 0)
                {
                    var lineIds = matchingLines.Select(ml => ml.GetProperty("id").GetInt32()).ToList();
                    var allSkins = await _dataFetcher.GetSkinsDataAsync();

                    var thematicSkins = allSkins.Values.Where(skin => 
                        skin.TryGetProperty("skinLines", out var slProp) && 
                        slProp.ValueKind == JsonValueKind.Array &&
                        slProp.EnumerateArray().Any(idObj => lineIds.Contains(idObj.GetProperty("id").GetInt32()))
                    ).ToList();

                    foreach (var skin in thematicSkins)
                    {
                        if (skin.TryGetProperty("splashPath", out var splashProp))
                        {
                            string splashPath = splashProp.GetString() ?? "";
                            var nameMatch = Regex.Match(splashPath, @"Characters/([^/]+)/");
                            if (nameMatch.Success)
                            {
                                string champName = nameMatch.Groups[1].Value;
                                string cleanName = _aliasManager.GetInternalName(champName);
                                if (!champions.Contains(cleanName) && _aliasManager.IsValidChampion(cleanName))
                                {
                                    champions.Add(cleanName);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return champions;
        }

        private string championsRandomLookup(List<string> candidates)
        {
            return candidates[_random.Next(candidates.Count)];
        }

        private string NormalizeFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;

            // Remove 2D / 3D Insensitively
            string normalized = Regex.Replace(folderName, @"\b(2D|3D)\b", "", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"2D|3D", "", RegexOptions.IgnoreCase);
            
            // Normalize "Darking" typo to "Darkin"
            normalized = Regex.Replace(normalized, "Darking", "Darkin", RegexOptions.IgnoreCase);

            // Normalize double underscores or trailing/leading underscores
            normalized = Regex.Replace(normalized, @"_+", "_");
            normalized = normalized.Trim('_');

            return normalized;
        }

        private string StripOwnerPrefix(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;
            var prefixMatch = Regex.Match(folderName, @"^(Play_vo_|Play_)([A-Za-z0-9]+?)(Skin\d+)?_", RegexOptions.IgnoreCase);
            if (prefixMatch.Success)
            {
                return folderName.Substring(prefixMatch.Length);
            }
            return folderName;
        }
    }
}
