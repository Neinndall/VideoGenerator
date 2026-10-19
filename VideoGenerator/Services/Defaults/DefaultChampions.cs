using VideoGenerator.Views.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VideoGenerator.Services
{
    /// <summary>
    /// Built-in champion vocabulary composed from the existing app-maintained
    /// DefaultGroups roster and the exact Riot-facing DefaultAliases.
    /// No second hand-maintained champion list or Config/champions.json required.
    /// Riot's local WAD champion summary is additional runtime knowledge.
    /// </summary>
    public static class DefaultChampions
    {
        public static List<ChampionAlias> Get()
        {
            var result = new Dictionary<string, ChampionAlias>(StringComparer.OrdinalIgnoreCase);
            foreach (var alias in DefaultAliases.Get())
            {
                alias.IsOfficial = true;
                result[Normalize(alias.DisplayName)] = alias;
            }

            foreach (string name in DefaultGroups.Get()
                .SelectMany(group => group.GetChampionsList()))
            {
                string key = Normalize(name);
                if (key.Length == 0 || result.ContainsKey(key)) continue;
                result[key] = new ChampionAlias
                {
                    DisplayName = name,
                    InternalName = key,
                    IsOfficial = true
                };
            }

            return result.Values.OrderBy(x => x.DisplayName,
                StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string Normalize(string name) =>
            new string((name ?? "").Where(char.IsLetterOrDigit).ToArray());
    }
}
