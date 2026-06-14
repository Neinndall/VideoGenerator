using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Services.Parsers
{
    public class SpellOrAttackParser : IEventParser
    {
        public bool CanParse(string folderName)
        {
            string workingFolder = StripOwnerPrefix(folderName);
            return workingFolder.Contains("cast", StringComparison.OrdinalIgnoreCase) || 
                   workingFolder.Contains("hit", StringComparison.OrdinalIgnoreCase) ||
                   workingFolder.Contains("Attack", StringComparison.OrdinalIgnoreCase) ||
                   workingFolder.Contains("Spell", StringComparison.OrdinalIgnoreCase);
        }

        public Task<ParsedEvent> ParseAsync(string folderName, string language)
        {
            string workingFolder = StripOwnerPrefix(folderName);
            string cleanName = NormalizeFolderName(workingFolder);
            cleanName = Regex.Replace(cleanName, @"(?<!^)(?=[A-Z])", " ");
            cleanName = cleanName.Replace("_", " ");
            
            var words = cleanName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(w => char.ToUpper(w[0]) + w.Substring(1));
            
            string formattedText = string.Join(" ", words);

            return Task.FromResult(new ParsedEvent
            {
                OriginalFolder = folderName,
                DisplayText = formattedText,
                IconLookupName = "Generic",
                IconType = "generic"
            });
        }

        private string StripOwnerPrefix(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;
            var prefixMatch = Regex.Match(folderName, @"^(Play_vo_|Play_)([A-Za-z0-9]+?)(Skin\d+)?_", RegexOptions.IgnoreCase);
            if (prefixMatch.Success)
            {
                return folderName.Substring(prefixMatch.Length);
            }
            return folderName;
        }

        private string NormalizeFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;

            // Remove 2D / 3D Insensitively
            string normalized = Regex.Replace(folderName, @"\b(2D|3D)\b", "", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"2D|3D", "", RegexOptions.IgnoreCase);
            
            // Normalize double underscores or trailing/leading underscores
            normalized = Regex.Replace(normalized, @"_+", "_");
            normalized = normalized.Trim('_');

            return normalized;
        }
    }
}
