using System;
using System.Windows;
using System.Windows.Controls;

namespace VideoGenerator.Views.Shell
{
    public partial class SidebarView : UserControl
    {
        public event EventHandler<string> NavigationRequested;

        public SidebarView()
        {
            InitializeComponent();
        }

        public void SelectWorkspace(string viewName)
        {
            foreach (object item in NavListBox.Items)
            {
                if (item is ListBoxItem navItem &&
                    string.Equals(navItem.Tag as string, viewName, StringComparison.OrdinalIgnoreCase))
                {
                    if (!ReferenceEquals(NavListBox.SelectedItem, navItem))
                    {
                        _isSynchronizingSelection = true;
                        try { NavListBox.SelectedItem = navItem; }
                        finally { _isSynchronizingSelection = false; }
                    }

                    break;
                }
            }
        }

        private bool _isSynchronizingSelection;

        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isSynchronizingSelection && NavListBox.SelectedItem is ListBoxItem item &&
                item.Tag is string viewName)
            {
                NavigationRequested?.Invoke(this, viewName);
            }
        }
    }
}
