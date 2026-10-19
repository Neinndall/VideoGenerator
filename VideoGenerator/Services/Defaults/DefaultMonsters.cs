using VideoGenerator.Views.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Versioned built-in neutral-monster vocabulary, just like DefaultRules.
    /// Recognizing a creature is NOT evidence that its exact HUD image exists:
    /// LeagueMonsterAssets separately declares verified WAD paths.
    ///
    /// Config/monsters.json holds the merged built-in and user entries.
    /// Update this source when Riot adds a new creature; merge without removing
    /// pre-existing personal names or their Epic/Large categorization.
    /// </summary>
    public static class DefaultMonsters
    {
        public enum MonsterKind { Epic, Large }

        public sealed record Entry(string Name, MonsterKind Kind, params string[] Aliases);

        private static readonly Entry[] BuiltIn =
        {
            // Epic objectives and named variants in Summoner's Rift VO.
            new("Baron", MonsterKind.Epic),
            new("Baron Nashor", MonsterKind.Epic),
            new("Dragon", MonsterKind.Epic, "Drake"),
            new("Cloud Drake", MonsterKind.Epic, "CloudDragon"),
            new("Infernal Drake", MonsterKind.Epic, "InfernalDragon"),
            new("Mountain Drake", MonsterKind.Epic, "MountainDragon"),
            new("Ocean Drake", MonsterKind.Epic, "OceanDragon"),
            new("Chemtech Drake", MonsterKind.Epic, "ChemtechDragon"),
            new("Hextech Drake", MonsterKind.Epic, "HextechDragon"),
            new("Elder Dragon", MonsterKind.Epic, "ElderDrake"),
            new("Rift Herald", MonsterKind.Epic, "Herald"),
            new("Void Grub", MonsterKind.Epic, "Voidgrub", "Voidgrubs"),
            new("Void Mite", MonsterKind.Epic, "Voidmite", "Voidmites"),
            new("Atakhan", MonsterKind.Epic),
            new("Ruinous Atakhan", MonsterKind.Epic, "AtakhanRuinous"),
            new("Voracious Atakhan", MonsterKind.Epic, "AtakhanVoracious"),

            // Jungle camps and their named smaller units.
            new("Blue Sentinel", MonsterKind.Large, "BlueBuff"),
            new("Red Brambleback", MonsterKind.Large, "RedBuff"),
            new("Gromp", MonsterKind.Large),
            new("Ancient Krug", MonsterKind.Large, "Krugs"),
            new("Krug", MonsterKind.Large),
            new("Lesser Krug", MonsterKind.Large),
            new("Razorbeak", MonsterKind.Large, "Raptors"),
            new("Raptor", MonsterKind.Large),
            new("Greater Murk Wolf", MonsterKind.Large, "Wolves"),
            new("Murk Wolf", MonsterKind.Large),
            new("Rift Scuttler", MonsterKind.Large, "Scuttler", "ScuttleCrab")
        };

        public static IReadOnlyList<Entry> Entries { get; } = Array.AsReadOnly(BuiltIn);

        private static readonly IReadOnlyDictionary<string, string> ByName =
            BuiltIn.SelectMany(entry => entry.Aliases.Append(entry.Name)
                .Select(alias => (Key: Normalize(alias), entry.Name)))
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Name,
                    StringComparer.OrdinalIgnoreCase);

        public static MonsterDatabase Get() => new()
        {
            Epic = BuiltIn.Where(entry => entry.Kind == MonsterKind.Epic)
                .Select(entry => entry.Name).ToList(),
            Large = BuiltIn.Where(entry => entry.Kind == MonsterKind.Large)
                .Select(entry => entry.Name).ToList()
        };

        public static bool TryResolveName(string name, out string officialName) =>
            ByName.TryGetValue(Normalize(name), out officialName);

        public static string Normalize(string name) =>
            new string((name ?? "").Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray());
    }
}
