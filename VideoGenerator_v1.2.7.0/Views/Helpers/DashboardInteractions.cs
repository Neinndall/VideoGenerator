using System.Windows;
using System.Windows.Controls;

namespace VideoGenerator.Views
{
    /// <summary>
    /// Contains view-only interactions that do not belong to production workflows.
    /// </summary>
    public partial class DashboardView
    {
        private bool _isDashboardMaximized;
        private GridLength _previousColumnWidth = new(380);
        private GridLength _previousHeaderHeight = GridLength.Auto;
        private GridLength _previousConfigHeight = GridLength.Auto;
        private GridLength _previousLogsHeight = GridLength.Auto;
        private GridLength _previousQuickEditHeight = GridLength.Auto;
        private GridLength _previousProgressHeight = GridLength.Auto;
        private Thickness _previousPreviewMargin = new(12, 0, 0, 0);

        private void PreviewImage_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left ||
                _model.SelectedEvent == null ||
                _model.PreviewImageSource == null)
            {
                return;
            }

            _isDashboardMaximized = !_isDashboardMaximized;
            if (_isDashboardMaximized)
            {
                SavePreviewLayout();
                SetPreviewMaximizedLayout();
                return;
            }

            RestorePreviewLayout();
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            _model.SearchQuery = string.Empty;
        }

        private void SavePreviewLayout()
        {
            _previousColumnWidth = PipelineColumn.Width;
            _previousHeaderHeight = HeaderRow.Height;
            _previousConfigHeight = ConfigRow.Height;
            _previousLogsHeight = LogsRow.Height;
            _previousQuickEditHeight = QuickEditRow.Height;
            _previousProgressHeight = ProgressRow.Height;
            _previousPreviewMargin = PreviewContainerGrid.Margin;
        }

        private void SetPreviewMaximizedLayout()
        {
            PipelineColumn.MinWidth = 0;
            PipelineColumn.Width = new GridLength(0);
            SplitterColumn.Width = new GridLength(0);
            HeaderRow.Height = new GridLength(0);
            ConfigRow.Height = new GridLength(0);
            LogsRow.Height = new GridLength(0);
            QuickEditRow.Height = new GridLength(0);
            ProgressRow.Height = new GridLength(0);
            PreviewContainerGrid.Margin = new Thickness(0);
        }

        private void RestorePreviewLayout()
        {
            PipelineColumn.MinWidth = 250;
            PipelineColumn.Width = _previousColumnWidth.Value > 0 ? _previousColumnWidth : new GridLength(380);
            SplitterColumn.Width = GridLength.Auto;
            HeaderRow.Height = _previousHeaderHeight;
            ConfigRow.Height = _previousConfigHeight;
            LogsRow.Height = _previousLogsHeight;
            QuickEditRow.Height = _previousQuickEditHeight;
            ProgressRow.Height = _previousProgressHeight;
            PreviewContainerGrid.Margin = _previousPreviewMargin;
        }
    }
}
