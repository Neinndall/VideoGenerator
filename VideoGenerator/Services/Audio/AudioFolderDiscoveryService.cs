using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Discovers renderable event folders and their audio inputs.
    /// This keeps filesystem conventions out of the dashboard view.
    /// </summary>
    public sealed class AudioFolderDiscoveryService
    {
        private static readonly Regex AudioFamilyPattern = new(
            @"^\[[^\]]+\](?:\s+.+)?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public IReadOnlyList<string> GetEventFolders(string rootDirectory)
        {
            var folders = new List<string>();
            IEnumerable<string> candidates;

            try
            {
                candidates = new[] { rootDirectory }
                    .Concat(Directory.EnumerateDirectories(rootDirectory, "*", SearchOption.AllDirectories));
            }
            catch (IOException)
            {
                return folders;
            }
            catch (UnauthorizedAccessException)
            {
                return folders;
            }

            foreach (string directory in candidates)
            {
                string directoryName = Path.GetFileName(directory);
                if (directoryName.Contains("_cast3D", StringComparison.OrdinalIgnoreCase) ||
                    IsAudioFamilyDirectory(directory))
                {
                    continue;
                }

                if (GetSupportedAudioFiles(directory).Count > 0 || GetAudioFamilies(directory).Count > 0)
                {
                    folders.Add(directory);
                }
            }

            return folders
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public List<string> GetSupportedAudioFiles(string directory)
        {
            try
            {
                return Directory.GetFiles(directory)
                    .Where(IsSupportedAudioFile)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (IOException)
            {
                return new List<string>();
            }
            catch (UnauthorizedAccessException)
            {
                return new List<string>();
            }
        }

        public List<AudioFamilyModel> GetAudioFamilies(string directory)
        {
            try
            {
                return Directory.GetDirectories(directory)
                    .Where(IsAudioFamilyDirectory)
                    .Select(familyDirectory => new AudioFamilyModel
                    {
                        Name = Path.GetFileName(familyDirectory),
                        AudioFiles = GetSupportedAudioFiles(familyDirectory)
                    })
                    .Where(family => family.AudioFiles.Count > 0)
                    .OrderBy(family => family.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (IOException)
            {
                return new List<AudioFamilyModel>();
            }
            catch (UnauthorizedAccessException)
            {
                return new List<AudioFamilyModel>();
            }
        }

        private static bool IsSupportedAudioFile(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAudioFamilyDirectory(string directory)
        {
            return AudioFamilyPattern.IsMatch(Path.GetFileName(directory));
        }
    }
}
