using System.Collections.Generic;

namespace VideoGenerator.Views.Models
{
    public static class DefaultGroups
    {
        public static List<ThematicGroup> Get()
        {
            return new List<ThematicGroup>
            {
                // Regions & Factions
                new ThematicGroup { Name = "Bandle City", Category = "Region", ChampionsRaw = "Corki, Lulu, Yuumi, Veigar, Tristana, Rumble, Teemo, Kennen" },
                new ThematicGroup { Name = "Bilgewater", Category = "Region", ChampionsRaw = "Fizz, Gangplank, Graves, Nautilus, Miss Fortune, Illaoi, Pyke, Twisted Fate" },
                new ThematicGroup { Name = "Demacia", Category = "Region", ChampionsRaw = "Fiora, Galio, Garen, Lux, Poppy, Lucian, Jarvan IV, Quinn, Shyvana, Sona, Vayne, Xin Zhao, Sylas" },
                new ThematicGroup { Name = "Ixtal", Category = "Region", ChampionsRaw = "Malphite, Milio, Neeko, Nidalee, Qiyana, Rengar, Zyra" },
                new ThematicGroup { Name = "Darkin", Category = "Region", ChampionsRaw = "Aatrox, Kayn, Varus, Naafiri" },
                new ThematicGroup { Name = "Ascended Darkin", Category = "Region", ChampionsRaw = "Aatrox, Varus, Naafiri" },
                new ThematicGroup { Name = "Noxus", Category = "Region", ChampionsRaw = "Darius, Draven, Katarina, Swain, Talon, Vladimir, Sion, Kled, Samira, Rell, Briar, LeBlanc, Cassiopeia, Riven" },
                new ThematicGroup { Name = "Ionia", Category = "Region", ChampionsRaw = "Ahri, Akali, Irelia, Jhin, Karma, Kayn, Kennen, Lee Sin, Lillia, Master Yi, Rakan, Sett, Shen, Syndra, Varus, Wukong, Xayah, Yasuo, Yone, Zed" },
                new ThematicGroup { Name = "Vastaya", Category = "Region", ChampionsRaw = "Ahri, Nami, Neeko, Rakan, Rengar, Wukong, Xayah" },
                new ThematicGroup { Name = "Demon", Category = "Region", ChampionsRaw = "Fiddlesticks, Evelynn, Nocturne, Shaco, Tahm Kench, Swain, Yone, Annie" },
                new ThematicGroup { Name = "Ascended", Category = "Region", ChampionsRaw = "Azir, Pantheon, Renekton, Nasus, Xerath" },
                new ThematicGroup { Name = "Void", Category = "Region", ChampionsRaw = "VelKoz, RekSai, Kassadin, Kaisa, ChoGath, KogMaw, Malzahar, KhaZix" },
                new ThematicGroup { Name = "Kinkou", Category = "Region", ChampionsRaw = "Shen, Akali, Kennen" },
                new ThematicGroup { Name = "Shurima", Category = "Region", ChampionsRaw = "Akshan, Amumu, Sivir, Renekton, Taliyah, Nasus, Rammus, kSante, Azir, Xerath" },
                new ThematicGroup { Name = "Targon", Category = "Region", ChampionsRaw = "Leona, Diana, Pantheon, Taric, Zoe, Aphelios, Aurelion Sol" },
                new ThematicGroup { Name = "Freljord", Category = "Region", ChampionsRaw = "Ashe, Sejuani, Lissandra, Braum, Olaf, Tryndamere, Volibear, Anivia, Ornn, Gnar, Nunu" },
                new ThematicGroup { Name = "Shadow Isles", Category = "Region", ChampionsRaw = "Viego, Thresh, Hecarim, Kalista, Karthus, Yorick, Gwen, Senna, Maokai, Elise" },
                new ThematicGroup { Name = "Piltover", Category = "Region", ChampionsRaw = "Jayce, Caitlyn, Vi, Ezreal, Heimerdinger, Orianna, Seraphine" },
                new ThematicGroup { Name = "Zaun", Category = "Region", ChampionsRaw = "Jinx, Ekko, Twitch, Singed, Warwick, Urgot, Viktor, Blitzcrank, Zac, Dr Mundo, Renata" },

                // Classes
                new ThematicGroup { Name = "Assassin", Category = "Class", ChampionsRaw = "Zed, Akali, Katarina, Talon, Pyke" },
                new ThematicGroup { Name = "Enchanter", Category = "Class", ChampionsRaw = "Lulu, Janna, Soraka, Nami, Sona" },
                new ThematicGroup { Name = "Fighter", Category = "Class", ChampionsRaw = "Darius, Garen, Sett, Lee Sin, Riven" },
                new ThematicGroup { Name = "Mage", Category = "Class", ChampionsRaw = "Lux, Ahri, Veigar, Syndra, Orianna" },
                new ThematicGroup { Name = "Marksmen", Category = "Class", ChampionsRaw = "Ashe, Caitlyn, Jinx, Ezreal, Jhin" },
                new ThematicGroup { Name = "Tank", Category = "Class", ChampionsRaw = "Malphite, Ornn, Sion, Leona, Braum" },

                // Legacy Skins (Fallback for common thematic keywords)
                new ThematicGroup { Name = "Spirit Blossom", Category = "Skinline", ChampionsRaw = "Ahri, Aphelios, Cassiopeia, Darius, Evelynn, Kindred, Lillia, Master Yi, Riven, Sett, Soraka, Syndra, Teemo, Thresh, Tristana, Vayne, Yasuo, Yone, Yorick, Ashe, Bard, Irelia, Ivern, Karma, Lux, Morgana, Nidalee, Varus, Zed, Zyra, Sona, Volibear" },
                new ThematicGroup { Name = "Dragonmancer", Category = "Skinline", ChampionsRaw = "Aurelion Sol, Lee Sin, Ashe, Brand, Sett, Yasuo, Thresh, Kai'Sa, Karma, Volibear, Vayne, Fiora, Kassadin, Rakan" },
                new ThematicGroup { Name = "Porcelain", Category = "Skinline", ChampionsRaw = "Amumu, Aurelion Sol, Darius, Ezreal, Graves, Irelia, Kindred, Lissandra, Lux, Miss Fortune, Morgana" },
                new ThematicGroup { Name = "Lunar Revel", Category = "Skinline", ChampionsRaw = "Lux, Nasus, Warwick, Jinx, Vayne, Sylas" }
            };
        }
    }
}
