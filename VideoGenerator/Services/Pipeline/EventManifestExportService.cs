using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Builds a portable event manifest without reading files or exporting audio or dialogue data.
    /// </summary>
    public static class EventManifestExportService
    {
        public static string CreateJson(string sourceFolder, string locale, IEnumerable<PreviewEventModel> analyzedEvents)
        {
            var events = (analyzedEvents ?? Enumerable.Empty<PreviewEventModel>())
                .Where(item => item != null)
                .Select(item =>
                {
                    string folderName = SafeFolderName(item.FolderName);
                    var parsed = item.ParsedData;

                    return new
                    {
                        folderName,
                        relativeFolder = GetRelativeFolder(sourceFolder, item.FolderPath, folderName),
                        champion = item.CharacterName ?? string.Empty,
                        audioCount = item.AudioFiles?.Count ?? 0,
                        audioFamilyCount = item.AudioFamilies?.Count ?? 0,
                        status = item.Status ?? string.Empty,
                        isMapped = parsed?.IsMapped ?? false,
                        displayText = parsed?.DisplayText ?? string.Empty,
                        iconType = parsed?.IconType ?? string.Empty,
                        iconLookupName = parsed?.IconLookupName ?? string.Empty
                    };
                })
                .OrderBy(item => item.relativeFolder, StringComparer.Ordinal)
                .ThenBy(item => item.folderName, StringComparer.Ordinal)
                .ThenBy(item => item.champion, StringComparer.Ordinal)
                .ThenBy(item => item.audioCount)
                .ThenBy(item => item.audioFamilyCount)
                .ThenBy(item => item.status, StringComparer.Ordinal)
                .ThenBy(item => item.isMapped)
                .ThenBy(item => item.displayText, StringComparer.Ordinal)
                .ThenBy(item => item.iconType, StringComparer.Ordinal)
                .ThenBy(item => item.iconLookupName, StringComparer.Ordinal)
                .ToList();

            return JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                locale = locale ?? string.Empty,
                events
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string SafeFolderName(string folderName)
        {
            // FolderName normally contains a leaf name. Guard against accidental absolute paths.
            string normalized = (folderName ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            int lastSeparator = normalized.LastIndexOf('/');
            return lastSeparator >= 0 ? normalized.Substring(lastSeparator + 1) : normalized;
        }

        private static string GetRelativeFolder(string sourceFolder, string folderPath, string folderName)
        {
            if (string.IsNullOrWhiteSpace(sourceFolder) || string.IsNullOrWhiteSpace(folderPath))
                return folderName;

            try
            {
                // Require absolute inputs so a relative path cannot be resolved against the
                // application's working directory and accidentally exported as an internal folder.
                if (!Path.IsPathFullyQualified(sourceFolder) || !Path.IsPathFullyQualified(folderPath))
                    return folderName;

                string relative = Path.GetRelativePath(
                    Path.GetFullPath(sourceFolder), Path.GetFullPath(folderPath)).Replace('\\', '/');

                // Path.GetRelativePath also produces a relative result for sibling folders.
                if (relative == "." || relative == ".." ||
                    relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                    return folderName;

                return relative;
            }
            catch (ArgumentException)
            {
                return folderName;
            }
            catch (NotSupportedException)
            {
                return folderName;
            }
            catch (PathTooLongException)
            {
                return folderName;
            }
        }
    }
}
