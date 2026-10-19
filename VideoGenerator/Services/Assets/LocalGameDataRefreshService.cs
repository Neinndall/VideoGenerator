using System;
using System.Threading;
using System.Threading.Tasks;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Coordinates the current Riot installation snapshot, schema-compatible
    /// JSON caches and in-memory lookups. Running a fresh audio scan checks the
    /// installation once, rather than enumerating hundreds of WADs for each event.
    /// </summary>
    public sealed class LocalGameDataRefreshService
    {
        private readonly LeagueWadAssetSource _gameAssets;
        private readonly LocalWadCatalogBootstrapper _catalogBootstrapper;
        private readonly DataFetcher _dataFetcher;
        private readonly SkinlineManager _skinlines;
        private readonly LocalGameIconManager _icons;
        private readonly LeagueWadHashResolver _hashes;
        private readonly LogService _logger;
        private readonly SemaphoreSlim _refreshGate = new(1, 1);
        private bool _initialized;

        public LocalGameDataRefreshService(
            LeagueWadAssetSource gameAssets,
            LocalWadCatalogBootstrapper catalogBootstrapper,
            DataFetcher dataFetcher,
            SkinlineManager skinlines,
            LocalGameIconManager icons,
            LeagueWadHashResolver hashes,
            LogService logger)
        {
            _gameAssets = gameAssets;
            _catalogBootstrapper = catalogBootstrapper;
            _dataFetcher = dataFetcher;
            _skinlines = skinlines;
            _icons = icons;
            _hashes = hashes;
            _logger = logger;
        }

        /// <summary>
        /// Returns whether source data changed. On first call, imports official
        /// catalogs even if they were previously cached by an older release.
        /// User-authored configuration and custom mappings are not modified.
        /// </summary>
        public async Task<bool> RefreshAsync(CancellationToken token = default)
        {
            await _refreshGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                bool archivesChanged = await Task.Run(
                    () => _gameAssets.RefreshArchives(), token).ConfigureAwait(false);

                if (_initialized && !archivesChanged)
                    return false;

                int updated = await _catalogBootstrapper.PrepareAsync(token)
                    .ConfigureAwait(false);

                // The old in-memory lookups are not allowed to shadow newer JSON
                // from the installed WADs. Keep existing usable image files.
                if (archivesChanged || updated > 0 || !_initialized)
                {
                    _dataFetcher.InvalidateCaches();
                    _skinlines.InvalidateCatalog();
                    _hashes.InvalidateCachedMetadata();
                    _icons.RefreshCatalogs();
                }

                _initialized = true;
                if (archivesChanged || updated > 0)
                    _logger.LogInfo(
                        $"Riot game data refreshed locally: {(archivesChanged ? "WAD archives changed" : "same WAD snapshot")}, " +
                        $"{updated} official catalogs updated.");
                return archivesChanged || updated > 0;
            }
            finally
            {
                _refreshGate.Release();
            }
        }
    }
}
