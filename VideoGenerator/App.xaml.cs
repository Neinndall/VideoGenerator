using System.Windows;
using System.IO;
using System;
using System.Reflection;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using VideoGenerator.Services;
using VideoGenerator.Views;

namespace VideoGenerator
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. Global Exception Handling
            AppDomain.CurrentDomain.UnhandledException += (s, ev) => LogException(ev.ExceptionObject as Exception);
            DispatcherUnhandledException += (s, ev) => { LogException(ev.Exception); ev.Handled = true; };
            
            // 2. Dependency Injection Configuration
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // 3. WPF Startup
            base.OnStartup(e);

            // 4. Show Main Window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            mainWindow.Activate();
            mainWindow.Focus();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // --- Core Services (Singletons) ---
            services.AddSingleton<LogService>();
            services.AddSingleton<DataFetcher>();
            services.AddSingleton<TranslationService>();
            services.AddSingleton<RuleManager>();
            services.AddSingleton<GroupManager>();
            services.AddSingleton<AliasManager>();
            services.AddSingleton<IconManager>();
            services.AddSingleton<NameParser>();
            services.AddSingleton<ImageGenerator>();
            services.AddSingleton<VideoService>();

            // --- Views (Singletons for state preservation) ---
            services.AddSingleton<MainWindow>();
            services.AddSingleton<DashboardView>();
            services.AddSingleton<BackgroundDesignView>();
            services.AddSingleton<EventRulesView>();
            services.AddSingleton<TranslationsView>();
            services.AddSingleton<SettingsView>();
        }

        private void LogException(Exception ex)
        {
            if (ex == null) return;
            
            var logger = ServiceProvider?.GetService<LogService>();
            if (logger != null)
            {
                logger.LogError("Critical Unhandled Exception", ex);
            }
            else
            {
                // Fallback if DI is not ready
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_errors.log");
                File.AppendAllText(logPath, $"[CRITICAL] {DateTime.Now}: {ex.Message}\n{ex.StackTrace}\n\n");
            }
            
            System.Windows.MessageBox.Show($"Critical error: {ex.Message}\n\nPlease check app_errors.log for more details.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
