using System;
using System.Collections.Generic;
using System.Linq;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Verified local Riot HUD artwork only. The broader built-in vocabulary
    /// belongs in DefaultMonsters (like DefaultRules); not every known monster
    /// has a reliably identified icon path in the installed WAD files.
    /// New official irregular asset paths can be added here separately.
    /// </summary>
    public static class LeagueMonsterAssets
    {
        private sealed record Entry(string Display, string Path, string[] Aliases);

        private static readonly Entry[] Entries =
        {
            new("Baron", "assets/characters/sru_baron/hud/baron_square.tex", new[] {"Baron"}),
            new("Baron Nashor", "assets/characters/sru_baron/hud/baron_square.tex", new[] {"BaronNashor"}),
            new("Dragon", "assets/characters/sru_dragon/hud/dragon_square.tex", new[] {"Dragon", "Drake"}),
            new("Elder Dragon", "assets/characters/sru_dragon_elder/hud/dragon_square_elder.tex", new[] {"ElderDragon", "ElderDrake"}),
            new("Rift Herald", "assets/characters/sru_riftherald/hud/sruriftherald_square.tex", new[] {"RiftHerald", "Herald"}),
            new("Atakhan", "assets/characters/sru_atakhan/hud/atakhan_r_square_128.tex", new[] {"Atakhan"}),
            new("Blue Sentinel", "assets/characters/sru_blue/hud/bluesentinel_square.tex", new[] {"BlueSentinel", "BlueBuff"}),
            new("Red Brambleback", "assets/characters/sru_red/hud/brambleback_square.tex", new[] {"RedBrambleback", "RedBuff"}),
            new("Gromp", "assets/characters/sru_gromp/hud/gromp_square.tex", new[] {"Gromp"}),
            // Smaller units are separate creatures: don't substitute their HUD
            // portraits with the large camp creature's verified icon.
            new("Ancient Krug", "assets/characters/sru_krug/hud/ancientkrug_square.tex", new[] {"AncientKrug", "Krugs"}),
            new("Razorbeak", "assets/characters/sru_razorbeak/hud/razorbeak_square.tex", new[] {"Razorbeak", "Raptors"}),
            new("Greater Murk Wolf", "assets/characters/sru_murkwolf/hud/greatermurkwolf_square.tex", new[] {"GreaterMurkWolf", "Wolves"}),
            new("Rift Scuttler", "assets/characters/sru_crab/hud/crab_square_0.tex", new[] {"RiftScuttler", "ScuttleCrab", "Scuttler"})
        };

        private static readonly IReadOnlyDictionary<string, Entry> ByName =
            Entries.SelectMany(entry => entry.Aliases.Append(entry.Display)
                .Select(alias => (Key: Normalize(alias), Entry: entry)))
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Entry,
                    StringComparer.OrdinalIgnoreCase);

        // Verified image owners, not the complete recognition vocabulary.
        // Event Mapping displays the merged DefaultMonsters/monsters.json.
        public static IReadOnlyList<string> OfficialNames { get; } =
            Array.AsReadOnly(Entries.Select(entry => entry.Display)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray());

        public static string Normalize(string name) =>
            new string((name ?? "").Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray());

        public static bool TryResolveName(string name, out string display)
        {
            if (ByName.TryGetValue(Normalize(name), out var entry))
            {
                display = entry.Display;
                return true;
            }
            display = null;
            return false;
        }

        public static bool TryGetPath(string name, out string path)
        {
            if (ByName.TryGetValue(Normalize(name), out var entry))
            {
                path = entry.Path;
                return true;
            }
            path = null;
            return false;
        }
    }
}
