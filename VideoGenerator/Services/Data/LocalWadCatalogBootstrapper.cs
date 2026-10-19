using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Refreshes Riot's official JSON caches from the installed game-data WAD.
    /// Config files are user-owned; a generated champions list is refreshed only
    /// while its content still matches the list this bootstrapper previously wrote.
    /// </summary>
    public sealed class LocalWadCatalogBootstrapper
    {
        private const string ChampionSummaryPath =
            "plugins/rcp-be-lol-game-data/global/default/v1/champion-summary.json";
        private const int ProvenanceVersion = 1;
        private static readonly SemaphoreSlim PreparationGate = new(1, 1);
        private readonly LeagueWadAssetSource _source;
        private readonly LogService _logger;
        private readonly string _storageRoot;

        public LocalWadCatalogBootstrapper(LeagueWadAssetSource source, LogService logger,
            string storageRoot = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storageRoot = storageRoot;
        }

        /// <summary>
        /// Returns the number of actual JSON files created or refreshed. This only
        /// reads/hashes the WAD during explicit preparation, never per icon/event.
        /// </summary>
        public async Task<int> PrepareAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            await PreparationGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!_source.IsAvailable)
                {
                    _logger.LogWarn("League of Legends game archives are unavailable. Existing local data caches will be used.");
                    return 0;
                }

                string[] locales = new[] { "default", AppConfig.GetCdragonLocale(), "es_es", "tr_tr" }
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                int updated = 0;
                foreach (string locale in locales)
                {
                    token.ThrowIfCancellationRequested();
                    string prefix = $"plugins/rcp-be-lol-game-data/global/{locale}/v1/";
                    updated += await TryRefreshCacheAsync(prefix + "skins.json",
                        AppConfig.GetSkinsCachePath(locale, _storageRoot), CatalogKind.Skins, token) ? 1 : 0;
                    updated += await TryRefreshCacheAsync(prefix + "skinlines.json",
                        AppConfig.GetSkinLinesCachePath(locale, _storageRoot), CatalogKind.Skinlines, token) ? 1 : 0;
                    updated += await TryRefreshCacheAsync(prefix + "items.json",
                        AppConfig.GetItemsCachePath(locale, _storageRoot), CatalogKind.Items, token) ? 1 : 0;
                }

                // Retain Riot's original summary in Cache, alongside the localized
                // catalogs. Never replace preexisting user-maintained Config data.
                byte[] championSummary = await ReadWadAssetAsync(ChampionSummaryPath, token)
                    .ConfigureAwait(false);
                if (championSummary != null && TryGetChampionNames(championSummary, out byte[] names))
                {
                    string summaryCache = Path.Combine(AppConfig.GetCacheDirectory(_storageRoot),
                        "champion_summary_data_default.json");
                    updated += await WriteCatalogAsync(summaryCache, championSummary,
                        ChampionSummaryPath, token).ConfigureAwait(false) ? 1 : 0;
                    updated += await TryRefreshGeneratedChampionsAsync(names, championSummary, token)
                        .ConfigureAwait(false) ? 1 : 0;
                }

                _logger.LogInfo($"Local WAD catalog preparation complete: {updated} JSON files created or refreshed.");
                return updated;
            }
            finally
            {
                PreparationGate.Release();
            }
        }

        private async Task<bool> TryRefreshCacheAsync(string assetPath, string destination,
            CatalogKind kind, CancellationToken token)
        {
            byte[] bytes = await ReadWadAssetAsync(assetPath, token).ConfigureAwait(false);
            if (bytes == null) return false;
            try
            {
                using var document = JsonDocument.Parse(bytes);
                if (!IsValidCatalog(document.RootElement, kind))
                {
                    _logger.LogDebug($"Skipping incompatible WAD catalog entries: {assetPath}");
                    return false;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug($"Skipping malformed WAD catalog '{assetPath}': {ex.Message}");
                return false;
            }

            return await WriteCatalogAsync(destination, bytes, assetPath, token)
                .ConfigureAwait(false);
        }

        private async Task<byte[]> ReadWadAssetAsync(string path, CancellationToken token)
        {
            try
            {
                return await _source.ReadAssetBytesAsync(path, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                InvalidDataException or ArgumentException)
            {
                _logger.LogDebug($"Skipping unreadable WAD catalog '{path}': {ex.Message}");
                return null;
            }
        }

        private enum CatalogKind { Skins, Skinlines, Items }

        private static bool IsValidCatalog(JsonElement root, CatalogKind kind)
        {
            if (kind == CatalogKind.Skins)
            {
                if (root.ValueKind != JsonValueKind.Object) return false;
                int count = 0;
                int valid = 0;
                foreach (JsonProperty entry in root.EnumerateObject())
                {
                    if (entry.Value.ValueKind != JsonValueKind.Object) return false;
                    if (long.TryParse(entry.Name, out _) &&
                        entry.Value.TryGetProperty("name", out JsonElement name) &&
                        name.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(name.GetString()))
                        valid++;
                    count++;
                }
                // Older WADs can contain a few incomplete records. Reject an
                // empty or predominantly incompatible catalog as a corrupt patch.
                return count > 0 && valid > 0 && valid * 2 >= count;
            }

            if (root.ValueKind != JsonValueKind.Array) return false;
            int records = 0;
            int validRecords = 0;
            foreach (JsonElement entry in root.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) return false;
                if (entry.TryGetProperty("id", out JsonElement id) &&
                    id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out _) &&
                    entry.TryGetProperty("name", out JsonElement name) &&
                    name.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(name.GetString()))
                    validRecords++;
                records++;
            }
            return records > 0 && validRecords > 0 && validRecords * 2 >= records;
        }

        private static bool TryGetChampionNames(byte[] source, out byte[] namesJson)
        {
            namesJson = null;
            try
            {
                using var document = JsonDocument.Parse(source);
                if (document.RootElement.ValueKind != JsonValueKind.Array) return false;
                var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (JsonElement entry in document.RootElement.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object) continue;
                    foreach (string field in new[] { "alias", "name" })
                    {
                        if (entry.TryGetProperty(field, out JsonElement value) &&
                            value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                            names.Add(value.GetString());
                    }
                }
                if (names.Count == 0) return false;
                namesJson = JsonSerializer.SerializeToUtf8Bytes(names);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private async Task<bool> TryRefreshGeneratedChampionsAsync(byte[] names,
            byte[] summary, CancellationToken token)
        {
            string configPath = AppConfig.GetChampionsFilePath(_storageRoot);
            string trackingPath = Path.Combine(AppConfig.GetCacheDirectory(_storageRoot),
                "generated_champions.wad-source");
            string digest = Sha256(summary);

            if (File.Exists(configPath))
            {
                // Only edit the original list if we can prove it is unmodified
                // output of an earlier run. Older installations have no such proof.
                CacheProvenance prior = await ReadProvenanceAsync(trackingPath, token)
                    .ConfigureAwait(false);
                if (prior == null || prior.SourcePath != ChampionSummaryPath ||
                    prior.Version != ProvenanceVersion ||
                    !await MatchesDigestAsync(configPath, prior.ContentSha256, token)
                        .ConfigureAwait(false))
                    return false;
            }

            bool changed = await ReplaceIfDifferentAsync(configPath, names, token)
                .ConfigureAwait(false);
            await WriteProvenanceAsync(trackingPath,
                new CacheProvenance(ChampionSummaryPath, digest, Sha256(names)), token)
                .ConfigureAwait(false);
            return changed;
        }

        private async Task<bool> WriteCatalogAsync(string destination, byte[] contents,
            string assetPath, CancellationToken token)
        {
            // A cache created before provenance existed might contain a user's
            // own edits. Keep its original bytes recoverable on first migration.
            // The same applies to manually edited caches after migration.
            if (File.Exists(destination))
            {
                byte[] current = await File.ReadAllBytesAsync(destination, token).ConfigureAwait(false);
                if (!current.AsSpan().SequenceEqual(contents))
                {
                    CacheProvenance previous = await ReadProvenanceAsync(
                        destination + ".wad-source", token).ConfigureAwait(false);
                    if (previous?.Version != ProvenanceVersion ||
                        previous.SourcePath != assetPath ||
                        !Sha256(current).Equals(previous.ContentSha256,
                            StringComparison.OrdinalIgnoreCase))
                        await BackupLegacyCacheAsync(destination, current, token).ConfigureAwait(false);
                }
            }

            bool changed = await ReplaceIfDifferentAsync(destination, contents, token)
                .ConfigureAwait(false);
            string sidecar = destination + ".wad-source";
            var provenance = new CacheProvenance(assetPath, Sha256(contents), Sha256(contents));
            await WriteProvenanceAsync(sidecar, provenance, token).ConfigureAwait(false);
            return changed;
        }

        private static async Task BackupLegacyCacheAsync(string destination, byte[] current,
            CancellationToken token)
        {
            string backup = destination + ".pre-wad.bak";
            if (File.Exists(backup))
            {
                // Preserve the first migration backup; distinguish any later
                // manual changes by digest instead of overwriting earlier data.
                backup = destination + ".pre-wad." + Sha256(current)[..16] + ".bak";
            }
            if (File.Exists(backup)) return;

            string temporary = backup + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporary, current, token).ConfigureAwait(false);
                try { File.Move(temporary, backup); }
                catch (IOException) when (File.Exists(backup)) { }
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { }
            }
        }

        private static async Task<bool> MatchesDigestAsync(string file, string expected,
            CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(expected) || !File.Exists(file)) return false;
            byte[] current = await File.ReadAllBytesAsync(file, token).ConfigureAwait(false);
            return Sha256(current).Equals(expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string Sha256(byte[] content) =>
            Convert.ToHexString(SHA256.HashData(content));

        private static async Task<CacheProvenance> ReadProvenanceAsync(string sidecar,
            CancellationToken token)
        {
            if (!File.Exists(sidecar)) return null;
            try
            {
                return JsonSerializer.Deserialize<CacheProvenance>(
                    await File.ReadAllBytesAsync(sidecar, token).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }

        private static async Task WriteProvenanceAsync(string sidecar, CacheProvenance record,
            CancellationToken token)
        {
            byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(record);
            await ReplaceIfDifferentAsync(sidecar, serialized, token).ConfigureAwait(false);
        }

        private static async Task<bool> ReplaceIfDifferentAsync(string destination,
            byte[] bytes, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (File.Exists(destination) &&
                (await File.ReadAllBytesAsync(destination, token).ConfigureAwait(false)).AsSpan()
                    .SequenceEqual(bytes))
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);
                File.Move(temporary, destination, overwrite: true);
                return true;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { }
            }
        }

        private sealed class CacheProvenance
        {
            public CacheProvenance() { }

            public CacheProvenance(string sourcePath, string sourceSha256, string contentSha256)
            {
                SourcePath = sourcePath;
                SourceSha256 = sourceSha256;
                ContentSha256 = contentSha256;
            }

            public int Version { get; set; } = ProvenanceVersion;
            public string SourcePath { get; set; }
            public string SourceSha256 { get; set; }
            public string ContentSha256 { get; set; }
        }
    }
}
