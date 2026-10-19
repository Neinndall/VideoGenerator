using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using VideoGenerator.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Read-only adapter for locally stored Riot game-data JSON caches. Asset paths are
    /// virtual WAD paths, not URLs or physical filesystem paths.
    /// </summary>
    public sealed class OfflineSkinCatalog
    {
        private static readonly Regex SkinDirectory = new(
            @"(?:^|/)characters/([^/]+)/skins/(?:skin([0-9]+)|base)(?:/|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly string _storageRoot;
        private readonly LogService _logger;

        public OfflineSkinCatalog(string storageRoot = null, LogService logger = null)
        {
            _storageRoot = storageRoot;
            _logger = logger;
        }

        /// <summary>
        /// Returns exact candidates suitable for LeagueWadAssetSource.RegisterIconAssetPath.
        /// Tile images precede splash art; the requested locale precedes the default
        /// cache. Missing or malformed files never trigger a network request.
        /// </summary>
        public IReadOnlyList<string> GetSkinAssetPaths(string champion, int skinId, string locale = "default")
        {
            if (string.IsNullOrWhiteSpace(champion) || skinId < 0 || NormalizeLocale(locale) == null)
                return Array.Empty<string>();

            var tilePaths = new List<string>();
            var splashPaths = new List<string>();
            string championKey = NormalizeChampion(champion);
            ForEachSkin(locale, skin =>
            {
                string tile = NormalizeAssetPath(ReadString(skin, "tilePath"));
                string splash = NormalizeAssetPath(ReadString(skin, "splashPath"));

                // Verify each path independently: a mismatched metadata entry
                // must never register an asset belonging to another champion.
                if (MatchesSkin(tile, championKey, skinId))
                    tilePaths.Add(tile);
                if (MatchesSkin(splash, championKey, skinId))
                    splashPaths.Add(splash);
            });

            // Preserve the most useful priority: tiles, then splashes, with no
            // duplicate hashes even when cache records or quest tiers overlap.
            return tilePaths.Concat(splashPaths)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <summary>
        /// Full splash artwork only. A 380px tile must never silently replace
        /// a missing wallpaper merely because both refer to the same skin.
        /// </summary>
        public IReadOnlyList<string> GetSplashAssetPaths(string champion, int skinId, string locale = "default")
        {
            if (string.IsNullOrWhiteSpace(champion) || skinId < 0 || NormalizeLocale(locale) == null)
                return Array.Empty<string>();
            var result = new List<string>();
            string championKey = NormalizeChampion(champion);
            ForEachSkin(locale, skin =>
            {
                string splash = NormalizeAssetPath(ReadString(skin, "splashPath"));
                if (MatchesSkin(splash, championKey, skinId))
                    result.Add(splash);
            });
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <summary>
        /// Accepts the exact contents of a skins.json WAD entry without writing any
        /// cache files. The caller remains responsible for reading the local WAD.
        /// </summary>
        public IReadOnlyList<string> GetSkinAssetPathsFromJson(
            ReadOnlyMemory<byte> skinsJson, string champion, int skinId)
        {
            if (string.IsNullOrWhiteSpace(champion) || skinId < 0 || skinsJson.IsEmpty)
                return Array.Empty<string>();

            var tiles = new List<string>();
            var splashes = new List<string>();
            string championKey = NormalizeChampion(champion);
            try
            {
                using var document = JsonDocument.Parse(skinsJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return Array.Empty<string>();
                ForEachSkinRecord(document.RootElement, skin =>
                {
                    string tile = NormalizeAssetPath(ReadString(skin, "tilePath"));
                    string splash = NormalizeAssetPath(ReadString(skin, "splashPath"));
                    if (MatchesSkin(tile, championKey, skinId)) tiles.Add(tile);
                    if (MatchesSkin(splash, championKey, skinId)) splashes.Add(splash);
                });
            }
            catch (JsonException ex)
            {
                _logger?.LogDebug($"Local WAD skins metadata could not be read: {ex.Message}");
                return Array.Empty<string>();
            }

            return tiles.Concat(splashes)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <summary>Finds the localized skin name, falling back to the default cache.</summary>
        public string GetSkinName(string champion, int skinId, string locale = "default")
        {
            if (string.IsNullOrWhiteSpace(champion) || skinId < 0 || NormalizeLocale(locale) == null)
                return null;

            string found = null;
            string championKey = NormalizeChampion(champion);
            ForEachSkin(locale, skin =>
            {
                if (found != null) return;
                string splash = NormalizeAssetPath(ReadString(skin, "splashPath"));
                string tile = NormalizeAssetPath(ReadString(skin, "tilePath"));
                if (MatchesSkin(splash, championKey, skinId) || MatchesSkin(tile, championKey, skinId))
                    found = ReadString(skin, "name");
            });
            return found;
        }

        /// <summary>
        /// Returns locally known thematic skinlines with their member champion and
        /// skin IDs. IDs are stable across localized skinline name translations.
        /// </summary>
        public IReadOnlyList<OfflineSkinline> GetSkinlines(string locale = "default")
        {
            if (NormalizeLocale(locale) == null)
                return Array.Empty<OfflineSkinline>();

            var names = new Dictionary<int, string>();
            foreach (string cacheLocale in Locales(locale))
            {
                string path = AppConfig.GetSkinLinesCachePath(cacheLocale, _storageRoot);
                WithJson(path, JsonValueKind.Array, root =>
                {
                    foreach (JsonElement entry in root.EnumerateArray())
                    {
                        if (entry.ValueKind != JsonValueKind.Object ||
                            !TryReadId(entry, "id", out int id) || names.ContainsKey(id))
                            continue;
                        string name = ReadString(entry, "name");
                        if (!string.IsNullOrWhiteSpace(name))
                            names[id] = name;
                    }
                });
            }

            var members = names.Keys.ToDictionary(id => id, _ => new List<OfflineSkinReference>());
            ForEachSkin(locale, skin =>
            {
                if (!skin.TryGetProperty("skinLines", out JsonElement lines) ||
                    lines.ValueKind != JsonValueKind.Array)
                    return;

                string splash = NormalizeAssetPath(ReadString(skin, "splashPath"));
                string tile = NormalizeAssetPath(ReadString(skin, "tilePath"));
                if (!TryGetSkinIdentity(splash, out string champion, out int skinId) &&
                    !TryGetSkinIdentity(tile, out champion, out skinId))
                    return;

                foreach (JsonElement line in lines.EnumerateArray())
                {
                    if (!TryReadSkinlineId(line, out int id) || !members.TryGetValue(id, out var list))
                        continue;
                    if (!list.Any(item => item.SkinId == skinId &&
                                          item.Champion.Equals(champion, StringComparison.OrdinalIgnoreCase)))
                        list.Add(new OfflineSkinReference(champion, skinId));
                }
            });

            return names.OrderBy(pair => pair.Key)
                .Select(pair => new OfflineSkinline(pair.Key, pair.Value,
                    members[pair.Key].OrderBy(c => c.Champion, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(c => c.SkinId).ToArray()))
                .ToArray();
        }

        /// <summary>Reads item IDs, display names and slugs from the existing item cache.</summary>
        public IReadOnlyList<OfflineItem> GetItems(string locale = "default")
        {
            if (NormalizeLocale(locale) == null)
                return Array.Empty<OfflineItem>();

            var items = new Dictionary<int, OfflineItem>();
            foreach (string cacheLocale in Locales(locale))
            {
                string path = AppConfig.GetItemsCachePath(cacheLocale, _storageRoot);
                WithJson(path, JsonValueKind.Array, root =>
                {
                    foreach (JsonElement entry in root.EnumerateArray())
                    {
                        if (entry.ValueKind != JsonValueKind.Object ||
                            !TryReadId(entry, "id", out int id) || items.ContainsKey(id))
                            continue;
                        string name = ReadString(entry, "name");
                        string slug = ReadString(entry, "nameSlug");
                        if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(slug))
                            items[id] = new OfflineItem(id, name, slug);
                    }
                });
            }
            return items.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();
        }

        /// <summary>Resolves numeric item IDs using cached display names or slugs.</summary>
        public int? GetItemId(string itemNameOrSlug, string locale = "default")
        {
            if (string.IsNullOrWhiteSpace(itemNameOrSlug)) return null;
            string key = NormalizeChampion(itemNameOrSlug);
            return GetItems(locale).Where(item =>
                NormalizeChampion(item.Name) == key ||
                NormalizeChampion(item.NameSlug) == key)
                .Select(item => (int?)item.Id).FirstOrDefault();
        }

        /// <summary>
        /// Converts Riot/CommunityDragon virtual paths such as
        /// /lol-game-data/assets/ASSETS/Characters/Ahri/... to the exact
        /// assets/characters/ahri/... form used for WAD path hashing.
        /// Returns null for external URLs, malformed paths or traversals.
        /// </summary>
        public static string NormalizeAssetPath(string virtualPath)
        {
            if (string.IsNullOrWhiteSpace(virtualPath)) return null;
            string path = virtualPath.Trim().Replace('\\', '/').TrimStart('/');
            const string gameDataAssets = "lol-game-data/assets/";
            const string gameData = "lol-game-data/";
            if (path.StartsWith(gameDataAssets, StringComparison.OrdinalIgnoreCase))
                path = path.Substring(gameDataAssets.Length);
            else if (path.StartsWith(gameData, StringComparison.OrdinalIgnoreCase))
                path = path.Substring(gameData.Length);

            while (path.StartsWith("assets/assets/", StringComparison.OrdinalIgnoreCase))
                path = path.Substring("assets/".Length);

            if (path.StartsWith("characters/", StringComparison.OrdinalIgnoreCase))
                path = "assets/" + path;

            if (!path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                return null;

            string[] segments = path.Split('/');
            if (segments.Length < 3 || segments.Any(segment =>
                string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".." ||
                segment.Any(char.IsControl) || segment.IndexOfAny(new[] { ':', '?', '#', '\\' }) >= 0))
                return null;

            return string.Join("/", segments).ToLowerInvariant();
        }

        private void ForEachSkin(string locale, Action<JsonElement> visitor)
        {
            foreach (string cacheLocale in Locales(locale))
            {
                string path = AppConfig.GetSkinsCachePath(cacheLocale, _storageRoot);
                WithJson(path, JsonValueKind.Object, root => ForEachSkinRecord(root, visitor));
            }
        }

        private static void ForEachSkinRecord(JsonElement root, Action<JsonElement> visitor)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object) continue;
                visitor(property.Value);
                if (!property.Value.TryGetProperty("questSkinInfo", out JsonElement quest) ||
                    quest.ValueKind != JsonValueKind.Object ||
                    !quest.TryGetProperty("tiers", out JsonElement tiers) ||
                    tiers.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (JsonElement tier in tiers.EnumerateArray())
                {
                    if (tier.ValueKind == JsonValueKind.Object)
                        visitor(tier);
                }
            }
        }

        private void WithJson(string path, JsonValueKind expectedKind, Action<JsonElement> read)
        {
            try
            {
                if (!File.Exists(path)) return;
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.ValueKind != expectedKind)
                    return;
                read(document.RootElement);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger?.LogDebug($"Offline catalog '{Path.GetFileName(path)}' could not be read: {ex.Message}");
            }
        }

        private static string ReadString(JsonElement element, string field) =>
            element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(field, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static bool TryReadId(JsonElement element, string field, out int id)
        {
            id = 0;
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(field, out JsonElement value)) return false;
            return value.ValueKind switch
            {
                JsonValueKind.Number => value.TryGetInt32(out id),
                JsonValueKind.String => int.TryParse(value.GetString(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out id),
                _ => false
            };
        }

        private static bool TryReadSkinlineId(JsonElement line, out int id)
        {
            id = 0;
            return line.ValueKind == JsonValueKind.Object
                ? TryReadId(line, "id", out id)
                : line.ValueKind == JsonValueKind.Number && line.TryGetInt32(out id);
        }

        private static bool MatchesSkin(string path, string champion, int skinId) =>
            TryGetSkinIdentity(path, out string parsedChampion, out int parsedSkinId) &&
            NormalizeChampion(parsedChampion) == champion && parsedSkinId == skinId;

        private static bool TryGetSkinIdentity(string path, out string champion, out int skinId)
        {
            champion = null;
            skinId = 0;
            if (path == null) return false;
            Match match = SkinDirectory.Match(path);
            if (!match.Success || (match.Groups[2].Success &&
                !int.TryParse(match.Groups[2].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out skinId))) return false;
            champion = match.Groups[1].Value;
            return true;
        }

        private static string NormalizeChampion(string name) =>
            new string((name ?? string.Empty).Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray());

        private static string NormalizeLocale(string locale)
        {
            string value = (locale ?? "default").Trim().ToLowerInvariant();
            return value switch
            {
                "en" or "en_us" or "all" => "default",
                "es" => "es_es",
                "tr" => "tr_tr",
                "default" => "default",
                _ => Regex.IsMatch(value, @"^[a-z]{2}_[a-z]{2}$",
                    RegexOptions.CultureInvariant) ? value : null
            };
        }

        private static IEnumerable<string> Locales(string locale)
        {
            string requested = NormalizeLocale(locale);
            if (requested == null) yield break;
            yield return requested;
            if (!requested.Equals("default", StringComparison.Ordinal))
                yield return "default";
        }
    }

    public sealed record OfflineSkinReference(string Champion, int SkinId);
    public sealed record OfflineSkinline(int Id, string Name, IReadOnlyList<OfflineSkinReference> Skins);
    public sealed record OfflineItem(int Id, string Name, string NameSlug);
}
