using System.Windows;
using System.Windows.Controls;

namespace VideoGenerator.Utils
{
    public static class TextBoxBehavior
    {
        public static readonly DependencyProperty ClearOnButtonClickProperty =
            DependencyProperty.RegisterAttached(
                "ClearOnButtonClick",
                typeof(bool),
                typeof(TextBoxBehavior),
                new PropertyMetadata(false, OnClearOnButtonClickChanged));

        public static bool GetClearOnButtonClick(DependencyObject obj) => (bool)obj.GetValue(ClearOnButtonClickProperty);
        public static void SetClearOnButtonClick(DependencyObject obj, bool value) => obj.SetValue(ClearOnButtonClickProperty, value);

        private static void OnClearOnButtonClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                textBox.Loaded += (s, ev) =>
                {
                    var clearBtn = textBox.Template.FindName("ClearBtn", textBox) as Button;
                    if (clearBtn != null)
                    {
                        clearBtn.Click += (btnSender, btnE) =>
                        {
                            textBox.Text = string.Empty;
                            // Also update binding if applicable
                            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                            binding?.UpdateSource();
                        };
                    }
                };
            }
        }
    }
}
