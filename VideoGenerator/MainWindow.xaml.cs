using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using VideoGenerator.Services;
using VideoGenerator.Utils;

namespace VideoGenerator.Views
{
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TaskCancellationService _cancellationService;
        private readonly Dictionary<string, UserControl> _viewCache = new();
        private bool _sidebarCollapsed;
        private bool _sidebarAutoCollapsed;
        private bool _settingsWorkspaceActive;

        public string AppVersion => AssemblyVersion.Version;

        public ProgressService Progress { get; }

        public MainWindow(
            IServiceProvider serviceProvider,
            TaskCancellationService cancellationService,
            ProgressService progressService)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            _cancellationService = cancellationService;
            Progress = progressService;
            
            DataContext = this;

            // Settings is a full-height preferences workspace, like the reference
            // layout. Keep the global progress/cancel bar whenever work is active,
            // but do not reserve 46 DIP for an idle "ready" banner on Settings.
            Progress.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ProgressService.IsBusy))
                    RefreshStatusBarVisibility();
            };
            
            // Load initial view
            NavigateTo("Dashboard");
        }

        private void NavigationRail_NavigationRequested(object sender, string viewName)
        {
            NavigateTo(viewName);
        }

        public void NavigateTo(string viewName)
        {
            if (!_viewCache.ContainsKey(viewName))
            {
                System.Windows.Controls.UserControl nextView = viewName switch
                {
                    "Dashboard" => _serviceProvider.GetRequiredService<DashboardView>(),
                    "Background" => _serviceProvider.GetRequiredService<BackgroundDesignView>(),
                    "RenderVideo" => _serviceProvider.GetRequiredService<RenderVideoView>(),
                    "EventRules" => _serviceProvider.GetRequiredService<EventRulesView>(),
                    "Translations" => _serviceProvider.GetRequiredService<TranslationsView>(),
                    "Settings" => _serviceProvider.GetRequiredService<SettingsView>(),
                    _ => null
                };

                if (nextView != null)
                {
                    _viewCache[viewName] = nextView;
                }
            }

            if (_viewCache.TryGetValue(viewName, out var view))
            {
                ContentArea.Content = view;
                NavigationRail.SelectWorkspace(viewName);
                _settingsWorkspaceActive = string.Equals(viewName, "Settings", StringComparison.Ordinal);
                RefreshStatusBarVisibility();
                // The status rail remains live when navigating away from Dashboard.
                // It uses DashboardModel's selection/filter change notifications.
                if (view is DashboardView dashboardView)
                    WorkspaceCounters.DataContext = dashboardView.DashboardModel;
                ActiveWorkspaceTitle.Text = viewName switch
                {
                    "Dashboard" => "Production studio",
                    "Background" => "Visual design",
                    "RenderVideo" => "Render video",
                    "EventRules" => "Event mapping",
                    "Translations" => "Dictionary",
                    "Settings" => "Settings",
                    _ => "Workspace"
                };
            }
        }

        private void RefreshStatusBarVisibility()
        {
            if (Content is not Grid root || root.RowDefinitions.Count < 3 || SystemStatusRail == null)
                return;

            bool visible = !_settingsWorkspaceActive || Progress.IsBusy;
            root.RowDefinitions[2].Height = new GridLength(visible ? 46 : 0);
            SystemStatusRail.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            // The final Settings card row remains fully visible; other
            // workspaces retain their original 20-DIP bottom padding.
            if (WorkspaceContentSurface != null)
                WorkspaceContentSurface.Padding = _settingsWorkspaceActive
                    ? new Thickness(24, 20, 24, 4)
                    : new Thickness(24, 20, 24, 20);
        }

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _sidebarAutoCollapsed = false;
            _sidebarCollapsed = !_sidebarCollapsed;
            ApplySidebarVisibility();
        }

        // Keep the top-bar metrics and workspace inputs usable on smaller
        // desktop windows / increased DPI. Users can always toggle Ctrl+B.
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (NavigationRail == null || SidebarColumn == null)
                return;

            if (e.NewSize.Width < 1130 && !_sidebarCollapsed)
            {
                _sidebarCollapsed = true;
                _sidebarAutoCollapsed = true;
                ApplySidebarVisibility();
            }
            else if (e.NewSize.Width >= 1130 && _sidebarAutoCollapsed)
            {
                _sidebarCollapsed = false;
                _sidebarAutoCollapsed = false;
                ApplySidebarVisibility();
            }
        }

        private void ApplySidebarVisibility()
        {
            NavigationRail.Visibility = _sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
            SidebarColumn.Width = new GridLength(_sidebarCollapsed ? 0 : 260);
            SidebarToggleButton.ToolTip = _sidebarCollapsed
                ? "Show navigation sidebar (Ctrl+B)"
                : "Hide navigation sidebar (Ctrl+B)";
        }

        private void GlobalStatusBar_CancelRequested(object sender, EventArgs e)
        {
            CancelActiveTask();
        }

        public void CancelActiveTask()
        {
            _cancellationService.Cancel();
            Progress.SetStatus("CANCELING TASK...");
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.B)
                {
                    ToggleSidebar_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }

                string viewName = e.Key switch
                {
                    Key.D1 or Key.NumPad1 => "Dashboard",
                    Key.D2 or Key.NumPad2 => "Background",
                    Key.D3 or Key.NumPad3 => "EventRules",
                    Key.D4 or Key.NumPad4 => "Translations",
                    Key.D5 or Key.NumPad5 => "Settings",
                    Key.D6 or Key.NumPad6 => "RenderVideo",
                    _ => null
                };

                if (!e.Handled && viewName != null)
                {
                    NavigateTo(viewName);
                    e.Handled = true;
                }
                else if (e.Key == Key.F && ContentArea.Content is DashboardView dashboard)
                {
                    e.Handled = dashboard.FocusEventSearch();
                }
            }

            if (!e.Handled && e.Key == Key.Escape && Progress.IsBusy)
            {
                CancelActiveTask();
                e.Handled = true;
            }
            base.OnPreviewKeyDown(e);
        }
    }
}
