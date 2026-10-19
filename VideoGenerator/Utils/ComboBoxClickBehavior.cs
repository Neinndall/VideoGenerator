using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VideoGenerator.Utils
{
    /// <summary>
    /// Handles the full closed-field hit area of the navy ComboBox.
    /// The previous template used an inner ToggleButton with a two-way
    /// IsChecked binding, competing with ComboBox's own mouse handling.
    /// One preview event now makes exactly one open/close state change.
    /// Keyboard interaction and selection still belong to ComboBox.
    /// </summary>
    public static class ComboBoxClickBehavior
    {
        public static readonly DependencyProperty HandlesClickProperty =
            DependencyProperty.RegisterAttached(
                "HandlesClick", typeof(bool), typeof(ComboBoxClickBehavior),
                new PropertyMetadata(false, OnHandlesClickChanged));

        public static bool GetHandlesClick(DependencyObject element) =>
            (bool)element.GetValue(HandlesClickProperty);

        public static void SetHandlesClick(DependencyObject element, bool value) =>
            element.SetValue(HandlesClickProperty, value);

        private static void OnHandlesClickChanged(
            DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (element is not FrameworkElement surface)
                return;

            surface.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            if (e.NewValue is true)
                surface.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        }

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled || sender is not FrameworkElement surface)
                return;

            // This element exists only over the CLOSED field, never over the
            // popup's options. Do not intercept mouse selection in the list.
            DependencyObject current = surface;
            while (current != null && current is not ComboBox)
                current = VisualTreeHelper.GetParent(current);

            if (current is not ComboBox combo || !combo.IsEnabled)
                return;

            combo.Focus();
            combo.IsDropDownOpen = !combo.IsDropDownOpen;
            e.Handled = true;
        }
    }
}
