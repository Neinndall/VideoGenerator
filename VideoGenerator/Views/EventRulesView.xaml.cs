using System;
using System.Linq;
using System.Windows;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;
using VideoGenerator.Services;
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace VideoGenerator.Views
{
    public partial class EventRulesView : UserControl
    {
        private readonly RuleManager _ruleManager;
        private readonly GroupManager _groupManager;
        private readonly AliasManager _aliasManager;

        public EventRulesView(RuleManager ruleManager, GroupManager groupManager, AliasManager aliasManager)
        {
            InitializeComponent();
            _ruleManager = ruleManager;
            _groupManager = groupManager;
            _aliasManager = aliasManager;
            
            DataContext = this;
            
            IconTypeBox.SelectedIndex = 0;
            RuleTypeBox.SelectedIndex = 0;
            GroupCategoryBox.SelectedIndex = 0;
            LoadMonsters();
            LoadStructures();
            NewStructureTargetBox.SelectedIndex = 0;
        }

        public RuleManager RuleManager => _ruleManager;
        public GroupManager GroupManager => _groupManager;
        public AliasManager AliasManager => _aliasManager;

        // --- Event Rules Logic ---

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            string keyword = KeywordBox.Text.Trim();
            string translationKey = TranslationKeyBox.Text.Trim();
            string iconLookup = IconLookupBox.Text.Trim();
            string iconType = IconTypeBox.SelectedItem?.ToString() ?? "generic";
            
            if (!Enum.TryParse<RuleType>(RuleTypeBox.SelectedItem?.ToString(), out var ruleType))
            {
                ruleType = RuleType.Simple;
            }

            if (string.IsNullOrEmpty(keyword) || string.IsNullOrEmpty(translationKey))
            {
                MessageBox.Show("Keyword and Dictionary Key are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                TranslationKey = translationKey,
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

            KeywordBox.Text = "";
            TranslationKeyBox.Text = "";
            IconLookupBox.Text = "";
            IconTypeBox.SelectedIndex = 0;
            RuleTypeBox.SelectedIndex = 0;
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
                MessageBox.Show($"A thematic group named '{name}' already exists.", "Duplicate Detected", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public System.Collections.ObjectModel.ObservableCollection<string> MonsterList { get; } = new();

        private void LoadMonsters()
        {
            MonsterList.Clear();
            var defaults = new System.Collections.Generic.List<string> {
                "Baron", "Nashor", "Dragon", "Drake", "Herald", "Sentinel", "Brambleback", 
                "Voidgrub", "Scuttle", "Crab", "Krug", "Wolf", "Wolves", "Murkwolf", 
                "Raptor", "Raptors", "Gromp", "Vilemaw", "Atakhan"
            };

            try
            {
                string path = AppConfig.MonstersPath;
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(json);
                    if (list != null)
                    {
                        foreach (var m in list) MonsterList.Add(m);
                        return;
                    }
                }
            }
            catch { }

            foreach (var m in defaults) MonsterList.Add(m);
            SaveMonsters();
        }

        private void SaveMonsters()
        {
            try
            {
                string path = AppConfig.MonstersPath;
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                string json = System.Text.Json.JsonSerializer.Serialize(MonsterList.ToList(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
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

        public System.Collections.ObjectModel.ObservableCollection<StructureMapping> StructureList { get; } = new();

        private void LoadStructures()
        {
            StructureList.Clear();
            var defaults = new System.Collections.Generic.List<StructureMapping> {
                new StructureMapping { Keyword = "Turret", TargetName = "Turret" },
                new StructureMapping { Keyword = "Tower", TargetName = "Turret" },
                new StructureMapping { Keyword = "Inhibitor", TargetName = "Inhibitor" },
                new StructureMapping { Keyword = "Nexus", TargetName = "Nexus" }
            };

            try
            {
                string path = AppConfig.StructuresPath;
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<StructureMapping>>(json);
                    if (list != null)
                    {
                        foreach (var s in list) StructureList.Add(s);
                        return;
                    }
                }
            }
            catch { }

            foreach (var s in defaults) StructureList.Add(s);
            SaveStructures();
        }

        private void SaveStructures()
        {
            try
            {
                string path = AppConfig.StructuresPath;
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
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
