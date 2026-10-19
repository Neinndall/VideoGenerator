using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VideoGenerator.Models;
using VideoGenerator.Utils;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Prepares the locally stored catalogs required by parsers and icon resolution.
    /// Game asset indexing can populate the catalogs when its local provider is available.
    /// Startup must not synchronize catalogs over HTTP or overwrite user data.
    /// </summary>
    public class DatabaseBuilder
    {
        private readonly LogService _logger;
        private readonly string _storageRoot;
        private readonly SemaphoreSlim _initializationGate = new(1, 1);
        private readonly TaskCompletionSource<bool> _initializationReady = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadyTask => _initializationReady.Task;
        public bool IsReady => _initializationReady.Task.IsCompleted;

        public DatabaseBuilder(LogService logger, string storageRoot = null)
        {
            _logger = logger;
            _storageRoot = storageRoot;
        }

        private string ChampionsFilePath => AppConfig.GetChampionsFilePath(_storageRoot);
        private string ItemsFilePath => AppConfig.GetItemsFilePath(_storageRoot);
        private string MonstersFilePath => AppConfig.GetMonstersFilePath(_storageRoot);
        private string StructuresFilePath => AppConfig.GetStructuresFilePath(_storageRoot);

        public async Task InitializeDatabasesAsync(string currentLolVersion)
        {
            await _initializationGate.WaitAsync();
            try
            {
                if (IsReady)
                    return;

                // version.json represents a previously synchronized version. Never
                // change it merely because a different client version was detected.
                await CreateCatalogIfMissingAsync(ChampionsFilePath, new List<string>());
                if (!File.Exists(ItemsFilePath))
                    await CreateCatalogIfMissingAsync(ItemsFilePath, await GetLocallyCachedItemsAsync());
                await CreateCatalogIfMissingAsync(MonstersFilePath, new MonsterDatabase());
                await CreateCatalogIfMissingAsync(StructuresFilePath, new List<StructureMapping>());

                _logger.LogInfo("Local game catalogs initialized without network synchronization.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize local game catalogs. Existing files remain unchanged.", ex);
            }
            finally
            {
                // Even a missing or damaged local catalog must not block analysis.
                // The user can restore it, or a future local game-assets provider can fill it.
                _initializationReady.TrySetResult(true);
                _initializationGate.Release();
            }
        }

        private async Task<Dictionary<string, string>> GetLocallyCachedItemsAsync()
        {
            var items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] locales = { AppConfig.GetCdragonLocale(), "default", "es_es", "tr_tr" };

            foreach (string locale in locales.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string path = AppConfig.GetItemsCachePath(locale, _storageRoot);
                if (!File.Exists(path))
                    continue;

                try
                {
                    using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
                    if (document.RootElement.ValueKind != JsonValueKind.Array)
                        throw new JsonException("Expected an array of item records.");

                    foreach (JsonElement entry in document.RootElement.EnumerateArray())
                    {
                        if (entry.ValueKind != JsonValueKind.Object ||
                            !entry.TryGetProperty("id", out JsonElement id) ||
                            id.ValueKind != JsonValueKind.Number ||
                            !id.TryGetInt32(out int itemId) ||
                            !entry.TryGetProperty("name", out JsonElement name) ||
                            name.ValueKind != JsonValueKind.String ||
                            string.IsNullOrWhiteSpace(name.GetString()))
                            continue;

                        items[name.GetString()] = itemId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }

                    if (items.Count > 0)
                    {
                        _logger.LogDebug($"Prepared {items.Count} item names from the existing local cache '{Path.GetFileName(path)}'.");
                        return items;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    _logger.LogWarn($"Local item cache '{Path.GetFileName(path)}' could not be read; original file preserved.");
                    _logger.LogDebug($"Local item cache details: {ex.Message}");
                }
            }

            // No WAD reader is wired into DatabaseBuilder yet. A schema-compatible
            // empty catalog keeps first-run parsing safe until local assets are indexed.
            return items;
        }

        private async Task CreateCatalogIfMissingAsync<T>(string path, T catalog)
        {
            // The existence check is intentional. User-maintained catalogs, even
            // ones with unsupported or malformed JSON, are never rewritten here.
            if (File.Exists(path))
                return;

            string temporaryPath = null;
            try
            {
                DirectoriesCreator.CreateParentDirectory(path);
                string json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
                await File.WriteAllTextAsync(temporaryPath, json, Encoding.UTF8);

                // File.Move without overwrite avoids races with another initializer
                // or with user edits made while the temporary file is being written.
                File.Move(temporaryPath, path);
            }
            catch (IOException ex) when (File.Exists(path))
            {
                _logger.LogDebug($"Local catalog '{Path.GetFileName(path)}' was created concurrently: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not bootstrap local catalog '{Path.GetFileName(path)}'.", ex);
            }
            finally
            {
                try
                {
                    if (temporaryPath != null && File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
