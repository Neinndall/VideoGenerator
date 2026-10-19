using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Text.RegularExpressions;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;
using VideoGenerator.Views.Dialogs;
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
        private readonly DataFetcher _dataFetcher;
        private EventRule _editingRule;
        private bool _isPopulatingRule;
        private bool _monsterCatalogWritable = true;
        private string _monsterRevisionAtLoad;
        private bool _structureCatalogWritable = true;
        private string _structureRevisionAtLoad;

        public EventRulesView(RuleManager ruleManager, GroupManager groupManager,
            AliasManager aliasManager, TranslationService translationService, DataFetcher dataFetcher)
        {
            InitializeComponent();
            _ruleManager = ruleManager;
            _groupManager = groupManager;
            _aliasManager = aliasManager;
            _translationService = translationService;
            _dataFetcher = dataFetcher;
            
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

            // Editing a rule must not change what is visible in the repository.
            RuleRepositoryFilterBox.SelectionChanged += (s, e) => _rulesView.Refresh();
            RuleTypeBox.SelectionChanged += (s, e) => SyncDictKey();

            KeywordBox.TextChanged += (s, e) => { SuggestCategoryFromKeyword(); SyncDictKey(); };
        }

        private void SyncDictKey()
        {
            if (_isPopulatingRule || _editingRule != null)
            {
                return;
            }

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
            // Ping-specific keywords take precedence over generic combat phrases
            // such as "assist", which also occurs in "AssistPing".
            if (k.Contains("ping") || k.Contains("omw") || k.Contains("enemymissing") ||
                k.Contains("caution") || k.Contains("retreat") || k == "mia")
            {
                return "PINGS";
            }

            // A single ability-slot letter is meaningful only as a whole keyword.
            if (k is "q" or "w" or "e" or "r")
            {
                return "ABILITIES";
            }

            var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["COMBAT"] = new[] { "kill", "death", "dead", "assist", "ace", "slay", "combat", "attack", "killstreak", "multikill", "firstblood", "execute", "shutdown" },
                ["EMOTES"] = new[] { "joke", "taunt", "laugh", "dance", "emote" },
                ["MOVEMENT"] = new[] { "teleport", "recall", "run", "dash", "move", "movespeed", "walk", "blink" },
                ["ITEMS"] = new[] { "item", "buy", "shop", "purchase", "sell", "gold", "spend" },
                ["PINGS"] = new[] { "ping", "mia", "omw", "danger", "retreat", "assist", "caution", "enemymissing" },
                ["ABILITIES"] = new[] { "spell", "cast", "hit", "ability", "passive", "skill" },
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
            
            string selectedCategory = (RuleRepositoryFilterBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ALL";
            if (selectedCategory == "ALL" || string.IsNullOrEmpty(selectedCategory)) return true;

            return rule.Section != null && rule.Section.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase);
        }

        public RuleManager RuleManager => _ruleManager;
        public GroupManager GroupManager => _groupManager;
        public AliasManager AliasManager => _aliasManager;

        // --- Event Rules Logic ---

        public void PreFillFromDashboard(string folderName)
        {
            ExitEditMode();

            // Simple logic: remove prefixes to guess the keyword
            string clean = folderName ?? string.Empty;
            clean = Regex.Replace(
                clean,
                @"^(_)?(Play_vo_|Play_|vo_)([A-Za-z0-9]+?)(Skin\d+)?_",
                "",
                RegexOptions.IgnoreCase);
            
            // Try to separate the action (e.g., Teleport3DGeneral -> Teleport)
            var match = Regex.Match(clean, @"^([A-Za-z][A-Za-z0-9_]*?)(2D|3D|General|inGeneral|Skin\d+|$)", RegexOptions.IgnoreCase);
            KeywordBox.Text = match.Success ? match.Groups[1].Value.Trim('_') : clean.Trim('_');
            
            SuggestCategoryFromKeyword();
            IconTypeBox.SelectedIndex = 0; // Default to generic
        }

        private void EditRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not EventRule rule)
            {
                return;
            }

            _editingRule = rule;
            _isPopulatingRule = true;
            try
            {
                KeywordBox.Text = rule.Keyword;
                DictKeyBox.Text = rule.TranslationKey;
                IconLookupBox.Text = rule.IconLookup ?? string.Empty;
                SelectComboBoxValue(RuleSectionBox, rule.Section);
                SelectComboBoxValue(IconTypeBox, rule.IconType);
                SelectComboBoxValue(RuleTypeBox, rule.Type.ToString());
            }
            finally
            {
                _isPopulatingRule = false;
            }

            RuleFormTitle.Text = "EDIT BEHAVIOR";
            SaveRuleButton.Content = "SAVE CHANGES";
            CancelEditButton.Visibility = Visibility.Visible;
            TranslationComposer.Visibility = Visibility.Collapsed;
            FormFieldsPanel.Visibility = Visibility.Visible;
            RegisterButtonBar.Visibility = Visibility.Visible;
        }

        private static void SelectComboBoxValue(System.Windows.Controls.ComboBox comboBox, string value)
        {
            for (int index = 0; index < comboBox.Items.Count; index++)
            {
                object item = comboBox.Items[index];
                string itemValue = item is ComboBoxItem comboBoxItem
                    ? comboBoxItem.Content?.ToString()
                    : item?.ToString();

                if (string.Equals(itemValue, value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }

            comboBox.SelectedIndex = -1;
        }

        private EventRule ReadRuleFromForm()
        {
            string section = (RuleSectionBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "OTHER";
            string iconType = IconTypeBox.SelectedItem?.ToString() ?? "generic";
            if (!Enum.TryParse<RuleType>(RuleTypeBox.SelectedItem?.ToString(), out RuleType ruleType))
            {
                ruleType = RuleType.Simple;
            }

            return new EventRule
            {
                Keyword = KeywordBox.Text.Trim(),
                Section = section,
                TranslationKey = DictKeyBox.Text.Trim(),
                IconType = iconType,
                IconLookup = IconLookupBox.Text.Trim(),
                Type = ruleType,
                ExtractsTarget = ruleType != RuleType.Simple
            };
        }

        private static void ApplyRuleValues(EventRule source, EventRule target)
        {
            target.Keyword = source.Keyword;
            target.Section = source.Section;
            target.TranslationKey = source.TranslationKey;
            target.IconType = source.IconType;
            target.IconLookup = source.IconLookup;
            target.Type = source.Type;
            target.ExtractsTarget = source.ExtractsTarget;
        }

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            EventRule formRule = ReadRuleFromForm();
            string keyword = formRule.Keyword;

            if (string.IsNullOrEmpty(keyword))
            {
                ModernMessageBox.Show("Keyword is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(formRule.TranslationKey))
            {
                ModernMessageBox.Show("Dict key is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Duplicate Prevention
            if (_ruleManager.Rules.Any(rule => !ReferenceEquals(rule, _editingRule) &&
                                               rule.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                ModernMessageBox.Show($"A rule with the keyword '{keyword}' already exists.", "Duplicate Detected", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_editingRule != null)
            {
                ApplyRuleValues(formRule, _editingRule);
                _ruleManager.SaveRules();
                _rulesView.Refresh();
                ExitEditMode();
                return;
            }

            int insertIndex = 0;
            while (insertIndex < _ruleManager.Rules.Count &&
                   string.Compare(_ruleManager.Rules[insertIndex].Keyword, keyword, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertIndex++;
            }
            _ruleManager.Rules.Insert(insertIndex, formRule);
            _ruleManager.SaveRules();

            // Fill composer context and show it
            ComposerKeywordText.Text = keyword;
            ComposerSectionText.Text = formRule.Section;
            ComposerTypeText.Text = $"{formRule.IconType} \u00B7 {formRule.Type}";
            TransKeyBox.Text = formRule.TranslationKey;
            TransENBox.Text = "";
            TransESBox.Text = "";
            TransTRBox.Text = "";
            FormFieldsPanel.Visibility = Visibility.Collapsed;
            RegisterButtonBar.Visibility = Visibility.Collapsed;
            TranslationComposer.Visibility = Visibility.Visible;
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode();
        }

        private void ExitEditMode()
        {
            _editingRule = null;
            _isPopulatingRule = true;
            try
            {
                KeywordBox.Text = string.Empty;
                DictKeyBox.Text = string.Empty;
                IconLookupBox.Text = string.Empty;
                RuleSectionBox.SelectedIndex = -1;
                SelectComboBoxValue(IconTypeBox, "generic");
                SelectComboBoxValue(RuleTypeBox, RuleType.Simple.ToString());
            }
            finally
            {
                _isPopulatingRule = false;
            }

            RuleFormTitle.Text = "NEW BEHAVIOR";
            SaveRuleButton.Content = "REGISTER EVENT";
            CancelEditButton.Visibility = Visibility.Collapsed;
            TranslationComposer.Visibility = Visibility.Collapsed;
            FormFieldsPanel.Visibility = Visibility.Visible;
            RegisterButtonBar.Visibility = Visibility.Visible;
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

            TransENBox.Text = "";
            TransESBox.Text = "";
            TransTRBox.Text = "";
            ExitEditMode();
        }

        private void SkipTranslation_Click(object sender, RoutedEventArgs e)
        {
            TransENBox.Text = "";
            TransESBox.Text = "";
            TransTRBox.Text = "";
            ExitEditMode();
        }

        private void DeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is EventRule rule)
            {
                if (ModernMessageBox.Show(
                    $"Delete the event rule '{rule.Keyword}'? This will change how matching folders are interpreted.",
                    "Delete Event Rule",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }

                _ruleManager.Rules.Remove(rule);
                _ruleManager.SaveRules();

                if (ReferenceEquals(rule, _editingRule))
                {
                    ExitEditMode();
                }
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
                ModernMessageBox.Show("Group Name and Champions are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Duplicate Prevention
            if (_groupManager.Groups.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                ModernMessageBox.Show($"A group named '{name}' already exists.", "Duplicate Detected", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (!_groupManager.SaveGroups())
            {
                _groupManager.Groups.Remove(newGroup);
                ModernMessageBox.Show("The groups.json file could not be saved. Your existing data has been preserved.",
                    "Groups Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
                    ModernMessageBox.Show($"'{group.Name}' is a built-in group. You cannot remove the application's defaults.",
                        "Built-in Group", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                int previous = _groupManager.Groups.IndexOf(group);
                _groupManager.Groups.Remove(group);
                if (!_groupManager.SaveGroups())
                {
                    _groupManager.Groups.Insert(previous, group);
                    ModernMessageBox.Show("The groups.json file could not be saved. Your existing data has been preserved.",
                        "Groups Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // --- Champion Aliases Logic ---

        private void AddAlias_Click(object sender, RoutedEventArgs e)
        {
            string display = AliasDisplayBox.Text.Trim();
            string internalName = AliasInternalBox.Text.Trim();

            if (string.IsNullOrEmpty(display))
            {
                ModernMessageBox.Show("Champion Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
 
            if (string.IsNullOrEmpty(internalName))
            {
                internalName = display.Replace(" ", "").Replace("'", "").Replace(".", "").Replace("-", "");
            }

            // Duplicate Prevention
            if (_aliasManager.Aliases.Any(a =>
                a.DisplayName.Equals(display, StringComparison.OrdinalIgnoreCase)))
            {
                ModernMessageBox.Show($"A champion alias named '{display}' already exists.", "Duplicate Detected", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (!_aliasManager.SaveAliases())
            {
                _aliasManager.Aliases.Remove(newAlias);
                ModernMessageBox.Show("The champion_aliases.json file could not be saved. Existing aliases have been preserved.",
                    "Champions Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AliasDisplayBox.Text = "";
            AliasInternalBox.Text = "";
        }

        private void DeleteAlias_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ChampionAlias alias)
            {
                if (alias.IsOfficial)
                {
                    ModernMessageBox.Show($"'{alias.DisplayName}' is a built-in champion alias and cannot be deleted.",
                        "Built-in Champion", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                int previous = _aliasManager.Aliases.IndexOf(alias);
                _aliasManager.Aliases.Remove(alias);
                if (!_aliasManager.SaveAliases())
                {
                    _aliasManager.Aliases.Insert(previous, alias);
                    ModernMessageBox.Show("The champion_aliases.json file could not be saved. Existing aliases have been preserved.",
                        "Champions Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // --- Monsters Logic ---

        public ObservableCollection<MonsterCatalogRow> MonsterList { get; } = new();

        private void LoadMonsters()
        {
            MonsterList.Clear();
            var db = new MonsterDatabase();
            _monsterCatalogWritable = _dataFetcher.TryLoadMonsterDatabase(out db);
            _monsterRevisionAtLoad = _dataFetcher.MonsterCatalogRevision;
            foreach (var entry in MonsterCatalogEditor.CreateRows(db))
                MonsterList.Add(entry);
        }

        private bool SaveMonsters()
        {
            if (!_monsterCatalogWritable ||
                _monsterRevisionAtLoad != _dataFetcher.MonsterCatalogRevision)
            {
                ModernMessageBox.Show(
                    "monsters.json could not be read or was changed elsewhere. Your original data is untouched. Reopen Event Mapping to refresh the list.",
                    "Monster Catalog Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            try
            {
                string path = AppConfig.MonstersPath;
                DirectoriesCreator.CreateParentDirectory(path);
                MonsterDatabase db = MonsterCatalogEditor.ToDatabase(MonsterList);
                string json = System.Text.Json.JsonSerializer.Serialize(db,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    System.IO.File.WriteAllText(temp, json);
                    System.IO.File.Move(temp, path, overwrite: true);
                }
                finally
                {
                    if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp);
                }
                _monsterRevisionAtLoad = _dataFetcher.MonsterCatalogRevision;
                return true;
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"The monsters list could not be saved: {ex.Message}",
                    "Save Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void AddMonster_Click(object sender, RoutedEventArgs e)
        {
            string name = NewMonsterBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ModernMessageBox.Show("Monster name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DefaultMonsters.Normalize(name).Length == 0)
            {
                ModernMessageBox.Show("Enter a valid monster name using letters or numbers.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DefaultMonsters.TryResolveName(name, out _) ||
                MonsterList.Any(row =>
                    DefaultMonsters.Normalize(row.Name) == DefaultMonsters.Normalize(name)))
            {
                ModernMessageBox.Show($"'{name}' is already in the monster list.", "Duplicate Detected", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Find insert index alphabetically
            int insertIndex = 0;
            while (insertIndex < MonsterList.Count && string.Compare(
                       MonsterList[insertIndex].Name, name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertIndex++;
            }

            var newRow = new MonsterCatalogRow(name, false, MonsterCatalogSection.Large);
            MonsterList.Insert(insertIndex, newRow);
            if (!SaveMonsters())
            {
                MonsterList.Remove(newRow);
                return;
            }
            NewMonsterBox.Text = "";
        }

        private void DeleteMonster_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.CommandParameter is MonsterCatalogRow row && row.CanDelete)
            {
                int index = MonsterList.IndexOf(row);
                if (index < 0) return;
                MonsterList.RemoveAt(index);
                if (!SaveMonsters()) MonsterList.Insert(index, row);
            }
        }

        // --- Structures Logic ---

        public ObservableCollection<StructureCatalogRow> StructureList { get; } = new();

        private void LoadStructures()
        {
            StructureList.Clear();
            _structureCatalogWritable = _dataFetcher.TryLoadStructureMappings(
                out List<StructureMapping> userMappings);
            _structureRevisionAtLoad = _dataFetcher.StructureCatalogRevision;
            foreach (var row in StructureCatalogEditor.CreateRows(userMappings))
                StructureList.Add(row);
            // App defaults are merged into structures.json during startup and
            // the Mapping editor saves that same merged list.
        }

        private bool SaveStructures()
        {
            if (!_structureCatalogWritable ||
                _structureRevisionAtLoad != _dataFetcher.StructureCatalogRevision)
            {
                ModernMessageBox.Show(
                    "structures.json could not be read or was modified elsewhere. Its existing content has been preserved. Please reopen Event Mapping before editing.",
                    "Structure Catalog Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            try
            {
                string path = AppConfig.StructuresPath;
                DirectoriesCreator.CreateParentDirectory(path);
                string json = System.Text.Json.JsonSerializer.Serialize(
                    StructureCatalogEditor.ToDatabaseMappings(StructureList),
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    System.IO.File.WriteAllText(temp, json);
                    System.IO.File.Move(temp, path, overwrite: true);
                }
                finally
                {
                    if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp);
                }
                _structureRevisionAtLoad = _dataFetcher.StructureCatalogRevision;
                return true;
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"The structure mappings could not be saved: {ex.Message}",
                    "Save Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void AddStructure_Click(object sender, RoutedEventArgs e)
        {
            string keyword = NewStructureKeywordBox.Text.Trim();
            string targetName = NewStructureTargetBox.SelectedItem?.ToString() ?? "Turret";

            if (string.IsNullOrEmpty(keyword))
            {
                ModernMessageBox.Show("Structure keyword is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DefaultStructures.Normalize(keyword).Length == 0)
            {
                ModernMessageBox.Show("Enter a keyword containing letters or numbers.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StructureList.Any(s =>
                DefaultStructures.Normalize(s.Keyword) == DefaultStructures.Normalize(keyword)))
            {
                ModernMessageBox.Show($"A structure mapping with keyword '{keyword}' already exists.", "Duplicate Detected", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var newMapping = new StructureCatalogRow(keyword, targetName, false);

            // Insert alphabetically by keyword
            int insertIndex = 0;
            while (insertIndex < StructureList.Count && string.Compare(StructureList[insertIndex].Keyword, keyword, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertIndex++;
            }

            StructureList.Insert(insertIndex, newMapping);
            if (!SaveStructures())
            {
                StructureList.Remove(newMapping);
                return;
            }

            NewStructureKeywordBox.Text = "";
            NewStructureTargetBox.SelectedIndex = 0;
        }

        private void DeleteStructure_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is StructureCatalogRow mapping && mapping.CanDelete)
            {
                int index = StructureList.IndexOf(mapping);
                if (index < 0) return;
                StructureList.RemoveAt(index);
                if (!SaveStructures()) StructureList.Insert(index, mapping);
                else if (DefaultStructures.Entries.Any(entry =>
                    DefaultStructures.Normalize(entry.Keyword) ==
                    DefaultStructures.Normalize(mapping.Keyword)))
                    LoadStructures(); // After removing an override, reveal its built-in.
            }
        }
    }
}
