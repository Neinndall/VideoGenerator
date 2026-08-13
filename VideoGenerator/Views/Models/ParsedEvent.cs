namespace VideoGenerator.Models
{
    public class ParsedEvent
    {
        public string OriginalFolder { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        /// <summary>
        /// Indicates that a parser rule recognized the folder name. Generic fallback
        /// events deliberately set this to false so they can be surfaced as Needs Mapping.
        /// Manually-created parsed data remains mapped for backwards compatibility.
        /// </summary>
        public bool IsMapped { get; set; } = true;
        public string IconLookupName { get; set; } = string.Empty;
        public string IconType { get; set; } = "generic"; // champion, item, monster, generic, region, structure, system
        public string IconPath { get; set; }
        public string Dialogue { get; set; } = string.Empty;
    }
}
