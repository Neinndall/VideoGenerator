using System.Windows;
using System.IO;
using System;
using System.Reflection;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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

            // 3. Auto-Sync Databases in background (Fire and forget)
            Task.Run(async () => 
            {
                try 
                {
                    var dbBuilder = ServiceProvider.GetRequiredService<DatabaseBuilder>();
                    var dataFetcher = ServiceProvider.GetRequiredService<DataFetcher>();
                    string version = await dataFetcher.GetLatestLolVersionAsync();
                    await dbBuilder.InitializeDatabasesAsync(version);
                    var logger = ServiceProvider.GetService<LogService>();
                    logger?.LogInfo("Local databases synchronized successfully.");
                }
                catch (Exception ex)
                {
                    var logger = ServiceProvider.GetService<LogService>();
                    logger?.LogError("Failed to synchronize local databases", ex);
                }
            });

            // 4. WPF Startup
            base.OnStartup(e);

            // 5. Show Main Window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            mainWindow.Activate();
            mainWindow.Focus();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // --- Core Services (Singletons) ---
            services.AddSingleton<HttpClient>();
            services.AddSingleton<LogService>();
            services.AddSingleton<DatabaseBuilder>();
            services.AddSingleton<DataFetcher>();
            services.AddSingleton<TranslationService>();
            services.AddSingleton<RuleManager>();
            services.AddSingleton<SkinlineManager>();
            services.AddSingleton<GroupManager>();
            services.AddSingleton<AliasManager>();
            services.AddSingleton<IconManager>();
            services.AddSingleton<NameParser>();
            services.AddSingleton<ImageGenerator>();
            services.AddSingleton<VideoService>();
            services.AddSingleton<TranscriptionService>();
            services.AddSingleton<DialogueService>();

            // --- Views (Singletons for state preservation) ---
            services.AddSingleton<MainWindow>();
            services.AddSingleton<DashboardView>();
            services.AddSingleton<BackgroundDesignView>();
            services.AddSingleton<EventRulesView>(provider => new EventRulesView(
                provider.GetRequiredService<RuleManager>(),
                provider.GetRequiredService<GroupManager>(),
                provider.GetRequiredService<AliasManager>(),
                provider.GetRequiredService<TranslationService>()
            ));
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
