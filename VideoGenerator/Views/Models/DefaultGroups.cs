using System.Collections.Generic;

namespace VideoGenerator.Views.Models
{
    public static class DefaultGroups
    {
        public static List<ThematicGroup> Get()
        {
            var groups = new List<ThematicGroup>
            {
                // Regions & Factions (Mapped to Category: Region for Crest/Icon support)
                new ThematicGroup { Name = "Bandle City", Category = "Region", ChampionsRaw = "Corki, Lulu, Yuumi, Veigar, Tristana, Rumble, Teemo, Kennen, Vex, Ziggs" },
                new ThematicGroup { Name = "Bilgewater", Category = "Region", ChampionsRaw = "Fizz, Gangplank, Graves, Nautilus, Miss Fortune, Illaoi, Pyke, Twisted Fate, Nilah" },
                new ThematicGroup { Name = "Demacia", Category = "Region", ChampionsRaw = "Fiora, Galio, Garen, Lux, Poppy, Lucian, Jarvan IV, Quinn, Shyvana, Sona, Vayne, Xin Zhao, Sylas, Kayle, Morgana" },
                new ThematicGroup { Name = "Ixtal", Category = "Region", ChampionsRaw = "Malphite, Milio, Neeko, Nidalee, Qiyana, Rengar, Zyra, Skarner, Smolder" },
                new ThematicGroup { Name = "Noxus", Category = "Region", ChampionsRaw = "Darius, Draven, Katarina, Swain, Talon, Vladimir, Sion, Kled, Samira, Rell, Briar, LeBlanc, Cassiopeia, Riven, Ambessa" },
                new ThematicGroup { Name = "Ionia", Category = "Region", ChampionsRaw = "Ahri, Akali, Irelia, Jhin, Karma, Kayn, Kennen, Lee Sin, Lillia, Master Yi, Rakan, Sett, Shen, Syndra, Varus, Wukong, Xayah, Yasuo, Yone, Zed, Hwei, Ivern" },
                new ThematicGroup { Name = "Shurima", Category = "Region", ChampionsRaw = "Akshan, Amumu, Sivir, Renekton, Taliyah, Nasus, Rammus, KSante, Azir, Xerath" },
                new ThematicGroup { Name = "Targon", Category = "Region", ChampionsRaw = "Leona, Diana, Pantheon, Taric, Zoe, Aphelios, Aurelion Sol, Soraka" },
                new ThematicGroup { Name = "Freljord", Category = "Region", ChampionsRaw = "Ashe, Sejuani, Lissandra, Braum, Olaf, Tryndamere, Volibear, Anivia, Ornn, Gnar, Nunu, Gragas, Trundle, Udyr, Aurora" },
                new ThematicGroup { Name = "Shadow Isles", Category = "Region", ChampionsRaw = "Viego, Thresh, Hecarim, Kalista, Karthus, Yorick, Gwen, Senna, Maokai, Elise" },
                new ThematicGroup { Name = "Piltover", Category = "Region", ChampionsRaw = "Jayce, Caitlyn, Vi, Ezreal, Heimerdinger, Orianna, Seraphine, Camille, Mel" },
                new ThematicGroup { Name = "Zaun", Category = "Region", ChampionsRaw = "Jinx, Ekko, Twitch, Singed, Warwick, Urgot, Viktor, Blitzcrank, Zac, Dr Mundo, Renata, Janna, Zeri" },
                new ThematicGroup { Name = "Void", Category = "Region", ChampionsRaw = "Vel'Koz, Rek'Sai, Kassadin, Kai'Sa, Cho'Gath, Kog'Maw, Malzahar, Kha'Zix, Bel'Veth, Jax, Zilean" },

                // Specific Lore Sub-Factions (Treated as Region for specific Crests)
                new ThematicGroup { Name = "Lunari", Category = "Region", ChampionsRaw = "Diana, Aphelios" },
                new ThematicGroup { Name = "Solari", Category = "Region", ChampionsRaw = "Leona" },
                new ThematicGroup { Name = "Kinkou", Category = "Region", ChampionsRaw = "Shen, Akali, Kennen" },
                new ThematicGroup { Name = "Darkin", Category = "Region", ChampionsRaw = "Aatrox, Kayn, Varus, Naafiri" },
                new ThematicGroup { Name = "Ascended", Category = "Region", ChampionsRaw = "Azir, Pantheon, Renekton, Nasus, Xerath" },
                new ThematicGroup { Name = "Vastaya", Category = "Region", ChampionsRaw = "Ahri, Nami, Neeko, Rakan, Rengar, Wukong, Xayah" },
                new ThematicGroup { Name = "Demon", Category = "Region", ChampionsRaw = "Fiddlesticks, Evelynn, Nocturne, Shaco, Tahm Kench, Swain, Yone, Annie" },

                // Classes
                new ThematicGroup { Name = "Assassin", Category = "Class", ChampionsRaw = "Zed, Akali, Katarina, Talon, Pyke, Rengar, Kha'Zix, Evelynn, Shaco, Fizz" },
                new ThematicGroup { Name = "Enchanter", Category = "Class", ChampionsRaw = "Lulu, Janna, Soraka, Nami, Sona, Milio, Yuumi, Renata, Karma" },
                new ThematicGroup { Name = "Fighter", Category = "Class", ChampionsRaw = "Darius, Garen, Sett, Lee Sin, Riven, Aatrox, Jax, Vi, Warwick, Olaf" },
                new ThematicGroup { Name = "Mage", Category = "Class", ChampionsRaw = "Lux, Ahri, Veigar, Syndra, Orianna, Hwei, Viktor, Malzahar, Brand, Ryze" },
                new ThematicGroup { Name = "Marksmen", Category = "Class", ChampionsRaw = "Ashe, Caitlyn, Jinx, Ezreal, Jhin, Vayne, Kai'Sa, Lucian, Miss Fortune, Samira" },
                new ThematicGroup { Name = "Tank", Category = "Class", ChampionsRaw = "Malphite, Ornn, Sion, Leona, Braum, K'Sante, Nautilus, Zac, Alistar, Cho'Gath" }
            };

            foreach (var group in groups)
            {
                group.IsOfficial = true;
            }

            return groups;
        }
    }
}
