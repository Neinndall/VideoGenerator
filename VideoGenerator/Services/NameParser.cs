using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    public class NameParser
    {
        private readonly TranslationService _translationService;
        private readonly DataFetcher _dataFetcher;
        private readonly RuleManager _ruleManager;
        private readonly GroupManager _groupManager;
        private readonly AliasManager _aliasManager;
        private static readonly Random _random = new();

        public NameParser(
            TranslationService translationService, 
            DataFetcher dataFetcher, 
            RuleManager ruleManager,
            GroupManager groupManager,
            AliasManager aliasManager)
        {
            _translationService = translationService;
            _dataFetcher = dataFetcher;
            _ruleManager = ruleManager;
            _groupManager = groupManager;
            _aliasManager = aliasManager;
        }

        // Clean folder name from common noise: leading/trailing underscores, redundant 2D/3D tokens, skin indices, etc.
        private string NormalizeFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;

            // Remove 2D / 3D Insensitively
            string normalized = Regex.Replace(folderName, @"\b(2D|3D)\b", "", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"2D|3D", "", RegexOptions.IgnoreCase);
            
            // Normalize double underscores or trailing/leading underscores
            normalized = Regex.Replace(normalized, @"_+", "_");
            normalized = normalized.Trim('_');

            return normalized;
        }

        public async Task<ParsedEvent> ParseFolderNameAsync(string folderName, string language)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                return new ParsedEvent
                {
                    OriginalFolder = folderName,
                    DisplayText = string.Empty,
                    IconLookupName = "Generic",
                    IconType = "generic"
                };
            }

            // 1. Check Item Events: BuyItem or UseItem
            if (folderName.Contains("BuyItem", StringComparison.OrdinalIgnoreCase) || 
                folderName.Contains("UseItem", StringComparison.OrdinalIgnoreCase))
            {
                var parsed = await ProcessItemEventAsync(folderName, language);
                if (parsed != null) return parsed;
            }

            // 2. Check Monster Events: Attack2D Monster/Jungle camps
            if (folderName.Contains("Attack2D", StringComparison.OrdinalIgnoreCase))
            {
                var parsed = await ProcessMonsterEventAsync(folderName, language);
                if (parsed != null) return parsed;
            }

            // 3. Check Skin Interactions (e.g. ChampionSkin1, ChampionSkin2, etc.)
            if (folderName.Contains("Skin", StringComparison.OrdinalIgnoreCase))
            {
                var parsed = await ProcessSkinInteractionEventAsync(folderName, language);
                if (parsed != null) return parsed;
            }

            // 4. Check Group interactions ending with Class (e.g. RespawnMage, KillAssassin)
            var matchedGroupClass = _groupManager.Groups.FirstOrDefault(g => g.Category == "Class" && folderName.EndsWith(g.Name, StringComparison.OrdinalIgnoreCase));
            if (matchedGroupClass != null)
            {
                var parsed = await ProcessGroupInteractionEventAsync(folderName, matchedGroupClass, language);
                if (parsed != null) return parsed;
            }

            // 4.5. Check Spell Casts / Hits / Basic Attacks (Dynamic Formatting)
            string workingFolder = StripOwnerPrefix(folderName);
            if (workingFolder.Contains("cast", StringComparison.OrdinalIgnoreCase) || 
                workingFolder.Contains("hit", StringComparison.OrdinalIgnoreCase) ||
                workingFolder.Contains("Attack", StringComparison.OrdinalIgnoreCase))
            {
                string cleanName = NormalizeFolderName(workingFolder);
                cleanName = Regex.Replace(cleanName, @"(?<!^)(?=[A-Z])", " ");
                cleanName = cleanName.Replace("_", " ");
                
                var words = cleanName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(w => char.ToUpper(w[0]) + w.Substring(1));
                
                string formattedText = string.Join(" ", words);

                return new ParsedEvent
                {
                    OriginalFolder = folderName,
                    DisplayText = formattedText,
                    IconLookupName = "Generic",
                    IconType = "generic"
                };
            }

            // 5. Dynamic Rules configured by the user, ordered by keyword length descending (greedy match prevention)
            string normalizedFolder = NormalizeFolderName(folderName);
            foreach (var rule in _ruleManager.Rules.OrderByDescending(r => r.Keyword.Length))
            {
                string normalizedKeyword = NormalizeFolderName(rule.Keyword);
                if (normalizedFolder.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    var parsed = await ProcessRuleEventAsync(folderName, rule, normalizedFolder, normalizedKeyword, language);
                    if (parsed != null) return parsed;
                }
            }

            // Default fallback
            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = folderName,
                IconLookupName = "Generic",
                IconType = "generic"
            };
        }

        private static readonly Dictionary<string, string> ItemNameToIdMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "EssenceReaver", "3508" },
            { "InfinityEdge", "3031" },
            { "KrakenSlayer", "6672" },
            { "RapidFirecannon", "3094" },
            { "GaleForce", "6671" },
            { "LordDominiksRegards", "3036" },
            { "Bloodthirster", "3072" },
            { "MortalReminder", "3033" },
            { "BladeOfTheRuinedKing", "3153" },
            { "GuinsoosRageblade", "3124" },
            { "RabadonsDeathcap", "3089" },
            { "ZhongyasHourglass", "3157" }, // Zhonya typo
            { "ZhonyasHourglass", "3157" }
        };

        private Task<ParsedEvent> ProcessItemEventAsync(string folderName, string language)
        {
            var match = Regex.Match(folderName, @"(BuyItem|UseItem)(2D|3D)?_?(.*)", RegexOptions.IgnoreCase);
            if (!match.Success) return Task.FromResult<ParsedEvent>(null);

            string action = match.Groups[1].Value;
            string itemIdOrName = match.Groups[3].Value.Trim('_');

            // Translate item clean name if matches our lookup
            string resolvedLookup = itemIdOrName;
            if (ItemNameToIdMap.TryGetValue(itemIdOrName, out string id))
            {
                resolvedLookup = id;
            }

            // Find key action
            string textKey = action.Equals("BuyItem", StringComparison.OrdinalIgnoreCase) ? "event_buy_item" : "event_use_item";
            
            // Format name to add spaces if it's text (e.g. EssenceReaver -> Essence Reaver)
            string displayItemName = itemIdOrName;
            if (!int.TryParse(itemIdOrName, out _))
            {
                displayItemName = Regex.Replace(itemIdOrName, @"(?<!^)(?=[A-Z])", " ");
            }

            string displayText = _translationService.GetText(language, textKey, 
                new Dictionary<string, string> { { "item_name", displayItemName } });

            return Task.FromResult(new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = resolvedLookup,
                IconType = "item"
            });
        }

        private Task<ParsedEvent> ProcessMonsterEventAsync(string folderName, string language)
        {
            var match = Regex.Match(folderName, @"Attack2D_?([A-Za-z0-9_]+)", RegexOptions.IgnoreCase);
            if (!match.Success) return Task.FromResult<ParsedEvent>(null);

            string monsterNameRaw = match.Groups[1].Value.Trim('_');

            if (monsterNameRaw.Equals("General", StringComparison.OrdinalIgnoreCase))
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
            
            // Strip "Square" suffix if it is already present in the folder name to avoid duplicate suffixes
            if (monsterNameRaw.EndsWith("Square", StringComparison.OrdinalIgnoreCase))
            {
                monsterNameRaw = monsterNameRaw.Substring(0, monsterNameRaw.Length - "Square".Length);
            }

            // Add underscores before capitals (e.g., BlueSentinel -> Blue_Sentinel)
            string monsterNameFormatted = Regex.Replace(monsterNameRaw, @"(?<!^)(?=[A-Z])", "_");
            // Normalize multiple underscores (e.g. Ancient__Krug to Ancient_Krug)
            monsterNameFormatted = Regex.Replace(monsterNameFormatted, @"_+", "_").Trim('_');
            string displayMonsterName = monsterNameFormatted.Replace("_", " ");

            string displayText = _translationService.GetText(language, "interaction_attack_monster", 
                new Dictionary<string, string> { { "monster", displayMonsterName } });

            return Task.FromResult(new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = monsterNameFormatted,
                IconType = "monster"
            });
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

        private async Task<ParsedEvent> ProcessSkinInteractionEventAsync(string folderName, string language)
        {
            string workingFolder = StripOwnerPrefix(folderName);
            // Remove 2D / 3D tokens to prevent them from corrupting champion names (e.g. 3DAphelios -> Aphelios)
            workingFolder = Regex.Replace(workingFolder, @"\b(2D|3D)\b", "", RegexOptions.IgnoreCase);
            workingFolder = Regex.Replace(workingFolder, @"2D|3D", "", RegexOptions.IgnoreCase);

            var skinMatches = Regex.Matches(workingFolder, @"([A-Za-z]+)Skin(\d+)");
            if (skinMatches.Count == 0) return null;

            List<string> processedChampions = new();
            string lastChampionName = "General";
            string lastChampionWithSkin = "General";

            foreach (Match match in skinMatches)
            {
                string rawChampionName = match.Groups[1].Value;
                int skinId = int.Parse(match.Groups[2].Value);

                string actualChampionName = rawChampionName;
                string[] prefixes = { "Kill", "FirstEncounter", "SecondEncounter", "MoveFirstAlly", "Move", "Assist", "AttackNear" };
                foreach (var prefix in prefixes)
                {
                    if (actualChampionName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        actualChampionName = actualChampionName.Substring(prefix.Length);
                        break;
                    }
                }

                // Clean the champion name through AliasManager
                actualChampionName = _aliasManager.GetInternalName(actualChampionName);

                string skinName = await FindSkinNameAsync(actualChampionName, skinId);
                if (skinName != null)
                {
                    string displaySkinTheme = GetDisplaySkinName(actualChampionName, skinName);
                    processedChampions.Add(!string.IsNullOrEmpty(displaySkinTheme) 
                        ? $"{actualChampionName} {displaySkinTheme}" 
                        : actualChampionName);
                }
                else
                {
                    processedChampions.Add($"{actualChampionName} (Skin {skinId})");
                }
                lastChampionName = actualChampionName;
                lastChampionWithSkin = $"{actualChampionName}Skin{skinId}";
            }

            string key;
            string iconType = "champion";
            string iconLookup = lastChampionWithSkin;

            if (folderName.Contains("Kill", StringComparison.OrdinalIgnoreCase))
            {
                key = processedChampions.Count == 1 ? "interaction_kill_one" : "interaction_kill_two";
            }
            else if (folderName.Contains("Death", StringComparison.OrdinalIgnoreCase))
            {
                key = processedChampions.Count == 1 ? "interaction_death_one" : "interaction_death_two";
            }
            else if (folderName.Contains("Assist", StringComparison.OrdinalIgnoreCase))
            {
                key = "interaction_assist_one";
            }
            else if (folderName.Contains("FirstEncounter", StringComparison.OrdinalIgnoreCase))
            {
                key = processedChampions.Count == 1 ? "interaction_first_encounter_one" : "interaction_first_encounter_two";
            }
            else
            {
                key = processedChampions.Count == 1 ? "interaction_generic_one" : "interaction_generic_two";
                iconType = "generic";
                iconLookup = "Generic";
            }

            string displayText = _translationService.GetText(language, key, processedChampions.Cast<object>().ToArray());

            return new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = iconLookup,
                IconType = iconType
            };
        }

        private Task<ParsedEvent> ProcessGroupInteractionEventAsync(string folderName, ThematicGroup matchedGroup, string language)
        {
            string actionPrefix = folderName.Substring(0, folderName.Length - matchedGroup.Name.Length);
            
            // Get random champion from group for the preview icon
            var champions = matchedGroup.GetChampionsList();
            string iconTarget = champions.Count > 0 ? champions[_random.Next(champions.Count)] : "General";

            string groupTranslationKey = $"class_{matchedGroup.Name.ToLower()}";
            string displayGroupName = _translationService.GetText(language, groupTranslationKey);

            string displayText;
            string iconType = "champion";

            if (actionPrefix.Contains("Kill", StringComparison.OrdinalIgnoreCase))
            {
                displayText = _translationService.GetText(language, "interaction_kill_class", displayGroupName);
            }
            else if (actionPrefix.Contains("Respawn", StringComparison.OrdinalIgnoreCase))
            {
                displayText = _translationService.GetText(language, "interaction_respawn_class", displayGroupName);
                iconTarget = "General";
                iconType = "generic";
            }
            else if (actionPrefix.Contains("FirstEncounter", StringComparison.OrdinalIgnoreCase))
            {
                displayText = _translationService.GetText(language, "interaction_first_encounter_class", displayGroupName);
            }
            else
            {
                displayText = $"{actionPrefix} {displayGroupName}";
                iconType = "generic";
            }

            return Task.FromResult(new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = iconTarget,
                IconType = iconType
            });
        }

        private Task<ParsedEvent> ProcessRuleEventAsync(string folderName, EventRule rule, string normalizedFolder, string normalizedKeyword, string language)
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

                return Task.FromResult(new ParsedEvent
                {
                    OriginalFolder = folderName,
                    DisplayText = simpleDisplayText,
                    IconLookupName = "Generic",
                    IconType = rule.IconType
                });
            }

            // Extraction logic
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
                if (rule.Type == RuleType.Interaction && matchedGroup != null)
                {
                    var candidates = matchedGroup.GetChampionsList();
                    iconTarget = candidates.Count > 0 ? candidates[_random.Next(candidates.Count)] : "General";
                    iconType = "champion";

                    string themeKey = $"{matchedGroup.Category.ToLower()}_{displayTargetName.ToLower().Replace(" ", "_")}";
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
                        else
                        {
                            iconType = "generic";
                            iconTarget = "Generic";
                        }
                    }
                    displayText = _translationService.GetText(language, rule.TranslationKey, displayTargetName);
                }
            }

            return Task.FromResult(new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = displayText,
                IconLookupName = iconTarget,
                IconType = iconType
            });
        }

        private async Task<string> FindSkinNameAsync(string championName, int skinId)
        {
            var skinsData = await _dataFetcher.GetSkinsDataAsync();
            if (skinsData == null) return null;

            foreach (var skin in skinsData.Values)
            {
                string skinName = skin.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                if (skinName.Contains(championName, StringComparison.OrdinalIgnoreCase))
                {
                    string splashPath = skin.TryGetProperty("splashPath", out var splashProp) ? splashProp.GetString() ?? "" : "";
                    var match = Regex.Match(splashPath, @"Skin(\d+)");
                    if (match.Success && int.Parse(match.Groups[1].Value) == skinId)
                    {
                        return skinName;
                    }
                }
            }
            return null;
        }

        private string GetDisplaySkinName(string championName, string fullSkinName)
        {
            string name = fullSkinName;
            if (name.StartsWith("After Hours ", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring("After Hours ".Length).Trim();
            }

            if (name.EndsWith(championName, StringComparison.OrdinalIgnoreCase))
            {
                string themeName = name.Substring(0, name.Length - championName.Length).Trim();
                if (string.Equals(themeName, "base", StringComparison.OrdinalIgnoreCase)) return "";
                if (themeName == "Spirit Blossom Springs") return "Springs Spirit Blossom";
                return themeName;
            }

            if (string.Equals(name, championName, StringComparison.OrdinalIgnoreCase)) return "";
            return name;
        }
    }
}
