using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace VideoGenerator.Views
{
    public partial class ModernMessageBoxWindow : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;
        public string HeaderTitle => HeaderTitleTextBlock?.Text ?? string.Empty;
        public string Caption => CaptionTextBlock?.Text ?? string.Empty;
        public string Message => MessageTextBlock?.Text ?? string.Empty;
        public int ButtonCount => ButtonsPanel?.Children.Count ?? 0;

        public ModernMessageBoxWindow(
            string message,
            string caption = "NOTIFICATION",
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.Information,
            string primaryButtonText = null,
            string secondaryButtonText = null,
            string tertiaryButtonText = null)
        {
            InitializeComponent();

            HeaderTitleTextBlock.Text = (caption ?? "NOTIFICATION").ToUpperInvariant();
            CaptionTextBlock.Text = caption ?? "Notice";
            MessageTextBlock.Text = message ?? string.Empty;

            ConfigureIconAndBadge(icon);
            ConfigureButtons(buttons, primaryButtonText, secondaryButtonText, tertiaryButtonText);

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    CloseWithResult(GetDefaultCancelResult(buttons));
                }
            };
        }

        private void ConfigureIconAndBadge(MessageBoxImage icon)
        {
            var accentBrush = (Brush)TryFindResource("AccentBrush") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B5CF6"));
            var successBrush = (Brush)TryFindResource("SuccessBrush") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            var warningBrush = (Brush)TryFindResource("WarningBrush") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            var errorBrush = (Brush)TryFindResource("ErrorBrush") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            var hextechGoldBrush = (Brush)TryFindResource("HextechGoldBrush") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C89B3C"));

            switch (icon)
            {
                case MessageBoxImage.Information:
                    DialogBadgeIcon.Kind = PackIconKind.InformationOutline;
                    DialogBadgeIcon.Foreground = accentBrush;
                    HeaderIcon.Kind = PackIconKind.InformationOutline;
                    HeaderIcon.Foreground = accentBrush;
                    IconBadgeBorder.BorderBrush = accentBrush;
                    break;

                case MessageBoxImage.Question:
                    DialogBadgeIcon.Kind = PackIconKind.HelpCircleOutline;
                    DialogBadgeIcon.Foreground = hextechGoldBrush;
                    HeaderIcon.Kind = PackIconKind.HelpCircleOutline;
                    HeaderIcon.Foreground = hextechGoldBrush;
                    IconBadgeBorder.BorderBrush = hextechGoldBrush;
                    break;

                case MessageBoxImage.Warning:
                    DialogBadgeIcon.Kind = PackIconKind.AlertOutline;
                    DialogBadgeIcon.Foreground = warningBrush;
                    HeaderIcon.Kind = PackIconKind.AlertOutline;
                    HeaderIcon.Foreground = warningBrush;
                    IconBadgeBorder.BorderBrush = warningBrush;
                    break;

                case MessageBoxImage.Error:
                    DialogBadgeIcon.Kind = PackIconKind.CloseCircleOutline;
                    DialogBadgeIcon.Foreground = errorBrush;
                    HeaderIcon.Kind = PackIconKind.AlertCircleOutline;
                    HeaderIcon.Foreground = errorBrush;
                    IconBadgeBorder.BorderBrush = errorBrush;
                    break;

                default:
                    DialogBadgeIcon.Kind = PackIconKind.BellOutline;
                    DialogBadgeIcon.Foreground = accentBrush;
                    HeaderIcon.Kind = PackIconKind.BellOutline;
                    HeaderIcon.Foreground = accentBrush;
                    IconBadgeBorder.BorderBrush = accentBrush;
                    break;
            }
        }

        private void ConfigureButtons(
            MessageBoxButton buttons,
            string primaryText,
            string secondaryText,
            string tertiaryText)
        {
            ButtonsPanel.Children.Clear();
            var primaryStyle = (Style)TryFindResource("ModernPrimaryButton");
            var secondaryStyle = (Style)TryFindResource("ModernSecondaryButton");

            switch (buttons)
            {
                case MessageBoxButton.OK:
                    var okBtn = CreateButton(primaryText ?? "OK", primaryStyle, MessageBoxResult.OK, isDefault: true);
                    ButtonsPanel.Children.Add(okBtn);
                    break;

                case MessageBoxButton.OKCancel:
                    var cancelBtn1 = CreateButton(secondaryText ?? "CANCEL", secondaryStyle, MessageBoxResult.Cancel, margin: new Thickness(0, 0, 8, 0), isCancel: true);
                    var okBtn2 = CreateButton(primaryText ?? "OK", primaryStyle, MessageBoxResult.OK, isDefault: true);
                    ButtonsPanel.Children.Add(cancelBtn1);
                    ButtonsPanel.Children.Add(okBtn2);
                    break;

                case MessageBoxButton.YesNo:
                    var noBtn = CreateButton(secondaryText ?? "NO", secondaryStyle, MessageBoxResult.No, margin: new Thickness(0, 0, 8, 0), isCancel: true);
                    var yesBtn = CreateButton(primaryText ?? "YES", primaryStyle, MessageBoxResult.Yes, isDefault: true);
                    ButtonsPanel.Children.Add(noBtn);
                    ButtonsPanel.Children.Add(yesBtn);
                    break;

                case MessageBoxButton.YesNoCancel:
                    var cancelBtn3 = CreateButton(tertiaryText ?? "CANCEL", secondaryStyle, MessageBoxResult.Cancel, margin: new Thickness(0, 0, 8, 0), isCancel: true);
                    var noBtn3 = CreateButton(secondaryText ?? "NO", secondaryStyle, MessageBoxResult.No, margin: new Thickness(0, 0, 8, 0));
                    var yesBtn3 = CreateButton(primaryText ?? "YES", primaryStyle, MessageBoxResult.Yes, isDefault: true);
                    ButtonsPanel.Children.Add(cancelBtn3);
                    ButtonsPanel.Children.Add(noBtn3);
                    ButtonsPanel.Children.Add(yesBtn3);
                    break;
            }
        }

        private Button CreateButton(
            string text,
            Style style,
            MessageBoxResult result,
            Thickness margin = default,
            bool isDefault = false,
            bool isCancel = false)
        {
            var btn = new Button
            {
                Content = text,
                Style = style,
                Height = 36,
                Padding = new Thickness(18, 0, 18, 0),
                Margin = margin,
                IsDefault = isDefault,
                IsCancel = isCancel,
                FontSize = 10,
                FontWeight = FontWeights.Black
            };
            btn.Click += (s, e) => CloseWithResult(result);
            return btn;
        }

        private void CloseWithResult(MessageBoxResult result)
        {
            Result = result;
            DialogResult = result == MessageBoxResult.OK || result == MessageBoxResult.Yes;
            Close();
        }

        private static MessageBoxResult GetDefaultCancelResult(MessageBoxButton buttons)
        {
            return buttons switch
            {
                MessageBoxButton.OK => MessageBoxResult.OK,
                MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
                MessageBoxButton.YesNo => MessageBoxResult.No,
                MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
                _ => MessageBoxResult.None
            };
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWithResult(MessageBoxResult.Cancel);
        }
    }

    public static class ModernMessageBox
    {
        public static MessageBoxResult Show(
            string message,
            string caption = "Notice",
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.Information,
            Window owner = null)
        {
            return ShowCustom(
                message,
                caption,
                buttons,
                icon,
                primaryButtonText: null,
                secondaryButtonText: null,
                tertiaryButtonText: null,
                owner: owner);
        }

        public static MessageBoxResult ShowCustom(
            string message,
            string caption = "Notice",
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.Information,
            string primaryButtonText = null,
            string secondaryButtonText = null,
            string tertiaryButtonText = null,
            Window owner = null)
        {
            if (owner == null)
            {
                owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
                     ?? Application.Current?.MainWindow;
            }

            var dialog = new ModernMessageBoxWindow(
                message,
                caption,
                buttons,
                icon,
                primaryButtonText,
                secondaryButtonText,
                tertiaryButtonText);

            if (owner != null && owner.IsVisible)
            {
                dialog.Owner = owner;
            }

            dialog.ShowDialog();
            return dialog.Result;
        }
    }
}
