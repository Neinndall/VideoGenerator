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
        private static readonly SemaphoreSlim CatalogMergeGate = new(1, 1);
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
                await CatalogMergeGate.WaitAsync();
                try
                {
                    await InitializeMonsterCatalogAsync();
                    await InitializeStructureCatalogAsync();
                }
                finally { CatalogMergeGate.Release(); }

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

        private async Task InitializeMonsterCatalogAsync()
        {
            var data = new DataFetcher(_logger, _storageRoot);

            if (!File.Exists(MonstersFilePath))
            {
                await CreateCatalogIfMissingAsync(MonstersFilePath, DefaultMonsters.Get());
                return;
            }
            if (!data.TryLoadMonsterDatabase(out MonsterDatabase merged))
                return;

            // When a future app update adds a new built-in monster, only append
            // missing defaults; existing categories and personal entries win.
            string previous = await File.ReadAllTextAsync(MonstersFilePath);
            bool missingDefault = DefaultMonsters.Entries.Any(entry =>
                !ContainsMonster(previous, entry.Name));
            if (missingDefault)
                await ReplaceCatalogIfUnchangedAsync(MonstersFilePath, previous, merged);
        }

        private static bool ContainsMonster(string json, string name)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                IEnumerable<JsonElement> values = new[] { "Epic", "Large" }
                    .Where(key => document.RootElement.TryGetProperty(key, out _))
                    .SelectMany(key => document.RootElement.GetProperty(key).EnumerateArray());
                return values.Any(value => value.ValueKind == JsonValueKind.String &&
                    DefaultMonsters.Normalize(value.GetString()) ==
                    DefaultMonsters.Normalize(name));
            }
            catch (JsonException) { return false; }
        }

        private async Task InitializeStructureCatalogAsync()
        {
            if (!File.Exists(StructuresFilePath))
            {
                await CreateCatalogIfMissingAsync(StructuresFilePath, DefaultStructures.Get());
                return;
            }
            var data = new DataFetcher(_logger, _storageRoot);
            if (!data.TryLoadStructureMappings(out List<StructureMapping> merged))
                return;
            string previous = await File.ReadAllTextAsync(StructuresFilePath);
            using var document = JsonDocument.Parse(previous);
            var saved = JsonSerializer.Deserialize<List<StructureMapping>>(previous) ??
                new List<StructureMapping>();
            bool missingDefault = DefaultStructures.Entries.Any(entry =>
                !saved.Any(item => DefaultStructures.Normalize(item.Keyword) ==
                    DefaultStructures.Normalize(entry.Keyword)));
            if (missingDefault)
                await ReplaceCatalogIfUnchangedAsync(StructuresFilePath, previous, merged);
        }

        private async Task ReplaceCatalogIfUnchangedAsync<T>(
            string path, string previous, T merged)
        {
            string temp = null;
            try
            {
                string json = JsonSerializer.Serialize(merged, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                temp = $"{path}.{Guid.NewGuid():N}.tmp";
                await File.WriteAllTextAsync(temp, json, Encoding.UTF8);
                // Avoid replacing a file edited while we were preparing data.
                if (await File.ReadAllTextAsync(path) == previous)
                    File.Move(temp, path, overwrite: true);
            }
            finally
            {
                if (temp != null && File.Exists(temp))
                    File.Delete(temp);
            }
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
