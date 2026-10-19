using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAPICodePack.Dialogs;
using VideoGenerator.Services;
using VideoGenerator.Views.Dialogs;

namespace VideoGenerator.Views.Settings
{
    public partial class GeneralSettingsView : UserControl
    {
        private LeagueWadAssetSource _activeSource;
        private long _inspectionGeneration;
        public GeneralSettingsView()
        {
            InitializeComponent();
        }

        public void SetActiveGameSource(LeagueWadAssetSource source)
        {
            _activeSource = source;
            ShowCurrentInstallation();
            _ = VerifyLoadedInstallationAsync();
        }

        private AppSettings CurrentSettings => AppSettings.Instance;

        private static bool IsSameInstallation(string current, string configured)
        {
            if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(configured))
                return false;

            string currentRoot = LeagueGameInstallationLocator.FindInstallationRoot(current);
            string configuredRoot = LeagueGameInstallationLocator.FindInstallationRoot(configured);
            return currentRoot != null && configuredRoot != null &&
                   string.Equals(currentRoot, configuredRoot, StringComparison.OrdinalIgnoreCase);
        }

        private void ShowCurrentInstallation()
        {
            string active = _activeSource?.GameDirectory;
            string configured = CurrentSettings.LeagueGameDirectory;
            // Counts are populated only from a real Game + Plugins inspection,
            // never from guesses or the existence of a saved folder alone.
            bool hasActiveSource = _activeSource?.IsAvailable == true;
            GameArchiveCountText.Text = hasActiveSource ? "…" : "—";
            PluginArchiveCountText.Text = hasActiveSource ? "…" : "—";
            if (_activeSource?.IsAvailable == true)
            {
                ActiveGameDirectoryText.Text =
                    $"Currently loaded: {active}  ·  {_activeSource.WadFiles.Count} WAD archives";
                if (string.IsNullOrWhiteSpace(configured))
                {
                    SetStatus("Local archives detected automatically",
                        "A local source is loaded, but no installation folder has been selected in Settings. Use Browse to choose one.",
                        PackIconKind.InformationOutline, "AccentBrush");
                }
                else if (IsSameInstallation(active, configured))
                {
                    SetStatus("Checking saved installation",
                        "Verifying the Game and Plugins archives for the selected folder.",
                        PackIconKind.InformationOutline, "AccentBrush");
                }
                else
                {
                    SetStatus("Restart required",
                        "The saved folder differs from the installation currently loaded. Restart to activate the selected WAD archives.",
                        PackIconKind.Restart, "WarningBrush");
                }
            }
            else
            {
                ActiveGameDirectoryText.Text = "Currently loaded: no WAD archives";
                if (LeagueGameInstallationLocator.FindInstallationRoot(configured) != null)
                {
                    SetStatus("Restart required",
                        "An installation folder is saved, but its WAD archives are not loaded in this session. Restart to activate it.",
                        PackIconKind.Restart, "WarningBrush");
                }
                else
                {
                    SetStatus("Game installation not connected",
                        "Choose your League of Legends folder to enable local icon and splash extraction.",
                        PackIconKind.AlertCircleOutline, "WarningBrush");
                }
            }
        }

