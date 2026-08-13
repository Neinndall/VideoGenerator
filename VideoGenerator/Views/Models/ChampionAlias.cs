using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoGenerator.Views.Models
{
    public partial class ChampionAlias : ObservableObject
    {
        [ObservableProperty]
        private string _displayName = string.Empty; // e.g. Wukong

        [ObservableProperty]
        private string _internalName = string.Empty; // e.g. MonkeyKing

        public bool IsOfficial { get; set; } = false;
    }
}
