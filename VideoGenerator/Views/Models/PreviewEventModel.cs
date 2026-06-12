using VideoGenerator.Models;
using System.Collections.Generic;

namespace VideoGenerator.Views.Models
{
    public class PreviewEventModel
    {
        public string FolderPath { get; set; }
        public string FolderName { get; set; }
        public ParsedEvent ParsedData { get; set; }
        public List<string> AudioFiles { get; set; } = new();
        public string Status { get; set; } // "Ready", "Missing Icon", "No Audio"
        public string CharacterName { get; set; }
    }
}
