using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Local-only icon resolver. Existing valid icons remain usable; all new assets come
    /// from the installed League WADs through LeagueWadAssetSource.
    /// </summary>
    public sealed class LocalGameIconManager
    {
        private const string GameData = "plugins/rcp-be-lol-game-data/global/default/v1/";
        private const string GameAssets = "plugins/rcp-be-lol-game-data/global/default/";
        private static readonly Regex SkinDirectory = new(
            @"(?:^|/)characters/([^/]+)/skins/(?:skin([0-9]+)|base)(?:/|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private readonly LeagueWadAssetSource _source;
        private readonly DataFetcher _data;
        private readonly AliasManager _aliases;
        private readonly GroupManager _groups;
        private readonly SkinlineManager _skinlines;
        private readonly LogService _logger;
        private readonly string _cacheRoot;
        private readonly LeagueWadHashResolver _hashes;
        private readonly OfflineSkinCatalog _offlineSkins;
        private readonly object _catalogLock = new();
        private CatalogSnapshot _catalogs;
        private long _nextGeneration;

        private sealed record ItemAsset(string Id, string Path);
        private sealed record ItemCatalog(bool HasWadMetadata,
            IReadOnlyDictionary<string, ItemAsset> Items);
        private sealed record ChampionCatalog(bool HasWadMetadata,
            IReadOnlyDictionary<string, int> Ids);
        private sealed record SkinCatalog(bool HasWadMetadata,
            IReadOnlyDictionary<string, IReadOnlyList<string>> Paths,
            IReadOnlyDictionary<string, IReadOnlyList<string>> SplashPaths,
            IReadOnlyDictionary<string, IReadOnlyList<LocalSplashSkin>> SplashSkins,
            IReadOnlyDictionary<int, IReadOnlyList<OfflineSkinReference>> SkinlineMembers);

        public sealed record LocalSplashSkin(int SkinId, string Name)
        {
            public string Label => $"{SkinId} · {Name}";
        }

        private sealed class SkinPaths
        {
            public readonly List<string> Tiles = new();
            public readonly List<string> Splashes = new();

            public IReadOnlyList<string> ToArray() => Tiles.Concat(Splashes)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private sealed class CatalogSnapshot
        {
            public CatalogSnapshot(long generation, string archiveRevision,
                Func<Task<SkinCatalog>> loadSkins, Func<Task<ItemCatalog>> loadItems,
                Func<Task<ChampionCatalog>> loadChampions,
                Func<SkinCatalog, Task<IReadOnlyList<OfflineSkinline>>> loadSkinlines)
            {
                Generation = generation;
                ArchiveRevision = archiveRevision;
                Skins = new Lazy<Task<SkinCatalog>>(loadSkins);
                Items = new Lazy<Task<ItemCatalog>>(loadItems);
                Champions = new Lazy<Task<ChampionCatalog>>(loadChampions);
                Skinlines = new Lazy<Task<IReadOnlyList<OfflineSkinline>>>(async () =>
                    await loadSkinlines(await Skins.Value));
            }

            public long Generation { get; }
            public string ArchiveRevision { get; }
            public Lazy<Task<SkinCatalog>> Skins { get; }
            public Lazy<Task<ItemCatalog>> Items { get; }
            public Lazy<Task<ChampionCatalog>> Champions { get; }
            public Lazy<Task<IReadOnlyList<OfflineSkinline>>> Skinlines { get; }
            public ConcurrentDictionary<string, IReadOnlyList<string>> SkinPaths { get; } =
                new(StringComparer.OrdinalIgnoreCase);
            public ConcurrentDictionary<string, SemaphoreSlim> IconGates { get; } =
                new(StringComparer.OrdinalIgnoreCase);

            // A unique lookup per generation also invalidates rendered PNGs whose
            // Riot WAD asset path and hash stay unchanged across a patch.
            public string Lookup(string original) => original + "@catalog" + Generation;
        }

        public LocalGameIconManager(LeagueWadAssetSource source, DataFetcher data,
            AliasManager aliases, GroupManager groups, SkinlineManager skinlines, LogService logger,
            string cacheRoot = null, LeagueWadHashResolver hashes = null,
            OfflineSkinCatalog offlineSkins = null)
        {
            _source = source;
            _data = data;
            _aliases = aliases;
            _groups = groups;
            _skinlines = skinlines;
            _logger = logger;
            _cacheRoot = string.IsNullOrWhiteSpace(cacheRoot) ? AppConfig.IconCacheDir : cacheRoot;
            _hashes = hashes ?? new LeagueWadHashResolver(source);
            _offlineSkins = offlineSkins ?? new OfflineSkinCatalog(logger: logger);
            _catalogs = CreateSnapshot();
        }

        /// <summary>
        /// Invalidates all patch-dependent Riot metadata. Call after refreshing the
        /// WAD archives or rebuilding local metadata; in-flight lookups finish against
        /// their original snapshot while subsequent lookups use the new catalog.
        /// No full skins.json read occurs until a skin icon is actually requested.
        /// </summary>
        public void RefreshCatalogs()
        {
            lock (_catalogLock)
                Volatile.Write(ref _catalogs, CreateSnapshot());
        }

        private CatalogSnapshot CreateSnapshot() => new(
            Interlocked.Increment(ref _nextGeneration), _source.ArchiveRevision,
            LoadSkinCatalogAsync, LoadItemAssetsAsync, LoadChampionIdsAsync,
            LoadSkinlinesAsync);

        private CatalogSnapshot CurrentCatalogs
        {
            get
            {
                CatalogSnapshot current = Volatile.Read(ref _catalogs);
                if (string.Equals(current.ArchiveRevision, _source.ArchiveRevision,
                    StringComparison.Ordinal)) return current;
                lock (_catalogLock)
                {
                    current = _catalogs;
                    if (!string.Equals(current.ArchiveRevision, _source.ArchiveRevision,
                        StringComparison.Ordinal))
                        Volatile.Write(ref _catalogs, current = CreateSnapshot());
                    return current;
                }
            }
        }

        public async Task<string> FindIconAsync(string type, string lookup)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(lookup))
                return null;
            string normalizedType = type.Trim().ToLowerInvariant();
            string name = lookup.Trim();
            if (normalizedType == "generic")
                return null;

            try
            {
                return normalizedType switch
                {
                    "champion" => await FindChampionAsync(name, CurrentCatalogs),
                    "region" => await FindGroupOrRegionAsync(name, CurrentCatalogs),
                    "skinline" => await FindSkinlineAsync(name, CurrentCatalogs),
                    "item" => await FindItemAsync(name, CurrentCatalogs),
                    "monster" => await FindMonsterAsync(name),
                    "structure" => await FindStructureAsync(name),
                    "system" => await FindSystemAsync(name),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Local game icon '{normalizedType}:{name}' unavailable: {ex.Message}");
                return null;
            }
        }

        public async Task<string> FindSplashAsync(string champion, int skinId)
        {
            if (string.IsNullOrWhiteSpace(champion) || skinId < 0)
                return null;
            string id = _aliases.GetInternalName(champion);
            string key = $"{id}Skin{skinId}";
            CatalogSnapshot catalogs = CurrentCatalogs;
            return await FindSkinAssetAsync(catalogs, "splash", key, id, skinId,
                "splash", $"{id}_{skinId}.jpg", $"{id}_{skinId}.png");
        }

        /// <summary>
        /// Lists only named splash assets whose virtual paths occur in the
        /// installed WAD index. The list is derived from Riot's skins.json,
        /// not a hardcoded or remotely downloaded champion/skin catalog.
        /// </summary>
        public async Task<IReadOnlyList<LocalSplashSkin>> GetAvailableSplashSkinsAsync(string champion)
        {
            if (string.IsNullOrWhiteSpace(champion)) return Array.Empty<LocalSplashSkin>();
            string name = NormalizeChampion(_aliases.GetInternalName(champion));
            SkinCatalog skins = await CurrentCatalogs.Skins.Value;
            if (!skins.SplashSkins.TryGetValue(name, out var options))
                return Array.Empty<LocalSplashSkin>();
            return options.Where(skin =>
                skins.SplashPaths.TryGetValue(name + ":" + skin.SkinId, out var paths) &&
                paths.Any(HasInstalledSplashPath)).ToArray();
        }

        private bool HasInstalledSplashPath(string candidate)
        {
            string path = LeagueWadHashResolver.NormalizeVirtualPath(candidate);
            if (path == null) return false;
            // Riot game-data images normally live inside the plugin WAD, not
            // directly at assets/..., so check the same exact locations as the
            // semantic WAD resolver without registering a fake icon.
            return _source.ContainsAssetPath(path) ||
                (path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) &&
                 _source.ContainsAssetPath(GameAssets + path));
        }

        private async Task<string> FindChampionAsync(string name, CatalogSnapshot catalogs)
        {
            var skinMatch = Regex.Match(name, @"^([A-Za-z0-9]+)(?:Skin|_)(\d+)$", RegexOptions.IgnoreCase);
            if (skinMatch.Success)
            {
                if (!int.TryParse(skinMatch.Groups[2].Value, out int skinId)) return null;
                string id = _aliases.GetInternalName(skinMatch.Groups[1].Value);
                string key = $"{id}Skin{skinId}";
                return await FindSkinAssetAsync(catalogs, "champion", key, id, skinId,
                    "tiles", $"{id}_{skinId}.jpg", $"{id}_{skinId}.png");
            }

            var group = await FindGroupOrRegionAsync(name, catalogs);
            if (group != null) return group;
            string internalName = _aliases.GetInternalName(name);
            ChampionCatalog champions = await catalogs.Champions.Value;
            int? championId = champions.Ids.TryGetValue(internalName, out int currentId)
                ? currentId
                : champions.HasWadMetadata ? null : await _hashes.GetChampionIdAsync(internalName);
            if (championId is > 0)
                _hashes.Register("champion", catalogs.Lookup(internalName),
                    $"v1/champion-icons/{championId.Value}.png");
            string current = await _source.FindIconAsync("champion", catalogs.Lookup(internalName));
            return current ?? await FindCachedAsync("champion", internalName + ".png");
        }

        private async Task<string> FindGroupOrRegionAsync(string name, CatalogSnapshot catalogs)
        {
            var group = _groups.Groups.FirstOrDefault(g =>
                g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (group != null)
            {
                if (group.Category.Equals("Region", StringComparison.OrdinalIgnoreCase))
                {
                    string crest = name.Trim().Replace(' ', '_') + "_Crest_icon.png";
                    string cached = await FindCachedAsync("region", crest, name + "_icon.png");
                    if (cached != null) return cached;
                    RegisterGenericCandidates("region", name);
                    string wadCrest = await _source.FindIconAsync("region", name);
                    if (wadCrest != null) return wadCrest;
                }

                foreach (string member in group.GetChampionsList()
                    .Where(n => !n.Equals(name, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                {
                    // Group membership describes champions. Avoid unbounded
                    // recursion if a custom group mistakenly lists another group.
                    if (_groups.Groups.Any(other =>
                        other.Name.Equals(member, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    string found = await FindChampionAsync(member, catalogs);
                    if (found != null) return found;
                }
            }

            return await FindSkinlineAsync(name, catalogs);
        }

        private async Task<string> FindSkinlineAsync(string name, CatalogSnapshot catalogs)
        {
            // Resolve a line or an official colon-qualified family (e.g.
            // Arcade -> Arcade: Heroes + Arcade: Battle Bosses) from Riot's
            // authoritative skins.json/skinlines.json membership. Never scan
            // arbitrary WAD hashes for a text substring.
            bool qualified = LeagueSkinlineIdentity.TrySplitChampionQualified(
                name, out string targetChampion, out string qualifiedTheme);
            string lineQuery = qualified ? qualifiedTheme : name;
            string canonicalChampion = qualified
                ? _aliases.GetInternalName(targetChampion) : null;
            var allLines = await catalogs.Skinlines.Value;
            // If Riot later ships a distinct official "True Damage X" line,
            // prefer that exact identity over our backwards-compatible VO
            // suffix alias to the older "True Damage" line.
            var exactLines = allLines
                .Where(line => LeagueSkinlineIdentity.Normalize(lineQuery) ==
                    LeagueSkinlineIdentity.Normalize(line.Name)).ToList();
            var matchingLines = exactLines.Count > 0
                ? exactLines
                : allLines.Where(line => LeagueSkinlineIdentity.Matches(lineQuery, line.Name));
            foreach (var line in matchingLines.OrderBy(line => line.Id))
            {
                foreach (var skin in line.Skins
                    .Where(skin => !qualified ||
                        string.Equals(_aliases.GetInternalName(skin.Champion),
                            canonicalChampion, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(s => s.Champion, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.SkinId))
                {
                    string result = await FindChampionAsync($"{skin.Champion}Skin{skin.SkinId}", catalogs);
                    if (result != null) return result;
                }
            }
            return null;
        }

        private async Task<string> FindItemAsync(string name, CatalogSnapshot catalogs)
        {
            // "Kill3DWard" is an object interaction, not the name of a shop item.
            // Use the actual Warding Totem asset (3340) from the installed
            // items.json; do not pretend an item called "Ward" exists.
            if (name.Equals("Ward", StringComparison.OrdinalIgnoreCase))
                name = "3340";
            string id = name;
            ItemCatalog items = await catalogs.Items.Value;
            string resolvedId = RegisterItemMetadata(catalogs, items, name);
            if (string.IsNullOrEmpty(resolvedId) && !items.HasWadMetadata)
            {
                id = _offlineSkins.GetItemId(name)?.ToString() ??
                     await _data.ResolveItemNameToIdAsync(name) ?? name;
                resolvedId = RegisterItemMetadata(catalogs, items, id);
            }
            if (!string.IsNullOrEmpty(resolvedId)) id = resolvedId;
            if (items.HasWadMetadata)
                return resolvedId == null ? null :
                    await _source.FindIconAsync("item", catalogs.Lookup(id));

            string cached = await FindCachedAsync("item", id + ".png", name + ".png", name + "_item.png");
            if (cached != null) return cached;
            return await _source.FindIconAsync("item", catalogs.Lookup(id));
        }

        private async Task<string> FindMonsterAsync(string name)
        {
            // These HUD square icons have been verified against installed
            // Game WAD hashes. Generic assets/ui/monster/<name> paths never
            // existed; use actual game resources, keeping the legacy cache
            // lookup for users whose installed version lacks the current path.
            string normalized = new string(name.Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray());
            if (LeagueMonsterAssets.TryGetPath(name, out string path) &&
                _source.ContainsAssetPath(path))
            {
                _hashes.Register("monster", name, path);
                string fromWad = await _source.FindIconAsync("monster", name);
                if (fromWad != null) return fromWad;
            }
            // A new entry in Event Mapping is not a new compiled-in asset.
            // The WAD is a hash table with no original paths; try deterministic
            // character/HUD conventions generated from the configured name.
            // Only register candidates whose xxHash64 actually exists locally.
            // This covers newly added monsters following Riot's regular
            // folder + <monster>_square naming pattern without code changes.
            // Non-standard Riot paths still require an explicit asset mapping.
            foreach (string candidate in MonsterHudCandidates(normalized))
            {
                if (!_source.ContainsAssetPath(candidate)) continue;
                _hashes.Register("monster", name, candidate);
            }
            string conventionIcon = await _source.FindIconAsync("monster", name);
            if (conventionIcon != null) return conventionIcon;
            return await FindNamedAsync("monster", name, name + "Square.png",
                name + "_Square.png", name + ".png");
        }

        private static IEnumerable<string> MonsterHudCandidates(string normalized)
        {
            if (normalized.Length == 0) yield break;
            // These are *candidate* virtual paths, not hashes interpreted as
            // paths. Preserve the provided monster identity, including
            // underscores, and test against the installed archive index.
            string[] identifiers = { normalized, "sru_" + normalized };
            foreach (string folder in identifiers)
                foreach (string stem in identifiers)
                    foreach (string ext in new[] { "tex", "dds", "png" })
                        yield return $"assets/characters/{folder}/hud/{stem}_square.{ext}";
        }

        private async Task<string> FindSystemAsync(string name)
        {
            string normalized = new string(name.Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray());
            // Read actual ping textures from Game/DATA/FINAL/Global.wad.client.
            // These paths have been checked against the locally installed WAD
            // hash index; unlike assets/ui/system/<name>, they exist in-game.
            string path = normalized switch
            {
                "danger" => "assets/shared/particles/ping-danger.tex",
                // The minimap HUD uses this blue downward ping, not the large
                // green directional particle previously shown in event videos.
                "onmyway" or "omw" => "assets/ux/minimap/pings/on_my_way_new.tex",
                "assistme" => "assets/shared/particles/pingupdate/need/assist_me.tex",
                "enemymissing" or "mia" =>
                    "assets/shared/particles/pingupdate/need/enemy_missing.tex",
                "target" => "assets/shared/particles/pingupdate/need/target.tex",
                "caution" => "assets/shared/particles/pingupdate/need/caution.tex",
                "allin" => "assets/ux/minimap/pings/all_in.tex",
                "bait" => "assets/ux/minimap/pings/bait.tex",
                "hold" => "assets/ux/minimap/pings/hold.tex",
                "push" => "assets/ux/minimap/pings/push.tex",
                "retreat" => "assets/ux/minimap/pings/retreat.tex",
                _ => null
            };
            if (path != null)
            {
                _hashes.Register("system", name, path);
                // Old game builds can lack the minimap texture. Retain the
                // verified particle asset as an ordered secondary candidate.
                if (normalized is "onmyway" or "omw")
                    _hashes.Register("system", name,
                        "assets/shared/particles/ping-omw-arrow.tex");
                string fromWad = await _source.FindIconAsync("system", name);
                if (fromWad != null) return fromWad;
            }
            return await FindNamedAsync("system", name, name + "_icon.png",
                name + "_ping.png", name + ".png");
        }

        private async Task<string> FindStructureAsync(string name)
        {
            string assetName = name.Contains("inhibitor", StringComparison.OrdinalIgnoreCase)
                ? "Blue_Inhibitor" : name.Contains("nexus", StringComparison.OrdinalIgnoreCase)
                ? "Blue_Nexus" : name.Contains("turret", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("tower", StringComparison.OrdinalIgnoreCase)
                ? "Blue_Turret" : name;
            string verifiedPath = assetName switch
            {
                "Blue_Turret" => "assets/characters/turret/hud/turret_blue_square.tex",
                "Blue_Nexus" => "assets/characters/nexus/hud/nexus_blue_square.tex",
                _ => null
            };
            if (verifiedPath != null)
            {
                _hashes.Register("structure", assetName, verifiedPath);
                string fromWad = await _source.FindIconAsync("structure", assetName);
                if (fromWad != null) return fromWad;
            }
            return await FindNamedAsync("structure", assetName, assetName + "_icon.png", assetName + ".png");
        }

        private async Task<string> FindNamedAsync(string category, string lookup, params string[] cacheFiles)
        {
            string cached = await FindCachedAsync(category, cacheFiles);
            if (cached != null) return cached;
            RegisterGenericCandidates(category, lookup);
            return await _source.FindIconAsync(category, lookup);
        }

        private async Task<string> FindCachedAsync(string category, params string[] names)
        {
            foreach (string name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                string path = Path.Combine(_cacheRoot, category, name);
                if (await DataFetcher.IsUsableCachedIconAsync(path)) return path;
            }
            return null;
        }

        private string RegisterItemMetadata(CatalogSnapshot catalogs, ItemCatalog items, string itemLookup)
        {
            if (!items.Items.TryGetValue(itemLookup, out ItemAsset match))
                return null;
            _hashes.Register("item", catalogs.Lookup(match.Id), match.Path);
            return match.Id;
        }

        private async Task<ItemCatalog> LoadItemAssetsAsync()
        {
            var items = new Dictionary<string, ItemAsset>(StringComparer.OrdinalIgnoreCase);
            byte[] data = await _source.ReadAssetBytesAsync(GameData + "items.json");
            if (data == null) return new ItemCatalog(false, items);
            try
            {
                using var document = JsonDocument.Parse(data);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return new ItemCatalog(false, items);
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object ||
                        !item.TryGetProperty("id", out var id) ||
                        !item.TryGetProperty("iconPath", out var path) ||
                        path.ValueKind != JsonValueKind.String)
                        continue;

                    string itemId = id.ToString();
                    if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(path.GetString()))
                        continue;
                    var asset = new ItemAsset(itemId, path.GetString());
                    items.TryAdd(itemId, asset);
                    foreach (string field in new[] { "name", "nameSlug" })
                    {
                        if (item.TryGetProperty(field, out var title) &&
                            title.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(title.GetString()))
                            items.TryAdd(title.GetString(), asset);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug($"Cannot inspect local items.json: {ex.Message}");
                return new ItemCatalog(false, items);
            }
            return new ItemCatalog(true, items);
        }

        private async Task<ChampionCatalog> LoadChampionIdsAsync()
        {
            var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            byte[] data = await _source.ReadAssetBytesAsync(GameData + "champion-summary.json");
            if (data == null) return new ChampionCatalog(false, ids);
            try
            {
                using var document = JsonDocument.Parse(data);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return new ChampionCatalog(false, ids);
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object ||
                        !item.TryGetProperty("id", out var id) ||
                        id.ValueKind != JsonValueKind.Number ||
                        !id.TryGetInt32(out int number) || number <= 0)
                        continue;

                    foreach (string field in new[] { "alias", "name" })
                        if (item.TryGetProperty(field, out var value) &&
                            value.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(value.GetString()))
                            ids.TryAdd(value.GetString(), number);
                }
                return new ChampionCatalog(true, ids);
            }
            catch (JsonException ex)
            {
                _logger.LogDebug($"Cannot inspect local champion-summary.json: {ex.Message}");
                return new ChampionCatalog(false, ids);
            }
        }

        private async Task<SkinCatalog> LoadSkinCatalogAsync()
        {
            var paths = new Dictionary<string, SkinPaths>(StringComparer.OrdinalIgnoreCase);
            var names = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            var members = new Dictionary<int, List<OfflineSkinReference>>();
            byte[] data = await _source.ReadAssetBytesAsync(GameData + "skins.json");
            if (data == null)
                return EmptySkins();

            try
            {
                using var document = JsonDocument.Parse(data);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return EmptySkins();

                foreach (var entry in document.RootElement.EnumerateObject())
                {
                    if (entry.Value.ValueKind != JsonValueKind.Object) continue;
                    AddSkinPaths(paths, names, members, entry.Value);
                    if (!entry.Value.TryGetProperty("questSkinInfo", out var quest) ||
                        quest.ValueKind != JsonValueKind.Object ||
                        !quest.TryGetProperty("tiers", out var tiers) ||
                        tiers.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var tier in tiers.EnumerateArray())
                        if (tier.ValueKind == JsonValueKind.Object)
                            AddSkinPaths(paths, names, members, tier);
                }

                return new SkinCatalog(true, paths.ToDictionary(pair => pair.Key,
                    pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
                    paths.ToDictionary(pair => pair.Key,
                        pair => (IReadOnlyList<string>)pair.Value.Splashes
                            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                        StringComparer.OrdinalIgnoreCase),
                    names.ToDictionary(pair => pair.Key,
                        pair => (IReadOnlyList<LocalSplashSkin>)pair.Value
                            .OrderBy(skin => skin.Key)
                            .Select(skin => new LocalSplashSkin(skin.Key, skin.Value)).ToArray(),
                        StringComparer.OrdinalIgnoreCase),
                    members.ToDictionary(pair => pair.Key,
                        pair => (IReadOnlyList<OfflineSkinReference>)pair.Value.ToArray()));
            }
            catch (JsonException ex)
            {
                _logger.LogDebug($"Cannot inspect local WAD skins.json: {ex.Message}");
                return EmptySkins();
            }
        }

        private static SkinCatalog EmptySkins() => new(false,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyList<LocalSplashSkin>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<int, IReadOnlyList<OfflineSkinReference>>());

        private async Task<IReadOnlyList<OfflineSkinline>> LoadSkinlinesAsync(SkinCatalog skins)
        {
            if (!skins.HasWadMetadata) return _offlineSkins.GetSkinlines();
            byte[] data = await _source.ReadAssetBytesAsync(GameData + "skinlines.json");
            if (data == null) return _offlineSkins.GetSkinlines();
            try
            {
                using var document = JsonDocument.Parse(data);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return _offlineSkins.GetSkinlines();

                var lines = new List<OfflineSkinline>();
                foreach (var line in document.RootElement.EnumerateArray())
                {
                    if (line.ValueKind != JsonValueKind.Object ||
                        !line.TryGetProperty("id", out var idValue) ||
                        !TryReadInteger(idValue, out int id) ||
                        !line.TryGetProperty("name", out var nameValue) ||
                        nameValue.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(nameValue.GetString())) continue;

                    IReadOnlyList<OfflineSkinReference> members =
                        skins.SkinlineMembers.TryGetValue(id, out var current)
                            ? current : Array.Empty<OfflineSkinReference>();
                    lines.Add(new OfflineSkinline(id, nameValue.GetString(),
                        members.OrderBy(s => s.Champion, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(s => s.SkinId).ToArray()));
                }
                return lines.OrderBy(line => line.Id).ToArray();
            }
            catch (JsonException ex)
            {
                _logger.LogDebug($"Cannot inspect local WAD skinlines.json: {ex.Message}");
                return _offlineSkins.GetSkinlines();
            }
        }

        private static void AddSkinPaths(Dictionary<string, SkinPaths> paths,
            Dictionary<string, Dictionary<int, string>> names,
            Dictionary<int, List<OfflineSkinReference>> members, JsonElement record)
        {
            Add("tilePath", isTile: true);
            Add("splashPath", isTile: false);

            if (!record.TryGetProperty("skinLines", out var lines) ||
                lines.ValueKind != JsonValueKind.Array) return;
            if (!TryGetSkinIdentity("splashPath", out var champion, out int skinId) &&
                !TryGetSkinIdentity("tilePath", out champion, out skinId)) return;

            foreach (var line in lines.EnumerateArray())
            {
                JsonElement idValue = line;
                if (line.ValueKind == JsonValueKind.Object &&
                    !line.TryGetProperty("id", out idValue)) continue;
                if (!TryReadInteger(idValue, out int id)) continue;

                if (!members.TryGetValue(id, out var skinList))
                    members[id] = skinList = new List<OfflineSkinReference>();
                if (!skinList.Any(s => s.SkinId == skinId &&
                    s.Champion.Equals(champion, StringComparison.OrdinalIgnoreCase)))
                    skinList.Add(new OfflineSkinReference(champion, skinId));
            }

            void Add(string field, bool isTile)
            {
                if (!record.TryGetProperty(field, out var value) ||
                    value.ValueKind != JsonValueKind.String) return;
                string path = OfflineSkinCatalog.NormalizeAssetPath(value.GetString());
                if (path == null) return;
                Match match = SkinDirectory.Match(path);
                if (!match.Success) return;
                int skinId = 0;
                if (match.Groups[2].Success && !int.TryParse(match.Groups[2].Value, out skinId))
                    return;

                string champion = NormalizeChampion(match.Groups[1].Value);
                if (champion.Length == 0) return;
                string key = champion + ":" + skinId;
                if (!paths.TryGetValue(key, out var skin))
                    paths[key] = skin = new SkinPaths();
                (isTile ? skin.Tiles : skin.Splashes).Add(path);
                if (!isTile)
                {
                    if (!names.TryGetValue(champion, out var options))
                        names[champion] = options = new Dictionary<int, string>();
                    if (!options.ContainsKey(skinId))
                    {
                        string label = record.TryGetProperty("name", out var nameValue) &&
                            nameValue.ValueKind == JsonValueKind.String
                                ? nameValue.GetString() : null;
                        options[skinId] = string.IsNullOrWhiteSpace(label)
                            ? (skinId == 0 ? "Base" : $"Skin {skinId}")
                            : label;
                    }
                }
            }

            bool TryGetSkinIdentity(string field, out string champion, out int skinId)
            {
                champion = null;
                skinId = 0;
                if (!record.TryGetProperty(field, out var value) ||
                    value.ValueKind != JsonValueKind.String) return false;
                string path = OfflineSkinCatalog.NormalizeAssetPath(value.GetString());
                if (path == null) return false;
                Match match = SkinDirectory.Match(path);
                if (!match.Success || (match.Groups[2].Success &&
                    !int.TryParse(match.Groups[2].Value, out skinId))) return false;
                champion = match.Groups[1].Value;
                return true;
            }
        }

        private static bool TryReadInteger(JsonElement element, out int id)
        {
            id = 0;
            return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out id) ||
                element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out id);
        }

        private static string NormalizeChampion(string name) =>
            new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        private async Task<string> FindSkinAssetAsync(CatalogSnapshot catalogs,
            string type, string key, string champion, int skinId,
            string legacyCategory, params string[] legacyNames)
        {
            // LeagueWadAssetSource writes the converted PNG to a deterministic cache
            // filename. Concurrent requests for the same skin can race in File.Move,
            // making one caller skip the preferred tile and return its splash instead.
            // Serialize a semantic icon within its catalog generation; unrelated icons
            // still resolve concurrently and all awaiters share the same cached image.
            SemaphoreSlim gate = catalogs.IconGates.GetOrAdd(type + ":" + key,
                _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                bool hasWadMetadata = await RegisterSkinCandidatesAsync(
                    catalogs, type, key, champion, skinId).ConfigureAwait(false);
                if (hasWadMetadata)
                    return await _source.FindIconAsync(type, catalogs.Lookup(key))
                        .ConfigureAwait(false);

                string cached = await FindCachedAsync(legacyCategory, legacyNames)
                    .ConfigureAwait(false);
                return cached ?? await _source.FindIconAsync(type, catalogs.Lookup(key))
                    .ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<bool> RegisterSkinCandidatesAsync(CatalogSnapshot catalogs,
            string type, string key, string champion, int skinId)
        {
            string skinKey = NormalizeChampion(champion) + ":" + skinId;
            SkinCatalog skins = await catalogs.Skins.Value;
            IReadOnlyList<string> paths = catalogs.SkinPaths.GetOrAdd(type + ":" + skinKey, _ =>
                skins.HasWadMetadata
                    ? (type == "splash" ? skins.SplashPaths : skins.Paths)
                        .TryGetValue(skinKey, out var current) ? current : Array.Empty<string>()
                    : type == "splash"
                        ? _offlineSkins.GetSplashAssetPaths(champion, skinId)
                        : _offlineSkins.GetSkinAssetPaths(champion, skinId));

            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                _hashes.Register(type, catalogs.Lookup(key), path);
            }
            return skins.HasWadMetadata;
        }

        private void RegisterGenericCandidates(string category, string lookup)
        {
            string lower = lookup.Replace(' ', '_').ToLowerInvariant();
            foreach (string extension in new[] { "png", "tex", "dds" })
            {
                _hashes.Register(category, lookup, $"assets/ui/{category}/{lower}.{extension}");
                _hashes.Register(category, lookup, $"assets/ui/{category}/{lower}_icon.{extension}");
            }
        }
    }
}
