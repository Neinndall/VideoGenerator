using System;
using System.Collections.Generic;
using System.Linq;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    /// <summary>
    /// DefaultStructures seeds structures.json with built-ins. The JSON stores
    /// the merged catalog, including personal additions, just like groups.json.
    /// Existing differing mappings with the same keyword are preserved as
    /// editable overrides.
    /// </summary>
    public sealed record StructureCatalogRow(string Keyword, string TargetName, bool IsOfficial)
    {
        public bool CanDelete => !IsOfficial;
    }

    public static class StructureCatalogEditor
    {
        public static IReadOnlyList<StructureCatalogRow> CreateRows(
            IEnumerable<StructureMapping> personalMappings)
        {
            var rows = new Dictionary<string, StructureCatalogRow>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var item in DefaultStructures.Entries)
                rows[DefaultStructures.Normalize(item.Keyword)] =
                    new StructureCatalogRow(item.Keyword, item.TargetName, true);

            foreach (StructureMapping mapping in personalMappings ?? Enumerable.Empty<StructureMapping>())
            {
                if (string.IsNullOrWhiteSpace(mapping?.Keyword) ||
                    string.IsNullOrWhiteSpace(mapping.TargetName)) continue;

                string key = DefaultStructures.Normalize(mapping.Keyword);
                if (key.Length == 0) continue;
                if (rows.TryGetValue(key, out var existing) && existing.IsOfficial &&
                    string.Equals(existing.TargetName, mapping.TargetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                rows[key] = new StructureCatalogRow(mapping.Keyword.Trim(),
                    mapping.TargetName.Trim(), false);
            }
            return rows.Values.OrderBy(row => row.Keyword,
                StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static List<StructureMapping> ToDatabaseMappings(
            IEnumerable<StructureCatalogRow> rows) => rows
            .Where(row => row != null &&
                          !string.IsNullOrWhiteSpace(row.Keyword) &&
                          !string.IsNullOrWhiteSpace(row.TargetName))
            .Select(row => new StructureMapping
            {
                Keyword = row.Keyword,
                TargetName = row.TargetName
            }).ToList();

        public static List<StructureMapping> Merge(
            IEnumerable<StructureMapping> saved) =>
            ToDatabaseMappings(CreateRows(saved));

        public static IReadOnlyList<StructureMapping> ForRecognition(
            IEnumerable<StructureMapping> personalMappings) => CreateRows(personalMappings)
            .OrderByDescending(row => DefaultStructures.Normalize(row.Keyword).Length)
            .ThenBy(row => row.Keyword, StringComparer.OrdinalIgnoreCase)
            .Select(row => new StructureMapping
            {
                Keyword = row.Keyword,
                TargetName = row.TargetName
            }).ToArray();
    }
}
