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

        private double _engineProgressBarValue = 0;
        public double EngineProgressBarValue
        {
            get => _engineProgressBarValue;
            set
            {
                if (_engineProgressBarValue != value)
                {
                    _engineProgressBarValue = value;
                    OnPropertyChanged(nameof(EngineProgressBarValue));
                }
            }
        }

        private bool _isEngineIndeterminate = true;
        public bool IsEngineIndeterminate
        {
            get => _isEngineIndeterminate;
            set
            {
                if (_isEngineIndeterminate != value)
                {
                    _isEngineIndeterminate = value;
                    OnPropertyChanged(nameof(IsEngineIndeterminate));
                }
            }
        }

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
                    else if (e.PropertyName == nameof(dashboardView.DashboardModel.ProgressValue))
                    {
                        EngineProgressBarValue = dashboardView.DashboardModel.ProgressValue;
                        IsEngineIndeterminate = EngineProgressBarValue <= 0;
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
                EngineProgressBarValue = 0;
                IsEngineIndeterminate = false;
            }
            else
            {
                EngineStatusText = "STABLE - READY";
                IsEngineBusy = false;
                EngineProgressBarValue = 0;
                IsEngineIndeterminate = true;
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
