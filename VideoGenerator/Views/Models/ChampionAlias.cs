using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace VideoGenerator.Views.Models
{
    public partial class ChampionAlias : ObservableObject
    {
        [ObservableProperty]
        private string _displayName = string.Empty; // e.g. Wukong

        [ObservableProperty]
        private string _internalName = string.Empty; // e.g. MonkeyKing

        // Riot champion-summary roles (such as fighter/tank). These are
        // champion classes, not player-selected positions like Jungle/ADC.
        public List<string> Roles { get; set; } = new();

        [JsonIgnore]
        public string RolesDisplay => string.Join(" / ", Roles ?? new List<string>());

        public bool IsOfficial { get; set; } = false;

        [JsonIgnore]
        public bool CanDelete => !IsOfficial;
    }
}
