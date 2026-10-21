using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using VideoGenerator.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Non-destructive, per-source-folder icon overrides for the Dashboard.
    /// Original user images, Riot caches and Event Mapping catalogs are never modified.
    /// The JSON index is intentionally separate from the official icon catalog.
    /// </summary>
    public sealed class ManualEventIconStore
    {
        private readonly string _indexPath;
        private readonly string _imagesDirectory;

        public ManualEventIconStore(string storageRoot = null)
        {
            string config = AppConfig.GetConfigDirectory(storageRoot);
            string iconCache = AppConfig.GetIconCacheDirectory(storageRoot);
            _indexPath = Path.Combine(config, "manual_event_icons.json");
            _imagesDirectory = Path.Combine(iconCache, "manual");
        }

        public string GetIconPath(string eventFolderPath)
        {
            string key = EventKey(eventFolderPath);
            var entries = ReadIndex();
            if (!entries.TryGetValue(key, out var name) || !IsSafeFileName(name))
                return null;

            string imagePath = Path.Combine(_imagesDirectory, name);
            return File.Exists(imagePath) ? imagePath : null;
        }

        public bool HasOverride(string eventFolderPath) =>
            GetIconPath(eventFolderPath) != null;

        public async Task<string> ImportAsync(
            string eventFolderPath, string sourceImagePath, CancellationToken cancellationToken = default)
        {
            string key = EventKey(eventFolderPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
            string sourcePath = Path.GetFullPath(sourceImagePath);
            var inputFile = new FileInfo(sourcePath);
            if (!inputFile.Exists || inputFile.Length is <= 0 or > 20 * 1024 * 1024)
                throw new InvalidDataException("Choose an existing image smaller than 20 MB.");

            // Read the index before creating anything. A malformed personal
            // JSON must never be silently replaced by an empty one.
            var entries = ReadIndex();
            var info = await Image.IdentifyAsync(sourcePath, cancellationToken);
            if (info == null || info.Width < 1 || info.Height < 1 ||
                info.Width > 4096 || info.Height > 4096 ||
                (long)info.Width * info.Height > 16_000_000)
                throw new InvalidDataException("The icon image has invalid or oversized dimensions.");

            using var image = await Image.LoadAsync<Rgba32>(sourcePath, cancellationToken);
            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Crop,
                Size = new Size(512, 512)
            }));
            cancellationToken.ThrowIfCancellationRequested();

            Directory.CreateDirectory(_imagesDirectory);
            string newName = Guid.NewGuid().ToString("N") + ".png";
            string outputPath = Path.Combine(_imagesDirectory, newName);
            string pendingImage = outputPath + ".tmp";
            try
            {
                await image.SaveAsPngAsync(pendingImage, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(pendingImage, outputPath);

                entries[key] = newName;
                SaveIndex(entries);
                return outputPath;
            }
            catch
            {
                if (File.Exists(pendingImage)) File.Delete(pendingImage);
                // An unindexed image is safe to discard; previous overrides
                // and the caller's original file are untouched.
                if (File.Exists(outputPath)) File.Delete(outputPath);
                throw;
            }
        }

        public void RemoveOverride(string eventFolderPath)
        {
            string key = EventKey(eventFolderPath);
            var entries = ReadIndex();
            if (!entries.Remove(key)) return;
            // No cache/image deletion: other references and user data stay safe.
            SaveIndex(entries);
        }

        private Dictionary<string, string> ReadIndex()
        {
            if (!File.Exists(_indexPath))
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(_indexPath));
            if (entries == null)
                throw new InvalidDataException("Manual icon index is invalid; it was not overwritten.");
            return new Dictionary<string, string>(entries, StringComparer.OrdinalIgnoreCase);
        }

        private void SaveIndex(Dictionary<string, string> entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_indexPath)!);
            string temporaryPath = _indexPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath,
                    JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporaryPath, _indexPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static bool IsSafeFileName(string fileName) =>
            !string.IsNullOrWhiteSpace(fileName) &&
            fileName == Path.GetFileName(fileName) &&
            fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        private static string EventKey(string folderPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
            string normalized = Path.GetFullPath(folderPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        }
    }
}
