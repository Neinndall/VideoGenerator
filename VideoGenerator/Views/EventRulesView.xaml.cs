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
        }

        public RuleManager RuleManager => _ruleManager;
        public GroupManager GroupManager => _groupManager;
        public AliasManager AliasManager => _aliasManager;

        // --- Event Rules Logic ---

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            string keyword = KeywordBox.Text.Trim();
            string translationKey = TranslationKeyBox.Text.Trim();
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

            _ruleManager.Rules.Insert(0, new EventRule
            {
                Keyword = keyword,
                TranslationKey = translationKey,
                IconType = iconType,
                Type = ruleType,
                ExtractsTarget = ruleType != RuleType.Simple
            });

            _ruleManager.SaveRules();

            KeywordBox.Text = "";
            TranslationKeyBox.Text = "";
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

            _groupManager.Groups.Insert(0, newGroup);
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

            if (string.IsNullOrEmpty(display) || string.IsNullOrEmpty(internalName))
            {
                MessageBox.Show("Both Display Name and Internal Name are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Duplicate Prevention
            if (_aliasManager.Aliases.Any(a => a.InternalName.Equals(internalName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"An alias with the Internal Name '{internalName}' already exists.", "Duplicate Detected", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _aliasManager.Aliases.Insert(0, new ChampionAlias
            {
                DisplayName = display,
                InternalName = internalName,
                IsOfficial = false
            });
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
    }
}
