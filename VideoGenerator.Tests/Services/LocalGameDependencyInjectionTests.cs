using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using VideoGenerator.Services;
using Xunit;

namespace VideoGenerator.Tests.Services;

public sealed class LocalGameDependencyInjectionTests
{
    [Fact]
    public void AutomaticallyInjectedGameServicesShareSingletonDependencies()
    {
        string root = Directory.CreateTempSubdirectory("VG.GameDI.").FullName;
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<LogService>();
            services.AddSingleton(provider => new LeagueWadAssetSource(
                provider.GetRequiredService<LogService>(),
                root,
                Path.Combine(root, "icons"),
                () => null));
            services.AddSingleton<LeagueWadHashResolver>();
            services.AddSingleton(provider => new OfflineSkinCatalog(
                root, provider.GetRequiredService<LogService>()));
            services.AddSingleton(provider => new DataFetcher(
                provider.GetRequiredService<LogService>(), root));
            services.AddSingleton(provider => new AliasManager(
                provider.GetRequiredService<LogService>(),
                Path.Combine(root, "aliases.json")));
            services.AddSingleton(provider => new GroupManager(
                provider.GetRequiredService<LogService>(),
                Path.Combine(root, "groups.json")));
            services.AddSingleton<SkinlineManager>();
            services.AddSingleton<LocalGameIconManager>();
            services.AddSingleton<LocalWadCatalogBootstrapper>();
            services.AddSingleton<LocalGameDataRefreshService>();

            using var provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

            Assert.Same(provider.GetRequiredService<LeagueWadHashResolver>(),
                provider.GetRequiredService<LeagueWadHashResolver>());
            Assert.Same(provider.GetRequiredService<LocalGameIconManager>(),
                provider.GetRequiredService<LocalGameIconManager>());
            Assert.NotNull(provider.GetRequiredService<LocalGameDataRefreshService>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
