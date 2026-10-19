using System;
using System.Windows;
using System.Windows.Controls;

namespace VideoGenerator.Views.Shell
{
    public partial class GlobalStatusBar : UserControl
    {
        public event EventHandler CancelRequested;

        public GlobalStatusBar()
        {
            InitializeComponent();
        }

        private void CancelProcessing_Click(object sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
