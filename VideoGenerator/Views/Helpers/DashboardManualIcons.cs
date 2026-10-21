using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using VideoGenerator.Services;
using VideoGenerator.Utils;
using VideoGenerator.Views.Dialogs;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Views
{
    /// <summary>
    /// On-hover Dashboard icon editor. The overlay is WPF-only: it is never
    /// included in the generated HUD images or video.
    /// </summary>
    public partial class DashboardView
    {
        private readonly ManualEventIconStore _manualIcons = new();

        private string GetManualIconPath(PreviewEventModel ev)
        {
            if (ev == null || string.IsNullOrWhiteSpace(ev.FolderPath)) return null;
            try { return _manualIcons.GetIconPath(ev.FolderPath); }
            catch (Exception ex)
            {
                _logger.LogWarn($"Could not read manual icon settings: {ex.Message}");
                return null;
            }
        }

        private void RestoreSavedIcon(PreviewEventModel ev)
        {
            string path = GetManualIconPath(ev);
            if (path == null || ev?.ParsedData == null) return;
            ev.ParsedData.IconPath = path;
            ev.UpdateStatusAfterIconResolution(path);
        }

        private void UpdateManualIconSlot()
        {
            if (PreviewIconAction == null) return;

            bool rightAligned = string.Equals(AppSettings.Instance.IconAlignment,
                "Right", StringComparison.OrdinalIgnoreCase);
            int x = rightAligned
                ? HudTextures.CanvasWidth - HudTextures.IconSize - HudTextures.IconX
                : HudTextures.IconX;
            int y = (int)(HudTextures.IconY + AppSettings.Instance.IconVerticalOffset);
            Canvas.SetLeft(PreviewIconAction, Math.Clamp(x - 16, 0, HudTextures.CanvasWidth - 212));
            Canvas.SetTop(PreviewIconAction, Math.Clamp(y - 12, 0, HudTextures.CanvasHeight - 208));

            var ev = _model.SelectedEvent;
            bool hasIcon = !string.IsNullOrWhiteSpace(ev?.ParsedData?.IconPath) &&
                           File.Exists(ev.ParsedData.IconPath);
            bool isManual = GetManualIconPath(ev) != null;
            PreviewIconActionText.Text = hasIcon ? "CHANGE ICON" : "ADD ICON";
            PreviewIconActionGlyph.Kind = hasIcon ? PackIconKind.PencilOutline : PackIconKind.ImagePlus;
            RestoreRiotIconMenuItem.IsEnabled = isManual;
            PreviewIconAction.IsEnabled = ev?.ParsedData != null && !_model.IsProcessing;
        }

        private async void PreviewIconAction_Click(object sender, RoutedEventArgs e)
        {
            var ev = _model.SelectedEvent;
            if (ev?.ParsedData == null || _model.IsProcessing) return;

            var dialog = new OpenFileDialog
            {
                Title = "Add or change event icon",
                Filter = "Images (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog() != true)
            {
                Keyboard.ClearFocus();
                return;
            }
            PreviewIconAction.IsEnabled = false;
            try
            {
                string path = await _manualIcons.ImportAsync(ev.FolderPath, dialog.FileName);
                ev.ParsedData.IconPath = path;
                ev.UpdateStatusAfterIconResolution(path);
                ev.MarkImagesDirty();
                _logger.LogInfo($"Manual icon applied to event '{ev.FolderName}'.");
                if (ReferenceEquals(_model.SelectedEvent, ev))
                    await UpdatePreviewAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to import manual icon for '{ev.FolderName}'", ex);
                ModernMessageBox.Show($"Unable to import this image: {ex.Message}",
                    "Add Event Icon", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Keyboard.ClearFocus();
                UpdateManualIconSlot();
            }
        }

        private async void RestoreRiotIcon_Click(object sender, RoutedEventArgs e)
        {
            var ev = _model.SelectedEvent;
            if (ev?.ParsedData == null || _model.IsProcessing) return;
            try
            {
                _manualIcons.RemoveOverride(ev.FolderPath);
                ev.ParsedData.IconPath = null;
                string icon = await _eventIconResolutionService.ResolveAsync(ev.ParsedData);
                ev.ParsedData.IconPath = icon;
                ev.UpdateStatusAfterIconResolution(icon);
                ev.MarkImagesDirty();
                _logger.LogInfo($"Restored automatic WAD icon selection for '{ev.FolderName}'.");
                if (ReferenceEquals(_model.SelectedEvent, ev))
                    await UpdatePreviewAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to restore automatic icon", ex);
                ModernMessageBox.Show($"Unable to restore the Riot icon: {ex.Message}",
                    "Restore Event Icon", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Keyboard.ClearFocus();
                UpdateManualIconSlot();
            }
        }
    }
}