        private async Task VerifyLoadedInstallationAsync()
        {
            string selected = _activeSource?.GameDirectory;
            if (string.IsNullOrWhiteSpace(selected))
                return;

            long generation = Interlocked.Increment(ref _inspectionGeneration);
            try
            {
                GameInstallationScanResult scan =
                    await LeagueGameInstallationLocator.InspectAsync(selected);
                if (generation != Interlocked.Read(ref _inspectionGeneration))
                    return;

                GameArchiveCountText.Text = scan.GameWadCount.ToString();
                PluginArchiveCountText.Text = scan.PluginWadCount.ToString();
                if (!scan.IsValid || !IsSameInstallation(scan.Root, selected))
                {
                    SetStatus("Installation needs attention",
                        "This session is using an incomplete or partial game folder. Browse to the League installation root and restart.",
                        PackIconKind.AlertCircleOutline, "WarningBrush");
                }
                else if (string.IsNullOrWhiteSpace(CurrentSettings.LeagueGameDirectory))
                {
                    SetStatus("Local archives detected automatically",
                        $"Found {scan.GameWadCount} Game and {scan.PluginWadCount} Plugins archives, but no installation folder is selected in Settings.",
                        PackIconKind.InformationOutline, "AccentBrush");
                }
                else if (IsSameInstallation(selected, CurrentSettings.LeagueGameDirectory))
                {
                    SetStatus("Local archives detected",
                        $"Found {scan.GameWadCount} Game and {scan.PluginWadCount} Plugins archives. Individual assets are verified when read.",
                        PackIconKind.CheckCircleOutline, "SuccessBrush");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                if (generation == Interlocked.Read(ref _inspectionGeneration))
                    SetStatus("Unable to verify installation", "Check access to the selected game directory.",
                        PackIconKind.AlertCircleOutline, "WarningBrush");
            }
        }

        private void SelectMediaDirectory_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select Voice-over Source Directory",
                // No inferred default folder: only reopen a valid saved choice.
                InitialDirectory = Directory.Exists(CurrentSettings.MediaSourceDirectory)
                    ? CurrentSettings.MediaSourceDirectory
                    : null
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                CurrentSettings.MediaSourceDirectory = dialog.FileName;
                CurrentSettings.SaveSettings();
            }
        }

        private async void SelectLeagueGameDirectory_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select League of Legends Installation",
                // Explicit user choice only; do not preselect the detected Riot
                // installation or the application's executable directory.
                InitialDirectory = Directory.Exists(CurrentSettings.LeagueGameDirectory)
                    ? CurrentSettings.LeagueGameDirectory
                    : null
            };

            if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
                return;

            Interlocked.Increment(ref _inspectionGeneration);
            var browseButton = (Button)sender;
            browseButton.IsEnabled = false;
            SetStatus("Checking WAD archives", "Checking Game and Plugins for local Riot assets…",
                PackIconKind.DatabaseSearchOutline, "AccentBrush");
            try
            {
                var result = await LeagueGameInstallationLocator.InspectAsync(dialog.FileName);
                if (!result.IsValid)
                {
                    ShowCurrentInstallation();
                    ModernMessageBox.Show(
                        "Select the League of Legends installation folder containing Game and Plugins. " +
                        "Both game WADs and the rcp-be-lol-game-data archives must be available.",
                        "Incomplete Game Installation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                CurrentSettings.LeagueGameDirectory = result.Root;
                // Save immediately: a user can restart right after choosing a game folder.
                CurrentSettings.SaveSettings();
                ShowCurrentInstallation();
                if (!IsSameInstallation(result.Root, _activeSource?.GameDirectory))
                {
                    SetStatus("Restart required",
                        $"Found {result.GameWadCount} Game and {result.PluginWadCount} Plugins archives. Restart to activate them.",
                        PackIconKind.Restart, "WarningBrush");
                }
                else
                {
                    SetStatus("Local archives detected",
                        $"Found {result.GameWadCount} Game and {result.PluginWadCount} Plugins archives. Individual assets are verified when read.",
                        PackIconKind.CheckCircleOutline, "SuccessBrush");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or ArgumentException)
            {
                ShowCurrentInstallation();
                ModernMessageBox.Show("Cannot inspect the selected folder. Check that it is accessible.",
                    "Game Folder Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                browseButton.IsEnabled = true;
            }
        }

        private void SetStatus(string title, string message, PackIconKind icon, string brushKey)
        {
            GameStatusTitle.Text = title;
            GameStatusDescription.Text = message;
            GameStatusIcon.Kind = icon;
            GameStatusIcon.Foreground = (Brush)FindResource(brushKey);
        }

    }
}
