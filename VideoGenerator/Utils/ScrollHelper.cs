using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VideoGenerator.Utils
{
    public static class ScrollHelper
    {
        public static readonly DependencyProperty BubbleMouseWheelProperty =
            DependencyProperty.RegisterAttached(
                "BubbleMouseWheel",
                typeof(bool),
                typeof(ScrollHelper),
                new PropertyMetadata(false, OnBubbleMouseWheelChanged));

        public static bool GetBubbleMouseWheel(DependencyObject obj) => (bool)obj.GetValue(BubbleMouseWheelProperty);
        public static void SetBubbleMouseWheel(DependencyObject obj, bool value) => obj.SetValue(BubbleMouseWheelProperty, value);

        private static void OnBubbleMouseWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue)
                    element.PreviewMouseWheel += Element_PreviewMouseWheel;
                else
                    element.PreviewMouseWheel -= Element_PreviewMouseWheel;
            }
        }

        private static void Element_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                
                var parent = VisualTreeHelper.GetParent((DependencyObject)sender) as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }
    }
}
