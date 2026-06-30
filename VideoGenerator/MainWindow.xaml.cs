using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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
            
            // Load initial view
            NavigateTo("Dashboard");
        }

        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavListBox.SelectedItem is ListBoxItem item && item.Tag is string viewName)
            {
                NavigateTo(viewName);
            }
        }

        private void NavigateTo(string viewName)
        {
            if (!_viewCache.ContainsKey(viewName))
            {
                UserControl nextView = viewName switch
                {
                    "Dashboard" => _serviceProvider.GetRequiredService<DashboardView>(),
                    "Background" => _serviceProvider.GetRequiredService<BackgroundDesignView>(),
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
            }
        }

        private void CancelProcessing_Click(object sender, RoutedEventArgs e)
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
            if (e.Key == System.Windows.Input.Key.Escape && Progress.IsBusy)
            {
                CancelActiveTask();
                e.Handled = true;
            }
            base.OnPreviewKeyDown(e);
        }
    }
}
