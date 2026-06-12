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
        private readonly LogService _logService;
        private readonly Dictionary<string, UserControl> _viewCache = new();

        public string AppVersion => AssemblyVersion.Version;

        public MainWindow(IServiceProvider serviceProvider, LogService logService)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            _logService = logService;
            
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
                    _viewCache[viewName] = nextView;
            }

            if (_viewCache.TryGetValue(viewName, out var view))
            {
                ContentArea.Content = view;
            }
        }
    }
}
