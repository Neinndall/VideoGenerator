using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoGenerator.Views.Models
{
    public enum RuleType
    {
        Simple,      // Exact or contains match, no target extraction (e.g. Recall)
        Target,      // Extracts a target name (e.g. Kill, Assist)
        Interaction  // Complex interaction with target (e.g. FirstEncounter)
    }

    public partial class EventRule : ObservableObject
    {
        [ObservableProperty]
        private string _keyword = "";

        [ObservableProperty]
        private string _translationKey = "";

        [ObservableProperty]
        private string _iconType = "generic"; // generic, champion, item, monster, region, structure

        [ObservableProperty]
        private string _iconLookup = "";

        [ObservableProperty]
        private bool _extractsTarget = false; // Legacy, usually determined by Type now

        [ObservableProperty]
        private RuleType _type = RuleType.Simple;
    }
}
