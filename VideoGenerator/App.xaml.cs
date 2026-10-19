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

            // 3. Prepare locally installed and cached game data in background.
            // An absent Riot installation must not block the production workspace.
            _ = Task.Run(async () =>
            {
                try
                {
                    var dbBuilder = ServiceProvider.GetRequiredService<DatabaseBuilder>();
                    try
                    {
                        // A fresh snapshot is imported once per installation patch.
                        await ServiceProvider.GetRequiredService<LocalGameDataRefreshService>()
                            .RefreshAsync();
                    }
                    catch (Exception ex)
                    {
                        ServiceProvider.GetRequiredService<LogService>()
                            .LogWarn($"Could not prepare all local WAD catalogs: {ex.Message}");
                    }
                    await dbBuilder.InitializeDatabasesAsync("local");
                    ServiceProvider.GetRequiredService<LogService>()
                        .LogInfo("Local League of Legends catalogs are ready. Existing settings and offline caches were preserved.");
                }
                catch (Exception ex)
                {
                    ServiceProvider.GetRequiredService<LogService>()
                        .LogError("Failed to initialize local game catalogs", ex);
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
            // Whisper's optional model downloader still needs HttpClient.
            // Removing external WAD hash catalogs must not break transcription.
            services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(15) });
            services.AddSingleton<LogService>();
            services.AddSingleton<DatabaseBuilder>();
            services.AddSingleton<DataFetcher>();
            services.AddSingleton<TranslationService>();
            services.AddSingleton<RuleManager>();
            services.AddSingleton<SkinlineManager>();
            services.AddSingleton<GroupManager>();
            services.AddSingleton<AliasManager>();
            services.AddSingleton(provider => new LeagueWadAssetSource(
                provider.GetRequiredService<LogService>(),
                provider.GetRequiredService<AppSettings>().LeagueGameDirectory));
            services.AddSingleton(provider => new LeagueWadHashResolver(
                provider.GetRequiredService<LeagueWadAssetSource>()));
            services.AddSingleton(provider => new OfflineSkinCatalog(logger:
                provider.GetRequiredService<LogService>()));
            services.AddSingleton<LocalWadCatalogBootstrapper>();
            services.AddSingleton(provider => new LocalGameIconManager(
                provider.GetRequiredService<LeagueWadAssetSource>(),
                provider.GetRequiredService<DataFetcher>(),
                provider.GetRequiredService<AliasManager>(),
                provider.GetRequiredService<GroupManager>(),
                provider.GetRequiredService<SkinlineManager>(),
                provider.GetRequiredService<LogService>(),
                hashes: provider.GetRequiredService<LeagueWadHashResolver>(),
                offlineSkins: provider.GetRequiredService<OfflineSkinCatalog>()));
            services.AddSingleton<LocalGameDataRefreshService>();
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
