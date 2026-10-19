using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LeagueToolkit.Core.Renderer;
using LeagueToolkit.Core.Wad;
using LeagueToolkit.Hashing;
using LeagueToolkit.Toolkit;
using SixLabors.ImageSharp;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Offline asset source for game and plugin WAD archives. WAD entries contain path hashes,
    /// not their original filenames: callers must supply a real asset path or hash to read them.
    /// </summary>
    public sealed class LeagueWadAssetSource
    {
        private const int MaxAssetBytes = 64 * 1024 * 1024;
        private readonly LogService _logger;
        private readonly string _iconCacheDirectory;
        private readonly Func<string> _protectedBackgroundPath;
        private readonly object _indexLock = new();
        private readonly object _refreshLock = new();
        private readonly object _mappingLock = new();
        private readonly Dictionary<string, List<ulong>> _iconMappings = new(StringComparer.OrdinalIgnoreCase);
        private ArchiveSnapshot _archiveSnapshot;

        private sealed class ArchiveSnapshot
        {
            public ArchiveSnapshot(IReadOnlyList<string> wadFiles, string revision)
            {
                WadFiles = wadFiles;
                Revision = revision;
            }

            public IReadOnlyList<string> WadFiles { get; }
            public string Revision { get; }
            public Dictionary<ulong, List<string>> Index;
        }

        public LeagueWadAssetSource(LogService logger, string gameDirectory = null,
            string iconCacheDirectory = null, Func<string> protectedBackgroundPath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            GameDirectory = ResolveGameDirectory(gameDirectory);
            _protectedBackgroundPath = protectedBackgroundPath ??
                (() => AppSettings.Instance.CustomBackgroundPath);
            _iconCacheDirectory = string.IsNullOrWhiteSpace(iconCacheDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VideoGenerator", "Cache", "IconCache")
                : Path.GetFullPath(iconCacheDirectory);
            _archiveSnapshot = DiscoverArchiveSnapshot();
        }

        /// <summary>Root used to search both GAME and PLUGINS; null when no client is located.</summary>
        public string GameDirectory { get; }

        /// <summary>Current WAD and WAD.CLIENT paths. No archives are kept open.</summary>
        public IReadOnlyList<string> WadFiles => Volatile.Read(ref _archiveSnapshot).WadFiles;

        public bool IsAvailable => WadFiles.Count > 0;

        /// <summary>
        /// Stable fingerprint of the discovered WAD paths, sizes and filesystem timestamps.
        /// Identical installations produce the same value across application restarts.
        /// </summary>
        public string ArchiveRevision => Volatile.Read(ref _archiveSnapshot).Revision;

        /// <summary>
        /// Checks Game and Plugins for added, removed or replaced WAD archives. An updated
        /// snapshot invalidates the lazy hash index and changes the icon cache identity.
        /// Call at startup or before a new scan, not for every icon request.
        /// </summary>
        public bool RefreshArchives()
        {
            // Serialize explicit refreshes so an older scan cannot replace a newer one.
            lock (_refreshLock)
            {
                ArchiveSnapshot updated = DiscoverArchiveSnapshot();
                lock (_indexLock)
                {
                    if (updated.Revision == _archiveSnapshot.Revision)
                        return false;

                    // The icon manager registers patch-specific mappings under
                    // @catalogN keys. Their obsolete generations must not accumulate.
                    // Keep caller-registered stable keys: their path hashes may still
                    // exist in the new WAD and callers need not register them again.
                    lock (_mappingLock)
                    {
                        foreach (string key in _iconMappings.Keys
                            .Where(key => key.Contains("@catalog", StringComparison.OrdinalIgnoreCase))
                            .ToArray())
                            _iconMappings.Remove(key);
                    }
                    Volatile.Write(ref _archiveSnapshot, updated);
                    return true;
                }
            }
        }

        /// <summary>
        /// Exposes a bounded sample of known entry hashes for diagnostics and local catalog
        /// matching. WAD descriptors do not include reversible original paths.
        /// </summary>
        public IReadOnlyList<ulong> GetAvailableAssetHashes(int limit = 128)
        {
            if (limit <= 0) return Array.Empty<ulong>();
            var snapshot = Volatile.Read(ref _archiveSnapshot);
            return EnsureArchiveIndex(snapshot, CancellationToken.None).Keys
                .OrderBy(hash => hash).Take(limit).ToArray();
        }

        /// <summary>
        /// Checks a candidate virtual path directly against the local WAD index.
        /// Hashes identify chunks but do NOT encode reversible filenames. A
        /// path must come from game metadata or a known naming convention.
        /// No image is extracted and no network/hash dictionary is consulted.
        /// </summary>
        public bool ContainsAssetPath(string virtualPath)
        {
            if (string.IsNullOrWhiteSpace(virtualPath)) return false;
            return EnsureArchiveIndex(Volatile.Read(ref _archiveSnapshot), CancellationToken.None)
                .ContainsKey(ComputePathHash(virtualPath));
        }

        /// <summary>
        /// Registers an exact Riot asset path for a semantic icon. Arbitrary paths are never guessed.
        /// If several assets are registered, they are tried in registration order until a valid image is found.
        /// </summary>
        public void RegisterIconAssetPath(string iconType, string iconLookup, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("A WAD asset path is required.", nameof(assetPath));
            RegisterIconAssetHash(iconType, iconLookup, ComputePathHash(assetPath));
        }

        /// <summary>Registers a known WAD hash without requiring a reverse hash dictionary.</summary>
        public void RegisterIconAssetHash(string iconType, string iconLookup, ulong assetHash)
        {
            string key = GetIconKey(iconType, iconLookup);
            lock (_mappingLock)
            {
                if (!_iconMappings.TryGetValue(key, out var hashes))
                    _iconMappings[key] = hashes = new List<ulong>();
                if (!hashes.Contains(assetHash))
                    hashes.Add(assetHash);
            }
        }

        public static ulong ComputePathHash(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("A WAD asset path is required.", nameof(assetPath));
            string normalized = assetPath.Trim().Replace('\\', '/').TrimStart('/').ToLowerInvariant();
            return XxHash64Ext.Hash(normalized);
        }

        /// <summary>Reads the decompressed bytes of the first matching entry in discovered archives.</summary>
        public Task<byte[]> ReadAssetBytesAsync(string assetPath, CancellationToken token = default) =>
            ReadAssetBytesAsync(ComputePathHash(assetPath), token);

        /// <summary>Reads by hash (including TEX, DDS, PNG, BIN, or any other WAD payload).</summary>
        public Task<byte[]> ReadAssetBytesAsync(ulong assetHash, CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
                return Task.FromCanceled<byte[]>(token);
            var snapshot = Volatile.Read(ref _archiveSnapshot);
            return ReadAssetBytesAsync(assetHash, snapshot, token);
        }

        private Task<byte[]> ReadAssetBytesAsync(ulong assetHash, ArchiveSnapshot snapshot,
            CancellationToken token) =>
            Task.Run(() => ReadAssetBytes(assetHash, snapshot, token), token);

        /// <summary>
        /// Returns a cached PNG path for a registered icon, or null if no registered asset is usable.
        /// JPEG/PNG/WebP are decoded by ImageSharp; League TEX/DDS use LeagueToolkit.Texture.
        /// All reads are local; no HTTP or Riot installation is required.
        /// </summary>
        public async Task<string> FindIconAsync(string iconType, string iconLookup, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(iconType) || string.IsNullOrWhiteSpace(iconLookup))
                return null;
            string key = GetIconKey(iconType, iconLookup);
            ulong[] candidates;
            lock (_mappingLock)
            {
                if (!_iconMappings.TryGetValue(key, out var hashes))
                    return null;
                candidates = hashes.ToArray();
            }

            while (true)
            {
                var snapshot = Volatile.Read(ref _archiveSnapshot);
                bool archivesChanged = false;
                // Catalog generations affect the registered hashes, not the stable
                // identity of the icon shown in the UI. Normalize them for storage.
                string stableKey = GetStableCacheKey(key);
                string cacheBase =
                    $"{XxHash64Ext.Hash(GameDirectory + ":" + stableKey):x16}";
                foreach (ulong hash in candidates)
                {
                    token.ThrowIfCancellationRequested();
                    // Include the installation and archive revision, so an updated source
                    // cannot return a PNG decoded from the previous Riot patch.
                    string cacheFile = Path.Combine(_iconCacheDirectory, "wad",
                        $"{cacheBase}.{hash:x16}.{snapshot.Revision}.png");
                    if (File.Exists(cacheFile) && IsValidPng(cacheFile))
                    {
                        if (ReferenceEquals(snapshot, Volatile.Read(ref _archiveSnapshot)))
                            return cacheFile;
                        archivesChanged = true;
                        break;
                    }

                    byte[] rawBytes = await ReadAssetBytesAsync(hash, snapshot, token)
                        .ConfigureAwait(false);
                    if (!ReferenceEquals(snapshot, Volatile.Read(ref _archiveSnapshot)))
                    {
                        archivesChanged = true;
                        break;
                    }
                    if (rawBytes == null)
                        continue;

                    byte[] pngBytes = ConvertToPng(rawBytes);
                    if (pngBytes == null)
                        continue;

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(cacheFile));
                        string tempFile = cacheFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
                        try
                        {
                            await File.WriteAllBytesAsync(tempFile, pngBytes, token).ConfigureAwait(false);
                            File.Move(tempFile, cacheFile, overwrite: true);
                        }
                        finally
                        {
                            if (File.Exists(tempFile)) File.Delete(tempFile);
                        }

                        if (!ReferenceEquals(snapshot, Volatile.Read(ref _archiveSnapshot)))
                        {
                            archivesChanged = true;
                            break;
                        }
                        // Keep recent versions for consumers still using an older
                        // URI, but bound the retained revisions per icon.
                        TrimIconCacheVersions(cacheBase, cacheFile);
                        return cacheFile;
                    }
                    catch (IOException ex)
                    {
                        _logger.LogDebug($"Failed to cache local WAD icon: {ex.Message}");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogDebug($"Failed to cache local WAD icon: {ex.Message}");
                    }
                }
                if (!archivesChanged) return null;
            }
        }

        /// <summary>Finds .wad and .wad.client under the install root, including Game and Plugins.</summary>
        public static IReadOnlyList<string> DiscoverWadFiles(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return Array.Empty<string>();

            var roots = new List<string> { Path.GetFullPath(directory) };
            if (new DirectoryInfo(directory).Name.Equals("Game", StringComparison.OrdinalIgnoreCase))
            {
                string plugins = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(directory)), "Plugins");
                if (Directory.Exists(plugins)) roots.Add(plugins);
            }

            var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                try
                {
                    var options = new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    };
                    foreach (string file in Directory.EnumerateFiles(root, "*", options))
                    {
                        if (file.EndsWith(".wad", StringComparison.OrdinalIgnoreCase) ||
                            file.EndsWith(".wad.client", StringComparison.OrdinalIgnoreCase))
                            files.Add(Path.GetFullPath(file));
                    }
                }
                catch (IOException) { /* Missing or changing installation: keep other roots. */ }
                catch (UnauthorizedAccessException) { }
            }
            return files.ToList();
        }

        private ArchiveSnapshot DiscoverArchiveSnapshot()
        {
            // Gather cheap filesystem metadata only during explicit refresh / construction.
            // Do not open WAD payloads or recursively search for every icon lookup.
            var files = new List<string>();
            var identity = new StringBuilder();
            identity.Append(GameDirectory?.ToUpperInvariant() ?? "<missing>").Append('\n');
            foreach (string path in DiscoverWadFiles(GameDirectory))
            {
                try
                {
                    var info = new FileInfo(path);
                    long size = info.Length;
                    long modified = info.LastWriteTimeUtc.Ticks;
                    long created = info.CreationTimeUtc.Ticks;
                    files.Add(path);
                    identity.Append(path.ToUpperInvariant()).Append('|')
                        .Append(size).Append('|').Append(modified).Append('|')
                        .Append(created).Append('\n');
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogDebug($"Cannot inspect WAD '{path}': {ex.Message}");
                }
            }

            string revision = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString())));
            return new ArchiveSnapshot(files.AsReadOnly(), revision);
        }

        private void TrimIconCacheVersions(string cacheBase, string activeFile)
        {
            try
            {
                string directory = Path.GetDirectoryName(activeFile);
                // This may be a manually selected WAD splash. A patch must never
                // delete the background the user chose, even after many revisions.
                string protectedPath = _protectedBackgroundPath();
                if (!string.IsNullOrWhiteSpace(protectedPath))
                {
                    try { protectedPath = Path.GetFullPath(protectedPath); }
                    catch (Exception ex) when (ex is ArgumentException or NotSupportedException or
                        PathTooLongException) { protectedPath = null; }
                }
                var versions = Directory.EnumerateFiles(directory, cacheBase + ".*.png")
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(info => info.LastWriteTimeUtc)
                    .ToList();

                // Keep the active version and two previous revisions. Old UI/exports
                // may still refer to their paths while an update is in progress.
                int kept = 0;
                foreach (var version in versions)
                {
                    if (!string.IsNullOrWhiteSpace(protectedPath) &&
                        string.Equals(version.FullName, protectedPath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (string.Equals(version.FullName, activeFile, StringComparison.OrdinalIgnoreCase) ||
                        kept < 2)
                    {
                        if (!string.Equals(version.FullName, activeFile, StringComparison.OrdinalIgnoreCase))
                            kept++;
                        continue;
                    }
                    try { version.Delete(); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private byte[] ReadAssetBytes(ulong assetHash, ArchiveSnapshot snapshot, CancellationToken token)
        {
            var index = EnsureArchiveIndex(snapshot, token);
            if (!index.TryGetValue(assetHash, out var archivePaths))
                return null;

            foreach (string archivePath in archivePaths)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    // Own the stream even if WadFile's constructor rejects corrupt metadata.
                    using var input = File.OpenRead(archivePath);
                    using var wad = new WadFile(input);
                    if (!wad.Chunks.TryGetValue(assetHash, out var chunk) ||
                        chunk.UncompressedSize < 0 || chunk.UncompressedSize > MaxAssetBytes)
                        continue;
                    using var raw = wad.LoadChunkDecompressed(chunk);
                    return raw.Span.ToArray();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                    InvalidDataException or ArgumentException or NotSupportedException or
                    ZstdSharp.ZstdException)
                {
                    _logger.LogDebug($"Cannot read WAD asset from '{archivePath}': {ex.Message}");
                }
            }
            return null;
        }

        private Dictionary<ulong, List<string>> EnsureArchiveIndex(
            ArchiveSnapshot snapshot, CancellationToken token)
        {
            var existing = Volatile.Read(ref snapshot.Index);
            if (existing != null) return existing;
            lock (_indexLock)
            {
                if (snapshot.Index != null) return snapshot.Index;
                var index = new Dictionary<ulong, List<string>>();
                foreach (string path in snapshot.WadFiles)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        using var input = File.OpenRead(path);
                        using var wad = new WadFile(input);
                        foreach (ulong hash in wad.Chunks.Keys)
                        {
                            if (!index.TryGetValue(hash, out var archives))
                                index[hash] = archives = new List<string>();
                            archives.Add(path);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                        InvalidDataException or ArgumentException or NotSupportedException)
                    {
                        _logger.LogDebug($"Skipping WAD '{path}': {ex.Message}");
                    }
                }
                Volatile.Write(ref snapshot.Index, index);
                return index;
            }
        }

        private static byte[] ConvertToPng(byte[] asset)
        {
            if (asset.Length < 4 || asset.Length > MaxAssetBytes) return null;
            try
            {
                using var output = new MemoryStream();
                if (asset.AsSpan(0, 4).SequenceEqual("TEX\0"u8) ||
                    asset.AsSpan(0, 4).SequenceEqual("DDS "u8))
                {
                    using var input = new MemoryStream(asset, writable: false);
                    Texture texture = Texture.Load(input);
                    if (texture.Mips.Length == 0) return null;
                    using var image = texture.Mips[0].ToImage();
                    image.SaveAsPng(output);
                }
                else
                {
                    using var image = Image.Load(asset);
                    image.SaveAsPng(output);
                }
                return output.ToArray();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or
                NotSupportedException or NotImplementedException or IOException or
                SixLabors.ImageSharp.UnknownImageFormatException or
                SixLabors.ImageSharp.InvalidImageContentException)
            {
                return null;
            }
        }

        private static bool IsValidPng(string file)
        {
            try
            {
                using var image = Image.Load(file);
                return image.Width > 0 && image.Height > 0;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                SixLabors.ImageSharp.UnknownImageFormatException or
                SixLabors.ImageSharp.InvalidImageContentException) { return false; }
        }

        private static string GetIconKey(string iconType, string iconLookup)
        {
            if (string.IsNullOrWhiteSpace(iconType) || string.IsNullOrWhiteSpace(iconLookup))
                throw new ArgumentException("Icon type and lookup must not be empty.");
            return iconType.Trim().ToLowerInvariant() + ":" + iconLookup.Trim().ToLowerInvariant();
        }

        private static string GetStableCacheKey(string registeredKey)
        {
            int marker = registeredKey.LastIndexOf("@catalog", StringComparison.Ordinal);
            return marker >= 0 &&
                long.TryParse(registeredKey.AsSpan(marker + "@catalog".Length), out _)
                ? registeredKey[..marker]
                : registeredKey;
        }

        private static string ResolveGameDirectory(string requested)
        {
            // Only load archives from a directory explicitly chosen and saved by
            // the user. Never fall back to environment variables or Riot paths.
            if (string.IsNullOrWhiteSpace(requested) || !Directory.Exists(requested))
                return null;

            return Path.GetFullPath(requested);
        }
    }
}
