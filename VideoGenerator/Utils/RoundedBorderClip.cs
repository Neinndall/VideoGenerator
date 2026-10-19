using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VideoGenerator.Utils
{
    /// <summary>
    /// Border.CornerRadius rounds its stroke, but child content may still paint
    /// square corners over it. Clip the CHILD to the inner rounded contour,
    /// not the Border itself: clipping the Border cuts off the outer stroke at
    /// the bottom corners and leaves an apparently disconnected horizontal line.
    /// </summary>
    public static class RoundedBorderClip
    {
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled", typeof(bool), typeof(RoundedBorderClip),
                new PropertyMetadata(false, OnEnabledChanged));

        public static bool GetEnabled(DependencyObject element) =>
            (bool)element.GetValue(EnabledProperty);

        public static void SetEnabled(DependencyObject element, bool value) =>
            element.SetValue(EnabledProperty, value);

        private static void OnEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (element is not Border border)
                return;

            if ((bool)e.NewValue)
            {
                border.SizeChanged += OnSizeChanged;
                border.Loaded += OnLoaded;
                AttachChild(border);
            }
            else
            {
                border.SizeChanged -= OnSizeChanged;
                border.Loaded -= OnLoaded;
                DetachChild(border);
                border.ClearValue(UIElement.ClipProperty);
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e) =>
            AttachChild((Border)sender);

        private static void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
            UpdateClip((Border)sender);

        private static void OnChildSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is FrameworkElement child &&
                VisualTreeHelper.GetParent(child) is Border border && GetEnabled(border))
                UpdateClip(border);
        }

        private static void AttachChild(Border border)
        {
            // A previous implementation clipped the frame itself, which hid
            // its corner strokes. Keep the frame completely un-clipped.
            border.ClearValue(UIElement.ClipProperty);
            if (border.Child is FrameworkElement child)
            {
                child.SizeChanged -= OnChildSizeChanged;
                child.SizeChanged += OnChildSizeChanged;
            }
            UpdateClip(border);
        }

        private static void DetachChild(Border border)
        {
            if (border.Child is FrameworkElement child)
            {
                child.SizeChanged -= OnChildSizeChanged;
                child.ClearValue(UIElement.ClipProperty);
            }
        }

        private static void UpdateClip(Border border)
        {
            if (border.Child is not FrameworkElement child ||
                child.ActualWidth <= 0 || child.ActualHeight <= 0)
                return;

            // Child render size is already inset by BorderThickness / Padding.
            // Subtract the frame thickness so its stroke remains visible.
            var thickness = border.BorderThickness;
            double radius = Math.Max(0, border.CornerRadius.TopLeft -
                Math.Max(thickness.Left, thickness.Top));
            radius = Math.Min(radius, Math.Min(child.ActualWidth, child.ActualHeight) / 2);

            child.Clip = new RectangleGeometry(
                new Rect(0, 0, child.ActualWidth, child.ActualHeight), radius, radius);
        }
    }
}
