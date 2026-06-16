namespace VideoGenerator.Models
{
    public class ParsedEvent
    {
        public string OriginalFolder { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string IconLookupName { get; set; } = string.Empty;
        public string IconType { get; set; } = "generic"; // champion, item, monster, generic, region, structure, system
        public string IconPath { get; set; }
        public string Dialogue { get; set; } = string.Empty;
    }
}
