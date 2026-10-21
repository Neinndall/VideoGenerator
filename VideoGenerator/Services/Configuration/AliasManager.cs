using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using VideoGenerator.Models;
using VideoGenerator.Utils;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    public class AliasManager
    {
        private readonly string _configPath;
        private readonly LogService _logger;
        private bool _canSave = true;
        private string _loadedRevision;
        private readonly string _riotSummaryPath;
        public ObservableCollection<ChampionAlias> Aliases { get; } = new();

        public AliasManager(LogService logger, string aliasesFilePath = null,
            string riotSummaryPath = null)
        {
            _logger = logger;
            _configPath = string.IsNullOrWhiteSpace(aliasesFilePath)
                ? AppConfig.AliasesPath
                : aliasesFilePath;
            string configDirectory = Path.GetDirectoryName(_configPath);
            _riotSummaryPath = riotSummaryPath ??
                (string.IsNullOrWhiteSpace(aliasesFilePath)
                    ? AppConfig.GetChampionSummaryCachePath()
                    : Path.Combine(Directory.GetParent(configDirectory).FullName, "Cache",
                        "champion_summary_data_default.json"));
            LoadAliases();
        }

        public void LoadAliases()
        {
            Aliases.Clear();
            List<ChampionAlias> loaded = new();
            _canSave = true;
            _loadedRevision = GetFileRevision();
            bool mergedAny = !File.Exists(_configPath);

            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    loaded = JsonSerializer.Deserialize<List<ChampionAlias>>(json) ??
                        throw new JsonException("Invalid champion aliases catalog.");
                    if (loaded.Any(a => a == null || string.IsNullOrWhiteSpace(a.DisplayName) ||
                        string.IsNullOrWhiteSpace(a.InternalName)))
                        throw new JsonException("Champion alias names must not be empty.");
                    int originalCount = loaded.Count;
                    loaded = loaded.GroupBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
                    if (loaded.Count != originalCount) mergedAny = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to load champion aliases. The original champion_aliases.json is preserved; editing is disabled until it is repaired.", ex);
                    _canSave = false;
                }
            }

            // All known champions live in champion_aliases.json: the built-in
            // list comes from the existing Groups + exact Riot aliases.
            // The installed game's WAD summary adds new champions automatically.
            var defaults = DefaultChampions.Get();
            var fromRiotSummary = ReadRiotSummary();
            var riotNames = new HashSet<string>(
                fromRiotSummary.Select(entry => Normalize(entry.DisplayName)),
                StringComparer.OrdinalIgnoreCase);
            foreach (ChampionAlias fromRiot in fromRiotSummary)
            {
                var existing = defaults.FirstOrDefault(entry =>
                    Normalize(entry.DisplayName) == Normalize(fromRiot.DisplayName));
                if (existing == null)
                    defaults.Add(fromRiot);
                else
                {
                    // Riot is authoritative for its champion internal alias.
                    // A hand-maintained group roster may lag behind new aliases.
                    existing.InternalName = fromRiot.InternalName;
                    existing.Roles = fromRiot.Roles;
                }
            }

            foreach (var def in defaults)
            {
                var existing = loaded.FirstOrDefault(a =>
                    Normalize(a.DisplayName) == Normalize(def.DisplayName));
                if (existing == null)
                {
                    def.IsOfficial = true;
                    loaded.Add(def);
                    mergedAny = true;
                }
                else
                {
                    // Official Riot entries are refreshed when the installed
                    // game updates; custom user aliases are never replaced.
                    if (existing.IsOfficial &&
                        !string.Equals(existing.InternalName, def.InternalName,
                            StringComparison.Ordinal))
                    {
                        existing.InternalName = def.InternalName;
                        mergedAny = true;
                    }
                    if (riotNames.Contains(Normalize(def.DisplayName)) &&
                        def.Roles?.Count > 0 &&
                        !(existing.Roles ?? new List<string>()).SequenceEqual(
                            def.Roles ?? new List<string>(), StringComparer.OrdinalIgnoreCase))
                    {
                        existing.Roles = def.Roles?.ToList() ?? new List<string>();
                        mergedAny = true;
                    }
                    if (!existing.IsOfficial) mergedAny = true;
                    existing.IsOfficial = true;
                }
            }

            foreach (var alias in loaded.OrderBy(a => a.DisplayName))
            {
                Aliases.Add(alias);
            }

            if (_canSave && mergedAny) SaveAliases();
        }

        public bool SaveAliases()
        {
            if (!_canSave) return false;
            try
            {
                if (GetFileRevision() != _loadedRevision)
                {
                    _logger.LogWarn("champion_aliases.json changed elsewhere. Reopen Mapping before saving.");
                    return false;
                }
                DirectoriesCreator.CreateParentDirectory(_configPath);
                string json = JsonSerializer.Serialize(Aliases.ToList(), new JsonSerializerOptions 
                { 
                    WriteIndented = true, 
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
                });
                string temp = _configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temp, json, new UTF8Encoding(false));
                    File.Move(temp, _configPath, overwrite: true);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
                _loadedRevision = GetFileRevision();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save champion aliases.", ex);
                return false;
            }
        }

        private string GetFileRevision()
        {
            try
            {
                var file = new FileInfo(_configPath);
                return file.Exists ? $"{file.LastWriteTimeUtc.Ticks}:{file.Length}" : "<missing>";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return "<unavailable>";
            }
        }

        public string GetInternalName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return string.Empty;
            string clean = displayName.Replace(" ", "").Replace("'", "").Replace(".", "").Replace("-", "");
            var alias = Aliases.FirstOrDefault(a => a.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase) || 
                                                     a.DisplayName.Replace(" ", "").Replace("'", "").Replace(".", "").Replace("-", "").Equals(clean, StringComparison.OrdinalIgnoreCase));
            
            if (alias != null) return alias.InternalName;
            string key = Normalize(displayName);
            return CurrentRiotNames().TryGetValue(key, out string internalName)
                ? internalName : clean;
        }

        private Dictionary<string, string> _validChampions;
        private string _championSummaryRevision;
        private readonly object _championsLock = new();

        public bool IsValidChampion(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return false;
            string clean = displayName.Replace(" ", "").Replace("'", "").Replace(".", "").Replace("-", "");
            
            if (CurrentRiotNames().ContainsKey(Normalize(clean))) return true;

            return Aliases.Any(a => a.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase) || 
                                     a.DisplayName.Replace(" ", "").Replace("'", "").Replace(".", "").Replace("-", "").Equals(clean, StringComparison.OrdinalIgnoreCase) ||
                                     a.InternalName.Equals(clean, StringComparison.OrdinalIgnoreCase));
        }

        public void InvalidateChampionCatalog()
        {
            lock (_championsLock)
            {
                _validChampions = null;
                _championSummaryRevision = null;
            }
        }

        private IReadOnlyDictionary<string, string> CurrentRiotNames()
        {
            string revision;
            try
            {
                var info = new FileInfo(_riotSummaryPath);
                revision = info.Exists ? $"{info.LastWriteTimeUtc.Ticks}:{info.Length}" : "<missing>";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                revision = "<unavailable>";
            }
            lock (_championsLock)
            {
                if (_validChampions == null || _championSummaryRevision != revision)
                {
                    var entries = ReadRiotSummary();
                    var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (ChampionAlias entry in entries)
                    {
                        byName[Normalize(entry.DisplayName)] = entry.InternalName;
                        byName[Normalize(entry.InternalName)] = entry.InternalName;
                    }
                    // Keep the mode-specific Riot aliases addressable when an
                    // event explicitly names them (e.g. Jade_Ahri). They must
                    // NEVER replace Ahri -> Ahri in the main champion catalog.
                    foreach (var variant in ReadRiotSummaryEntries())
                    {
                        string key = Normalize(variant.Champion.InternalName);
                        if (key.Length > 0)
                            byName.TryAdd(key, variant.Champion.InternalName);
                    }
                    _validChampions = byName;
                    _championSummaryRevision = revision;
                }
                return _validChampions;
            }
        }

        private List<ChampionAlias> ReadRiotSummary()
        {
            // Riot's champion-summary.json may contain multiple entries with the
            // SAME displayed champion (e.g. Ahri / Jade_Ahri, ids 103 / 60103).
            // These are separate game-mode variants, NOT a replacement for the
            // standard champion. Keep the canonical record for Event Mapping,
            // regardless of JSON order. Explicit variants remain available in
            // CurrentRiotNames without becoming additional display-name rows.
            return ReadRiotSummaryEntries()
                .GroupBy(item => Normalize(item.Champion.DisplayName),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    // Exact champion aliases (e.g. Ahri) outrank alternate
                    // variant aliases. Wukong/MonkeyKing is handled by ID.
                    .OrderBy(item => Normalize(item.Champion.InternalName) ==
                                     Normalize(item.Champion.DisplayName) ? 0 : 1)
                    .ThenBy(item => item.Id)
                    .ThenBy(item => item.Champion.InternalName, StringComparer.OrdinalIgnoreCase)
                    .First().Champion)
                .ToList();
        }

        private List<(ChampionAlias Champion, int Id)> ReadRiotSummaryEntries()
        {
            var candidates = new List<(ChampionAlias Champion, int Id)>();
            if (!File.Exists(_riotSummaryPath)) return candidates;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(_riotSummaryPath));
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    throw new JsonException("Invalid Riot champion summary.");
                foreach (var entry in document.RootElement.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object ||
                        !entry.TryGetProperty("alias", out var alias) ||
                        !entry.TryGetProperty("name", out var name) ||
                        alias.ValueKind != JsonValueKind.String ||
                        name.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(alias.GetString()) ||
                        string.IsNullOrWhiteSpace(name.GetString()))
                        continue;
                    int id = entry.TryGetProperty("id", out var idValue) &&
                             idValue.ValueKind == JsonValueKind.Number &&
                             idValue.TryGetInt32(out int currentId) ? currentId : int.MaxValue;
                    if (id <= 0) continue;
                    candidates.Add((new ChampionAlias
                    {
                        DisplayName = name.GetString(),
                        InternalName = alias.GetString(),
                        Roles = ReadChampionRoles(entry),
                        IsOfficial = true
                    }, id));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogError("Could not read the Riot champion summary cache.", ex);
            }
            return candidates;
        }

        private static List<string> ReadChampionRoles(JsonElement champion)
        {
            if (!champion.TryGetProperty("roles", out JsonElement roles) ||
                roles.ValueKind != JsonValueKind.Array)
                return new List<string>();

            return roles.EnumerateArray()
                .Where(role => role.ValueKind == JsonValueKind.String)
                .Select(role => role.GetString()?.Trim())
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string Normalize(string text) =>
            DefaultChampions.Normalize(text).ToLowerInvariant();
    }
}
