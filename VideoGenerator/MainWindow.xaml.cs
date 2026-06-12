using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using VideoGenerator.Services;
using VideoGenerator.Utils;

namespace VideoGenerator.Views
{
    public partial class MainWindow : Window, System.ComponentModel.INotifyPropertyChanged
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly LogService _logService;
        private readonly Dictionary<string, UserControl> _viewCache = new();

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

        public string AppVersion => AssemblyVersion.Version;

        private string _engineStatusText = "STABLE - READY";
        public string EngineStatusText
        {
            get => _engineStatusText;
            set
            {
                if (_engineStatusText != value)
                {
                    _engineStatusText = value;
                    OnPropertyChanged(nameof(EngineStatusText));
                }
            }
        }

        private bool _isEngineBusy = false;
        public bool IsEngineBusy
        {
            get => _isEngineBusy;
            set
            {
                if (_isEngineBusy != value)
                {
                    _isEngineBusy = value;
                    OnPropertyChanged(nameof(IsEngineBusy));
                    OnPropertyChanged(nameof(EngineProgressBarValue));
                }
            }
        }

        public double EngineProgressBarValue => IsEngineBusy ? 100 : 0;

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

        private void SubscribeToDashboardModel(DashboardView dashboardView)
        {
            if (dashboardView?.DashboardModel != null)
            {
                dashboardView.DashboardModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(dashboardView.DashboardModel.IsProcessing))
                    {
                        UpdateEngineStatus(dashboardView.DashboardModel.IsProcessing);
                    }
                };
                // Initial update
                UpdateEngineStatus(dashboardView.DashboardModel.IsProcessing);
            }
        }

        private void UpdateEngineStatus(bool isProcessing)
        {
            if (isProcessing)
            {
                EngineStatusText = "PROCESSING MEDIA...";
                IsEngineBusy = true;
            }
            else
            {
                EngineStatusText = "STABLE - READY";
                IsEngineBusy = false;
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
                    if (nextView is DashboardView dv)
                    {
                        SubscribeToDashboardModel(dv);
                    }
                }
            }

            if (_viewCache.TryGetValue(viewName, out var view))
            {
                ContentArea.Content = view;
            }
        }
    }
}
