namespace VideoGenerator.Models
{
    public class ParsedEvent
    {
        public string OriginalFolder { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string IconLookupName { get; set; } = string.Empty;
        public string IconType { get; set; } = "generic"; // champion, item, monster, generic
        public string IconPath { get; set; }
    }
}
