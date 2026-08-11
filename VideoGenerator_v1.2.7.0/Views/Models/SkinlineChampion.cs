namespace VideoGenerator.Views.Models
{
    public class SkinlineChampion
    {
        public string Name { get; set; } = string.Empty;
        public int SkinId { get; set; }

        public override string ToString() => SkinId > 0 ? $"{Name}Skin{SkinId}" : Name;
    }
}
