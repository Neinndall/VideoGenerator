using System.Windows;
using System.IO;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VideoGenerator.Models;
using VideoGenerator.Services;
using VideoGenerator.Utils;
using VideoGenerator.Views;
using VideoGenerator.Views.Dialogs;

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

            // Initialize WPF before starting background work so services can safely
            // resolve the application dispatcher when they publish UI notifications.
            base.OnStartup(e);

            // 3. Auto-Sync Databases in background (Fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    var dbBuilder = ServiceProvider.GetRequiredService<DatabaseBuilder>();
                    var dataFetcher = ServiceProvider.GetRequiredService<DataFetcher>();
                    string version = await dataFetcher.GetLatestLolVersionAsync();
                    await dbBuilder.InitializeDatabasesAsync(version);
                    ServiceProvider.GetRequiredService<LogService>()
                        .LogInfo("Database synchronization finished. Existing local caches remain available when a source could not be refreshed.");
                }
                catch (Exception ex)
                {
                    ServiceProvider.GetRequiredService<LogService>()
                        .LogError("Failed to synchronize local databases", ex);
                }
            });

            // 4. Show Main Window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            mainWindow.Activate();
            mainWindow.Focus();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // --- Core Services (Singletons) ---
            services.AddSingleton(_ => AppSettings.Instance);
            services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(15) });
            services.AddSingleton<LogService>();
            services.AddSingleton<DatabaseBuilder>();
            services.AddSingleton<DataFetcher>();
            services.AddSingleton<TranslationService>();
            services.AddSingleton<RuleManager>();
            services.AddSingleton<SkinlineManager>();
            services.AddSingleton<GroupManager>();
            services.AddSingleton<AliasManager>();
            services.AddSingleton<IconManager>();
            services.AddSingleton<IEventNameParser, EventNameParser>();
            services.AddSingleton<ImageGenerator>();
            services.AddSingleton<VideoService>();
            services.AddSingleton<TranscriptionService>();
            services.AddSingleton<DialogueService>();
            services.AddSingleton<IDialogueStore>(provider => provider.GetRequiredService<DialogueService>());
            services.AddSingleton<TaskCancellationService>();
            services.AddSingleton<ProgressService>();
            services.AddSingleton<EventFilterService>();
            services.AddSingleton<AudioFolderDiscoveryService>();
            services.AddSingleton<EventAnalysisService>();
            services.AddSingleton<EventIconResolutionService>();
            services.AddSingleton<PreviewImageService>();
            services.AddSingleton<AudioFamilyMergeService>();
            services.AddSingleton<HudImagePreparationService>();
            services.AddSingleton<ProductionWorkPlanningService>();

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
                string logPath = AppConfig.ApplicationErrorsPath;
                DirectoriesCreator.CreateParentDirectory(logPath);
                File.AppendAllText(logPath, $"[CRITICAL] {DateTime.Now}: {ex.Message}\n{ex.StackTrace}\n\n");
            }
            
            try
            {
                ModernMessageBox.Show($"Critical error: {ex.Message}\n\nPlease check logs/application_errors.log for more details.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                System.Windows.MessageBox.Show($"Critical error: {ex.Message}\n\nPlease check logs/application_errors.log for more details.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
