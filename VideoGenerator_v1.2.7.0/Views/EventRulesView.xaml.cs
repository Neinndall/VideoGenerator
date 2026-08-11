using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Text.RegularExpressions;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;
using VideoGenerator.Services;
using VideoGenerator.Utils;
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace VideoGenerator.Views
{
    public partial class EventRulesView : UserControl
    {
        private readonly RuleManager _ruleManager;
        private readonly GroupManager _groupManager;
        private readonly AliasManager _aliasManager;
        private readonly TranslationService _translationService;

        public EventRulesView(RuleManager ruleManager, GroupManager groupManager, AliasManager aliasManager, TranslationService translationService)
        {
            InitializeComponent();
            _ruleManager = ruleManager;
            _groupManager = groupManager;
            _aliasManager = aliasManager;
            _translationService = translationService;
            
            DataContext = this;
            
            // Setup dynamic filtering for the Rules Repository
            _rulesView = System.Windows.Data.CollectionViewSource.GetDefaultView(_ruleManager.Rules);
            _rulesView.Filter = FilterRulesByCategory;

            IconTypeBox.SelectedIndex = 0;
            RuleTypeBox.SelectedIndex = 0;
            GroupCategoryBox.SelectedIndex = 0;
            LoadMonsters();
            LoadStructures();
            NewStructureTargetBox.SelectedIndex = 0;

            // Trigger filter refresh when category changes
            RuleSectionBox.SelectionChanged += (s, e) => _rulesView.Refresh();
            RuleTypeBox.SelectionChanged += (s, e) => SyncDictKey();

            KeywordBox.TextChanged += (s, e) => { SuggestCategoryFromKeyword(); SyncDictKey(); };
        }

        private void SyncDictKey()
        {
            string keyword = KeywordBox.Text.Trim();
            string type = RuleTypeBox.SelectedItem?.ToString() ?? "Simple";
            if (string.IsNullOrEmpty(keyword))
            {
                DictKeyBox.Text = "";
                return;
            }
            string prefix = type == "Simple" ? "event_" : "interaction_";
            DictKeyBox.Text = $"{prefix}{keyword.ToLower().Replace(" ", "_")}";
        }

        private void SuggestCategoryFromKeyword()
        {
            string keyword = KeywordBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(keyword)) return;

            string suggested = DetectCategory(keyword);
            if (string.IsNullOrEmpty(suggested)) return;

            for (int i = 0; i < RuleSectionBox.Items.Count; i++)
            {
                if (RuleSectionBox.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Content?.ToString(), suggested, StringComparison.OrdinalIgnoreCase))
                {
                    RuleSectionBox.SelectedIndex = i;
                    break;
                }
            }
        }

        private static string DetectCategory(string keyword)
        {
            string k = keyword.ToLowerInvariant();
            var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["COMBAT"] = new[] { "kill", "death", "dead", "assist", "ace", "slay", "combat", "killstreak", "multikill", "firstblood", "execute", "shutdown" },
                ["EMOTES"] = new[] { "joke", "taunt", "laugh", "dance", "emote" },
                ["MOVEMENT"] = new[] { "teleport", "recall", "run", "dash", "move", "movespeed", "walk", "blink" },
                ["ITEMS"] = new[] { "item", "buy", "shop", "purchase", "sell", "gold", "spend" },
                ["PINGS"] = new[] { "ping", "mia", "omw", "danger", "retreat", "assist", "caution", "enemymissing" },
                ["ABILITIES"] = new[] { "spell", "cast", "hit", "ability", "q", "w", "e", "r", "passive", "skill" },
                ["INTERACTIONS"] = new[] { "encounter", "interact", "skin", "firstencounter", "firstmeet", "banter" },
                ["SYSTEM"] = new[] { "system", "shopopen", "shopclose", "end", "surrender", "start", "lockin", "hud", "menu", "pause" }
            };

            foreach (var pair in map)
            {
                foreach (var token in pair.Value)
                {
                    if (k.Contains(token))
                        return pair.Key;
                }
            }

            return "OTHER";
        }

        private readonly System.ComponentModel.ICollectionView _rulesView;

        private bool FilterRulesByCategory(object obj)
        {
            if (obj is not EventRule rule) return false;
            
            string selectedCategory = (RuleSectionBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ALL";
            if (selectedCategory == "ALL" || string.IsNullOrEmpty(selectedCategory)) return true;

            return rule.Section != null && rule.Section.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase);
        }

        public RuleManager RuleManager => _ruleManager;
        public GroupManager GroupManager => _groupManager;
        public AliasManager AliasManager => _aliasManager;

        // --- Event Rules Logic ---

        public void PreFillFromDashboard(string folderName)
        {
            // Simple logic: remove prefixes to guess the keyword
            string clean = folderName;
            if (clean.StartsWith("_")) clean = clean.Substring(1);
            if (clean.StartsWith("Play_vo_")) clean = clean.Replace("Play_vo_", "");
            
            // Try to separate the action (e.g., Teleport3DGeneral -> Teleport)
            var match = Regex.Match(clean, @"^([A-Za-z]+?)(2D|3D|General|inGeneral|Skin\d+)");
            KeywordBox.Text = match.Success ? match.Groups[1].Value : clean;
            
            string suggested = DetectCategory(KeywordBox.Text);
            SuggestCategoryFromKeyword();
            IconTypeBox.SelectedIndex = 0; // Default to generic
        }

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            string keyword = KeywordBox.Text.Trim();
            string section = (RuleSectionBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "OTHER";
            string iconLookup = IconLookupBox.Text.Trim();
            string iconType = IconTypeBox.SelectedItem?.ToString() ?? "generic";
            
            if (!Enum.TryParse<RuleType>(RuleTypeBox.SelectedItem?.ToString(), out var ruleType))
            {
                ruleType = RuleType.Simple;
            }

            if (string.IsNullOrEmpty(keyword))
            {
                MessageBox.Show("Keyword is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string transKey = DictKeyBox.Text.Trim();
            if (string.IsNullOrEmpty(transKey))
            {
                MessageBox.Show("Dict key is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Duplicate Prevention
            if (_ruleManager.Rules.Any(r => r.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"A rule with the keyword '{keyword}' already exists.", "Duplicate Detected", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var newRule = new EventRule
            {
                Keyword = keyword,
                Section = section,
                TranslationKey = transKey,
                IconType = iconType,
                IconLookup = iconLookup,
                Type = ruleType,
                ExtractsTarget = ruleType != RuleType.Simple
            };

            int insertIndex = 0;
            while (insertIndex < _ruleManager.Rules.Count && string.Compare(_ruleManager.Rules[insertIndex].Keyword, keyword, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertIndex++;
            }
            _ruleManager.Rules.Insert(insertIndex, newRule);
            _ruleManager.SaveRules();

            // Fill composer context and show it
            ComposerKeywordText.Text = keyword;
            ComposerSectionText.Text = section;
            ComposerTypeText.Text = $"{iconType} · {ruleType}";
            TransKeyBox.Text = transKey;
            TransENBox.Text = "";
            TransESBox.Text = "";
            TransTRBox.Text = "";
            FormFieldsPanel.Visibility = Visibility.Collapsed;
            RegisterButtonBar.Visibility = Visibility.Collapsed;
            TranslationComposer.Visibility = Visibility.Visible;
        }

        private void SaveTranslation_Click(object sender, RoutedEventArgs e)
        {
            string key = TransKeyBox.Text.Trim();
            if (string.IsNullOrEmpty(key)) return;

            string enVal = TransENBox.Text.Trim();
            string esVal = TransESBox.Text.Trim();
            string trVal = TransTRBox.Text.Trim();

            if (!string.IsNullOrEmpty(enVal) || !string.IsNullOrEmpty(esVal) || !string.IsNullOrEmpty(trVal))
            {
                _translationService.UpdateTranslations(key, enVal, esVal, trVal);
            }

            TranslationComposer.Visibility = Visibility.Collapsed;
            FormFieldsPanel.Visibility = Visibility.Visible;
            RegisterButtonBar.Visibility = Visibility.Visible;
            TransENBox.Text = "";
            TransESBox.Text = "";
            TransTRBox.Text = "";
        }

        private void SkipTranslation_Click(object sender, RoutedEventArgs e)
        {
            TranslationComposer.Visibility = Visibility.Collapsed;
            FormFieldsPanel.Visibility = Visibility.Visible;
            RegisterButtonBar.Visibility = Visibility.Visible;
            TransENBox.Text = "";
            TransESBox.Text = "";
            TransTRBox.Text = "";
        }

        private void DeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is EventRule rule)
            {
                _ruleManager.Rules.Remove(rule);
                _ruleManager.SaveRules();
            }
        }

        // --- Thematic Groups Logic ---

        private void AddGroup_Click(object sender, RoutedEventArgs e)
        {
            string name = GroupNameBox.Text.Trim();
            string category = GroupCategoryBox.SelectedItem?.ToString() ?? "Custom";
            string champions = ChampionsBox.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(champions))
            {
                MessageBox.Show("Group Name and Champions are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Duplicate Prevention
            if (_groupManager.Groups.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"A group named '{name}' already exists.", "Duplicate Detected", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var newGroup = new ThematicGroup
            {
                Name = name,
                Category = category,
                ChampionsRaw = champions,
                IsOfficial = false
            };

            int insertIndex = 0;
            while (insertIndex < _groupManager.Groups.Count)
            {
                var current = _groupManager.Groups[insertIndex];
                int catCompare = string.Compare(current.Category, category, StringComparison.OrdinalIgnoreCase);
                if (catCompare > 0)
                {
                    break;
                }
                if (catCompare == 0 && string.Compare(current.Name, name, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    break;
                }
                insertIndex++;
            }
            _groupManager.Groups.Insert(insertIndex, newGroup);
            _groupManager.SaveGroups();

            GroupNameBox.Text = "";
            ChampionsBox.Text = "";
            GroupCategoryBox.SelectedIndex = 0;
        }

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ThematicGroup group)
            {
                if (group.IsOfficial)
                {
                    var result = MessageBox.Show($"'{group.Name}' is an official group. Are you sure you want to delete it?", "Delete Official Group", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result != MessageBoxResult.Yes) return;
                }

                _groupManager.Groups.Remove(group);
                _groupManager.SaveGroups();
            }
        }

        // --- Champion Aliases Logic ---

        private void AddAlias_Click(object sender, RoutedEventArgs e)
        {
            string display = AliasDisplayBox.Text.Trim();
            string internalName = AliasInternalBox.Text.Trim();

            if (string.IsNullOrEmpty(display))
            {
                MessageBox.Show("Champion Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
 
            if (string.IsNullOrEmpty(internalName))
            {
                internalName = display.Replace(" ", "").Replace("'", "").Replace(".", "").Replace("-", "");
            }

            // Duplicate Prevention
            if (_aliasManager.Aliases.Any(a => a.InternalName.Equals(internalName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"An alias with the Internal Name '{internalName}' already exists.", "Duplicate Detected", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var newAlias = new ChampionAlias
            {
                DisplayName = display,
                InternalName = internalName,
                IsOfficial = false
            };

            int insertIndex = 0;
            while (insertIndex < _aliasManager.Aliases.Count && string.Compare(_aliasManager.Aliases[insertIndex].DisplayName, display, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertIndex++;
            }
            _aliasManager.Aliases.Insert(insertIndex, newAlias);
            _aliasManager.SaveAliases();

            AliasDisplayBox.Text = "";
            AliasInternalBox.Text = "";
        }

        private void DeleteAlias_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ChampionAlias alias)
            {
                if (alias.IsOfficial)
                {
                    var result = MessageBox.Show($"'{alias.DisplayName}' is an official mapping. Delete anyway?", "Confirm", MessageBoxButton.YesNo);
                    if (result != MessageBoxResult.Yes) return;
                }
                _aliasManager.Aliases.Remove(alias);
                _aliasManager.SaveAliases();
            }
        }

        // --- Monsters Logic ---

        public ObservableCollection<string> MonsterList { get; } = new();

        private void LoadMonsters()
        {
            MonsterList.Clear();
            var db = new MonsterDatabase();

            try
            {
                string path = AppConfig.Paths.MonstersPath;
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    var loaded = System.Text.Json.JsonSerializer.Deserialize<MonsterDatabase>(json);
                    if (loaded != null) db = loaded;
                }
            }
            catch
            {
                // Legacy flat list fallback
                try
                {
                    string path = AppConfig.Paths.MonstersPath;
                    if (System.IO.File.Exists(path))
                    {
                        string json = System.IO.File.ReadAllText(path);
                        var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                        if (list != null) db.Large = list;
                    }
                }
                catch { }
            }

            foreach (var m in db.All) MonsterList.Add(m);
        }

        private void SaveMonsters()
        {
            try
            {
                    string path = AppConfig.Paths.MonstersPath;
                DirectoriesCreator.CreateParentDirectory(path);

                var db = new MonsterDatabase();
                foreach (var m in MonsterList)
                {
                    // Heuristic: dragons/heralds/baron/voidgrub go to Epic, the rest to Large
                    bool isEpic = m.Contains("Baron", StringComparison.OrdinalIgnoreCase) ||
                                  m.Contains("Dragon", StringComparison.OrdinalIgnoreCase) ||
                                  m.Contains("Drake", StringComparison.OrdinalIgnoreCase) ||
                                  m.Contains("Herald", StringComparison.OrdinalIgnoreCase) ||
                                  m.Contains("Voidgrub", StringComparison.OrdinalIgnoreCase);

                    var targetList = isEpic ? db.Epic : db.Large;
                    if (!targetList.Contains(m, StringComparer.OrdinalIgnoreCase))
                        targetList.Add(m);
                }

                string json = System.Text.Json.JsonSerializer.Serialize(db, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(path, json);
            }
            catch { }
        }

        private void AddMonster_Click(object sender, RoutedEventArgs e)
        {
            string name = NewMonsterBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Monster name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MonsterList.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show($"'{name}' is already in the monster list.", "Duplicate Detected", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Find insert index alphabetically
            int insertIndex = 0;
            while (insertIndex < MonsterList.Count && string.Compare(MonsterList[insertIndex], name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertIndex++;
            }

            MonsterList.Insert(insertIndex, name);
            SaveMonsters();
            NewMonsterBox.Text = "";
        }

        private void DeleteMonster_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string monsterName)
            {
                MonsterList.Remove(monsterName);
                SaveMonsters();
            }
        }

        // --- Structures Logic ---

        public ObservableCollection<StructureMapping> StructureList { get; } = new();

        private void LoadStructures()
        {
            StructureList.Clear();
            var defaults = new List<StructureMapping> {
                new StructureMapping { Keyword = "Turret", TargetName = "Turret" },
                new StructureMapping { Keyword = "Tower", TargetName = "Turret" },
                new StructureMapping { Keyword = "Inhibitor", TargetName = "Inhibitor" },
                new StructureMapping { Keyword = "Nexus", TargetName = "Nexus" }
            };

            List<StructureMapping> loadedList = new();
            try
            {
                string path = AppConfig.Paths.StructuresPath;
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<StructureMapping>>(json);
                    if (list != null) loadedList = list;
                }
            }
            catch { }

            // Smart Merge
            bool anyMerged = false;
            foreach (var d in defaults)
            {
                if (!loadedList.Any(s => s.Keyword.Equals(d.Keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    loadedList.Add(d);
                    anyMerged = true;
                }
            }

            foreach (var s in loadedList.OrderBy(x => x.Keyword)) StructureList.Add(s);
            
            if (anyMerged || StructureList.Count == 0) SaveStructures();
        }

        private void SaveStructures()
        {
            try
            {
                string path = AppConfig.Paths.StructuresPath;
                DirectoriesCreator.CreateParentDirectory(path);
                string json = System.Text.Json.JsonSerializer.Serialize(StructureList.ToList(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(path, json);
            }
            catch { }
        }

        private void AddStructure_Click(object sender, RoutedEventArgs e)
        {
            string keyword = NewStructureKeywordBox.Text.Trim();
            string targetName = NewStructureTargetBox.SelectedItem?.ToString() ?? "Turret";

            if (string.IsNullOrEmpty(keyword))
            {
                MessageBox.Show("Structure keyword is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StructureList.Any(s => s.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"A structure mapping with keyword '{keyword}' already exists.", "Duplicate Detected", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var newMapping = new StructureMapping
            {
                Keyword = keyword,
                TargetName = targetName
            };

            // Insert alphabetically by keyword
            int insertIndex = 0;
            while (insertIndex < StructureList.Count && string.Compare(StructureList[insertIndex].Keyword, keyword, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertIndex++;
            }

            StructureList.Insert(insertIndex, newMapping);
            SaveStructures();

            NewStructureKeywordBox.Text = "";
            NewStructureTargetBox.SelectedIndex = 0;
        }

        private void DeleteStructure_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is StructureMapping mapping)
            {
                StructureList.Remove(mapping);
                SaveStructures();
            }
        }
    }
}
