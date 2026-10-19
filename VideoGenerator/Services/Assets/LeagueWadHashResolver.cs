using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Translates a semantic image request to the exact lowercase Riot WAD virtual
    /// path and its xxHash64 identifier. WAD chunk hashes cannot be reversed into
    /// original filenames: candidate paths must come from known game metadata.
    /// </summary>
    public sealed class LeagueWadHashResolver
    {
        private const string PluginBase = "plugins/rcp-be-lol-game-data/global/";
        private readonly LeagueWadAssetSource _source;
        private readonly object _lock = new();
        private readonly Dictionary<string, List<WadAssetPathCandidate>> _candidates =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _championIdGate = new(1, 1);
        private IReadOnlyDictionary<string, int> _championIds;
        private string _championIdsRevision;

        public LeagueWadHashResolver(LeagueWadAssetSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public sealed record WadAssetPathCandidate(string Path, ulong Hash);

        /// <summary>
        /// Called after a Riot patch changes the archive snapshot. This prevents
        /// remembered champion IDs and asset paths from referring to old metadata.
        /// </summary>
        public void InvalidateCachedMetadata()
        {
            lock (_lock)
                _candidates.Clear();
            _championIds = null;
            _championIdsRevision = null;
        }

        public static string NormalizeVirtualPath(string virtualPath)
        {
            if (string.IsNullOrWhiteSpace(virtualPath)) return null;
            string path = virtualPath.Trim().Replace('\\', '/').TrimStart('/');
            if (path.Contains("://", StringComparison.Ordinal) ||
                path.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("lol-game-data/assets/", StringComparison.OrdinalIgnoreCase))
            {
                if (!path.StartsWith("lol-game-data/assets/", StringComparison.OrdinalIgnoreCase))
                    return null;
                path = path.Substring("lol-game-data/assets/".Length);
            }
            if (path.StartsWith("lol-game-data/", StringComparison.OrdinalIgnoreCase))
                path = path.Substring("lol-game-data/".Length);
            while (path.StartsWith("assets/assets/", StringComparison.OrdinalIgnoreCase))
                path = path.Substring("assets/".Length);
            if (path.StartsWith("characters/", StringComparison.OrdinalIgnoreCase))
                path = "assets/" + path;

            var segments = path.Split('/');
            if (segments.Length < 2 || segments.Any(segment => string.IsNullOrWhiteSpace(segment) ||
                segment is "." or ".." || segment.Any(char.IsControl) ||
                segment.IndexOfAny(new[] { ':', '?', '#', '\\' }) >= 0))
                return null;
            return string.Join("/", segments).ToLowerInvariant();
        }

        public IReadOnlyList<WadAssetPathCandidate> Register(
            string iconType, string iconLookup, string virtualPath, string locale = "default")
        {
            if (string.IsNullOrWhiteSpace(iconType) || string.IsNullOrWhiteSpace(iconLookup))
                return Array.Empty<WadAssetPathCandidate>();
            string normalized = NormalizeVirtualPath(virtualPath);
            if (normalized == null) return Array.Empty<WadAssetPathCandidate>();
            string loc = NormalizeLocale(locale);
            if (loc == null) return Array.Empty<WadAssetPathCandidate>();

            string[] paths;
            if (normalized.StartsWith("plugins/", StringComparison.Ordinal) ||
                normalized.StartsWith("data/", StringComparison.Ordinal))
                paths = new[] { normalized };
            else if (normalized.StartsWith("assets/", StringComparison.Ordinal))
                paths = (loc == "default"
                    ? new[] { PluginBase + "default/" + normalized, normalized }
                    : new[] { PluginBase + loc + "/" + normalized,
                        PluginBase + "default/" + normalized, normalized });
            else if (normalized.StartsWith("v1/", StringComparison.Ordinal))
                paths = (loc == "default"
                    ? new[] { PluginBase + "default/" + normalized }
                    : new[] { PluginBase + loc + "/" + normalized,
                        PluginBase + "default/" + normalized });
            else
                return Array.Empty<WadAssetPathCandidate>();

            string key = iconType.Trim().ToLowerInvariant() + ":" + iconLookup.Trim().ToLowerInvariant();
            var result = new List<WadAssetPathCandidate>();
            lock (_lock)
            {
                if (!_candidates.TryGetValue(key, out var existing))
                    _candidates[key] = existing = new List<WadAssetPathCandidate>();

                foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var candidate = new WadAssetPathCandidate(path, LeagueWadAssetSource.ComputePathHash(path));
                    if (existing.Any(value => value.Hash == candidate.Hash)) continue;
                    existing.Add(candidate);
                    _source.RegisterIconAssetHash(iconType, iconLookup, candidate.Hash);
                    result.Add(candidate);
                }
            }
            return result;
        }

        public IReadOnlyList<WadAssetPathCandidate> GetRegistered(string iconType, string iconLookup)
        {
            if (string.IsNullOrWhiteSpace(iconType) || string.IsNullOrWhiteSpace(iconLookup))
                return Array.Empty<WadAssetPathCandidate>();
            string key = iconType.Trim().ToLowerInvariant() + ":" + iconLookup.Trim().ToLowerInvariant();
            lock (_lock)
                return _candidates.TryGetValue(key, out var items)
                    ? items.ToArray()
                    : Array.Empty<WadAssetPathCandidate>();
        }

        public async Task<int?> GetChampionIdAsync(string champion, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(champion)) return null;
            if (_championIds == null ||
                _championIdsRevision != _source.ArchiveRevision)
            {
                await _championIdGate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    string currentRevision = _source.ArchiveRevision;
                    if (_championIds == null ||
                        _championIdsRevision != currentRevision)
                    {
                        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        byte[] bytes = await _source.ReadAssetBytesAsync(
                            PluginBase + "default/v1/champion-summary.json", token).ConfigureAwait(false);
                        if (bytes != null)
                        {
                            try
                            {
                                using var document = JsonDocument.Parse(bytes);
                                if (document.RootElement.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var record in document.RootElement.EnumerateArray())
                                    {
                                        if (record.ValueKind != JsonValueKind.Object ||
                                            !record.TryGetProperty("id", out var idProperty) ||
                                            idProperty.ValueKind != JsonValueKind.Number ||
                                            !idProperty.TryGetInt32(out int number) || number <= 0)
                                            continue;
                                        foreach (string field in new[] { "alias", "name" })
                                            if (record.TryGetProperty(field, out var value) &&
                                                value.ValueKind == JsonValueKind.String &&
                                                !string.IsNullOrWhiteSpace(value.GetString()))
                                                ids.TryAdd(value.GetString(), number);
                                    }
                                }
                            }
                            catch (JsonException) { }
                        }
                        _championIds = ids;
                        _championIdsRevision = currentRevision;
                    }
                }
                finally
                {
                    _championIdGate.Release();
                }
            }
            return _championIds.TryGetValue(champion.Trim(), out int id) ? id : null;
        }

        private static string NormalizeLocale(string locale)
        {
            string value = (locale ?? "default").ToLowerInvariant().Trim();
            return value switch
            {
                "en" or "en_us" or "all" => "default",
                "es" => "es_es",
                "tr" => "tr_tr",
                _ when value == "default" ||
                    (value.Length == 5 && value[2] == '_' &&
                     value.Where((_, i) => i != 2).All(char.IsLetter)) => value,
                _ => null
            };
        }
    }
}
