using System.Collections.Generic;

namespace VideoGenerator.Views.Models
{
    public static class DefaultAliases
    {
        public static List<ChampionAlias> Get()
        {
            return new List<ChampionAlias>
            {
                new ChampionAlias { DisplayName = "Wukong", InternalName = "MonkeyKing" },
                new ChampionAlias { DisplayName = "Master Yi", InternalName = "MasterYi" },
                new ChampionAlias { DisplayName = "Xin Zhao", InternalName = "XinZhao" },
                new ChampionAlias { DisplayName = "Lee Sin", InternalName = "LeeSin" },
                new ChampionAlias { DisplayName = "LeBlanc", InternalName = "Leblanc" },
                new ChampionAlias { DisplayName = "Kha'Zix", InternalName = "Khazix" },
                new ChampionAlias { DisplayName = "Bel'Veth", InternalName = "Belveth" },
                new ChampionAlias { DisplayName = "Vel'Koz", InternalName = "Velkoz" },
                new ChampionAlias { DisplayName = "Rek'Sai", InternalName = "RekSai" },
                new ChampionAlias { DisplayName = "Dr. Mundo", InternalName = "DrMundo" },
                new ChampionAlias { DisplayName = "Jarvan IV", InternalName = "JarvanIV" }
            };
        }
    }
}
