using VideoGenerator.Views.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Built-in structure vocabulary, like DefaultRules and DefaultMonsters.
    /// These mappings seed the merged Config/structures.json on first run.
    /// An icon target is a semantic identity, not a
    /// guarantee that matching artwork exists in the installed WAD.
    /// </summary>
    public static class DefaultStructures
    {
        public sealed record Entry(string Keyword, string TargetName);

        private static readonly Entry[] BuiltIn =
        {
            new("Turret", "Turret"),
            new("Tower", "Turret"),
            new("BlueTurret", "Turret"),
            new("RedTurret", "Turret"),
            new("BlueTower", "Turret"),
            new("RedTower", "Turret"),
            new("OuterTurret", "Turret"),
            new("InnerTurret", "Turret"),
            new("InhibitorTurret", "Turret"),
            new("NexusTurret", "Turret"),
            new("Inhibitor", "Inhibitor"),
            new("BlueInhibitor", "Inhibitor"),
            new("RedInhibitor", "Inhibitor"),
            new("Nexus", "Nexus"),
            new("BlueNexus", "Nexus"),
            new("RedNexus", "Nexus")
        };

        public static IReadOnlyList<Entry> Entries { get; } = Array.AsReadOnly(BuiltIn);

        public static List<StructureMapping> Get() => BuiltIn.Select(entry =>
            new StructureMapping { Keyword = entry.Keyword, TargetName = entry.TargetName })
            .ToList();

        public static string Normalize(string name) => new string((name ?? "")
            .Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}
