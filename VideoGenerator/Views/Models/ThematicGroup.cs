using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace VideoGenerator.Views.Models
{
    public partial class ThematicGroup : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _category = "Custom"; // Region, Skinline, Class, Race, etc.

        [ObservableProperty]
        private string _championsRaw = string.Empty; // Comma separated list for easy UI editing

        public List<string> GetChampionsList()
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(ChampionsRaw)) return list;
            
            foreach (var part in ChampionsRaw.Split(','))
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed)) list.Add(trimmed);
            }
            return list;
        }

        public bool IsOfficial { get; set; } = false;
    }
}
