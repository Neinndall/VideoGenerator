using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using VideoGenerator.Models;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    public class AliasManager
    {
        private readonly string _configPath;
        public ObservableCollection<ChampionAlias> Aliases { get; } = new();

        public AliasManager()
        {
            _configPath = Path.Combine(AppConfig.ConfigDir, "champion_aliases.json");
            LoadAliases();
        }

        public void LoadAliases()
        {
            Aliases.Clear();
            List<ChampionAlias> loaded = new();

            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    loaded = JsonSerializer.Deserialize<List<ChampionAlias>>(json) ?? new();
                }
                catch { }
            }

            var defaults = DefaultAliases.Get();
            foreach (var def in defaults)
            {
                var existing = loaded.FirstOrDefault(a => a.DisplayName.Equals(def.DisplayName, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    def.IsOfficial = true;
                    loaded.Add(def);
                }
                else
                {
                    existing.IsOfficial = true;
                }
            }

            foreach (var alias in loaded.OrderBy(a => a.DisplayName))
            {
                Aliases.Add(alias);
            }

            SaveAliases();
        }

        public void SaveAliases()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
                string json = JsonSerializer.Serialize(Aliases.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }

        public string GetInternalName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return string.Empty;
            string clean = displayName.Replace(" ", "").Replace("'", "");
            var alias = Aliases.FirstOrDefault(a => a.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase) || 
                                                     a.DisplayName.Replace(" ", "").Equals(clean, StringComparison.OrdinalIgnoreCase));
            
            return alias?.InternalName ?? clean;
        }

        private static readonly HashSet<string> ValidChampions = new(StringComparer.OrdinalIgnoreCase)
        {
            "Aatrox", "Ahri", "Akali", "Akshan", "Alistar", "Amumu", "Anivia", "Annie", "Aphelios", "Ashe", "AurelionSol",
            "Azir", "Bard", "Belveth", "Blitzcrank", "Brand", "Braum", "Briar", "Caitlyn", "Camille", "Cassiopeia",
            "ChoGath", "Corki", "Darius", "Diana", "DrMundo", "Draven", "Ekko", "Elise", "Evelynn", "Ezreal",
            "Fiddlesticks", "Fiora", "Fizz", "Galio", "Gangplank", "Garen", "Gnar", "Gragas", "Graves", "Gwen",
            "Hecarim", "Heimerdinger", "Hwei", "Illaoi", "Irelia", "Ivern", "Janna", "JarvanIV", "Jax", "Jayce",
            "Jhin", "Jinx", "Kaisa", "Kalista", "Karma", "Karthus", "Kassadin", "Katarina", "Kayle", "Kayn",
            "Kennen", "Khazix", "Kindred", "Kled", "KogMaw", "KSante", "Leblanc", "LeeSin", "Leona", "Lillia", "Lissandra",
            "Lucian", "Lulu", "Lux", "Malphite", "Malzahar", "Maokai", "MasterYi", "Milio", "MissFortune", "Mordekaiser",
            "Morgana", "Naafiri", "Nami", "Nasus", "Nautilus", "Neeko", "Nidalee", "Nilah", "Nocturne", "Nunu",
            "Olaf", "Orianna", "Ornn", "Pantheon", "Poppy", "Pyke", "Qiyana", "Quinn", "Rakan", "Rammus",
            "RekSai", "Rell", "Renata", "Renekton", "Rengar", "Riven", "Rumble", "Ryze", "Samira", "Sejuani",
            "Senna", "Seraphine", "Sett", "Shaco", "Shen", "Shyvana", "Singed", "Sion", "Sivir", "Skarner",
            "Sona", "Soraka", "Swain", "Sylas", "Syndra", "TahmKench", "Taliyah", "Talon", "Taric", "Teemo",
            "Thresh", "Tristana", "Trundle", "Tryndamere", "TwistedFate", "Twitch", "Udyr", "Urgot", "Varus",
            "Vayne", "Veigar", "Velkoz", "Vex", "Vi", "Viego", "Viktor", "Vladimir", "Volibear", "Warwick",
            "MonkeyKing", "Xayah", "Xerath", "XinZhao", "Yasuo", "Yone", "Yorick", "Yuumi", "Zac", "Zed",
            "Zeri", "Ziggs", "Zilean", "Zoe", "Zyra", "Ambessa", "Mel", "Locke", "Zaahen", "Yunara"
        };

        public bool IsValidChampion(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return false;
            string clean = displayName.Replace(" ", "").Replace("'", "").Replace(".", "").Replace("-", "");
            
            if (ValidChampions.Contains(clean)) return true;

            return Aliases.Any(a => a.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase) || 
                                     a.DisplayName.Replace(" ", "").Replace("'", "").Replace(".", "").Replace("-", "").Equals(clean, StringComparison.OrdinalIgnoreCase) ||
                                     a.InternalName.Equals(clean, StringComparison.OrdinalIgnoreCase));
        }
    }
}
