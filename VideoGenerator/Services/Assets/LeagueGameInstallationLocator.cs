using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Lightweight filesystem-only inspection of a League of Legends installation.
    /// No WAD archive is opened and no network request is made.
    /// </summary>
    public static class LeagueGameInstallationLocator
    {
        /// <summary>
        /// Resolves a selected installation, Game, Game/DATA, Game/DATA/FINAL,
        /// Plugins, or Plugins/rcp-be-lol-game-data folder to the installation root.
        /// A valid root must contain both Game and Plugins directories.
        /// </summary>
        public static string FindInstallationRoot(string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath))
                return null;

            try
            {
                string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selectedPath.Trim()));
                if (!Directory.Exists(fullPath))
                    return null;

                var selected = new DirectoryInfo(fullPath);
                DirectoryInfo root = selected;

                // Prefer an installation root itself even if its name matches a
                // recognized child folder (for example an installation named Game).
                if (!HasInstallationDirectories(root.FullName))
                {
                    if (IsName(selected, "Game") || IsName(selected, "Plugins"))
                    {
                        root = selected.Parent;
                    }
                    else if (IsName(selected, "DATA") && IsName(selected.Parent, "Game"))
                    {
                        root = selected.Parent.Parent;
                    }
                    else if (IsName(selected, "FINAL") && IsName(selected.Parent, "DATA") &&
                             IsName(selected.Parent.Parent, "Game"))
                    {
                        root = selected.Parent.Parent.Parent;
                    }
                    else if (IsName(selected, "rcp-be-lol-game-data") &&
                             IsName(selected.Parent, "Plugins"))
                    {
                        root = selected.Parent.Parent;
                    }
                    else
                    {
                        return null;
                    }
                }

                return root != null && HasInstallationDirectories(root.FullName)
                    ? root.FullName
                    : null;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or
                                       PathTooLongException or IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Inspects local WAD file names on a worker thread without opening any WAD.
        /// A detected catalog is identified by default-assets.wad or default-assets2.wad
        /// in Plugins/rcp-be-lol-game-data, not by decoding archive contents.
        /// </summary>
        public static Task<GameInstallationScanResult> InspectAsync(
            string selectedPath, CancellationToken token = default)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                string root = FindInstallationRoot(selectedPath);
                if (root == null)
                    return new GameInstallationScanResult(null, 0, 0, false);

                token.ThrowIfCancellationRequested();
                var archives = LeagueWadAssetSource.DiscoverWadFiles(root);
                int gameCount = 0;
                int pluginCount = 0;

                foreach (string archive in archives)
                {
                    token.ThrowIfCancellationRequested();
                    string relativePath = Path.GetRelativePath(root, archive).Replace('\\', '/');
                    if (relativePath.StartsWith("Game/", StringComparison.OrdinalIgnoreCase))
                        gameCount++;
                    else if (relativePath.StartsWith("Plugins/", StringComparison.OrdinalIgnoreCase))
                        pluginCount++;
                }

                string gameDataPlugin = Path.Combine(root, "Plugins", "rcp-be-lol-game-data");
                bool hasCatalog =
                    File.Exists(Path.Combine(gameDataPlugin, "default-assets.wad")) ||
                    File.Exists(Path.Combine(gameDataPlugin, "default-assets2.wad"));

                token.ThrowIfCancellationRequested();
                return new GameInstallationScanResult(root, gameCount, pluginCount, hasCatalog);
            }, token);
        }

        private static bool IsName(DirectoryInfo directory, string expected) =>
            directory != null && directory.Name.Equals(expected, StringComparison.OrdinalIgnoreCase);

        private static bool HasInstallationDirectories(string path) =>
            Directory.Exists(Path.Combine(path, "Game")) &&
            Directory.Exists(Path.Combine(path, "Plugins"));
    }

    public sealed record GameInstallationScanResult(
        string Root,
        int GameWadCount,
        int PluginWadCount,
        bool HasGameDataCatalog)
    {
        public bool IsValid => Root != null &&
                               GameWadCount > 0 &&
                               PluginWadCount > 0 &&
                               HasGameDataCatalog;
    }
}
