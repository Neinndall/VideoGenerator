using System;
using System.Collections.Generic;
using System.Linq;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// DefaultMonsters is the bundled seed; monsters.json contains its
    /// combined, editable catalog, like groups.json and champion_aliases.json.
    /// Official rows remain protected in Event Mapping. Existing user category
    /// choices win during a merge.
    /// </summary>
    public enum MonsterCatalogSection { OfficialOnly, Epic, Large }

    public sealed record MonsterCatalogRow(string Name, bool IsOfficial,
        MonsterCatalogSection Section)
    {
        public bool CanDelete => !IsOfficial;
    }

    public static class MonsterCatalogEditor
    {
        public static IReadOnlyList<MonsterCatalogRow> CreateRows(MonsterDatabase database)
        {
            var byName = new Dictionary<string, MonsterCatalogRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in DefaultMonsters.Entries)
                byName[DefaultMonsters.Normalize(entry.Name)] =
                    new MonsterCatalogRow(entry.Name, true, MonsterCatalogSection.OfficialOnly);

            AddSaved(database?.Epic, MonsterCatalogSection.Epic);
            AddSaved(database?.Large, MonsterCatalogSection.Large);
            return byName.Values.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToArray();

            void AddSaved(IEnumerable<string> source, MonsterCatalogSection category)
            {
                foreach (string raw in source ?? Array.Empty<string>())
                {
                    string name = raw?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    // Historical files may contain an alias of a built-in
                    // (Drake/Dragon, BlueBuff/Blue Sentinel). Merge under its
                    // canonical name rather than persisting two copies.
                    string key = DefaultMonsters.TryResolveName(name, out string official)
                        ? DefaultMonsters.Normalize(official)
                        : DefaultMonsters.Normalize(name);
                    if (key.Length == 0) continue;
                    if (byName.TryGetValue(key, out var existing))
                    {
                        // User's category wins over the official-only presentation.
                        if (existing.Section == MonsterCatalogSection.OfficialOnly)
                            byName[key] = existing with { Section = category };
                    }
                    else
                    {
                        byName[key] = new MonsterCatalogRow(name, false, category);
                    }
                }
            }
        }

        public static MonsterDatabase ToDatabase(IEnumerable<MonsterCatalogRow> rows)
        {
            var database = new MonsterDatabase();
            var defaultCategories = DefaultMonsters.Entries.ToDictionary(
                entry => DefaultMonsters.Normalize(entry.Name), entry => entry.Kind);
            foreach (MonsterCatalogRow row in rows)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.Name)) continue;
                var section = row.Section;
                if (section == MonsterCatalogSection.OfficialOnly &&
                    defaultCategories.TryGetValue(DefaultMonsters.Normalize(row.Name),
                        out var kind))
                    section = kind == DefaultMonsters.MonsterKind.Epic
                        ? MonsterCatalogSection.Epic : MonsterCatalogSection.Large;

                List<string> destination = section switch
                {
                    MonsterCatalogSection.Epic => database.Epic,
                    MonsterCatalogSection.Large => database.Large,
                    _ => null
                };
                if (destination != null &&
                    !destination.Contains(row.Name, StringComparer.OrdinalIgnoreCase))
                    destination.Add(row.Name);
            }
            return database;
        }

        public static MonsterDatabase Merge(MonsterDatabase saved) =>
            ToDatabase(CreateRows(saved));

    }
}
