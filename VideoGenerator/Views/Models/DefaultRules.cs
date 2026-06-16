using System.Collections.Generic;

namespace VideoGenerator.Views.Models
{
    public static class DefaultRules
    {
        public static List<EventRule> Get()
        {
            return new List<EventRule>
            {
                // ==========================================
                // ⚔️ COMBAT (Peleas y Muertes)
                // ==========================================
                new EventRule { Keyword = "Kill", TranslationKey = "interaction_kill_one", Section = "COMBAT", IconType = "champion", Type = RuleType.Target, ExtractsTarget = true },
                new EventRule { Keyword = "Assist", TranslationKey = "interaction_assist_one", Section = "COMBAT", IconType = "champion", Type = RuleType.Target, ExtractsTarget = true },
                new EventRule { Keyword = "Death", TranslationKey = "interaction_death_one", Section = "COMBAT", IconType = "champion", Type = RuleType.Target, ExtractsTarget = true },
                new EventRule { Keyword = "Attack", TranslationKey = "event_attack", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillFirst", TranslationKey = "event_first_blood", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillPenta", TranslationKey = "event_penta_kill", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillTurret", TranslationKey = "event_turret_takedown", Section = "COMBAT", IconType = "structure", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillAllyAhead", TranslationKey = "event_kill_ally_ahead", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillAheadAllyTeam", TranslationKey = "event_kill_ally_ahead", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillAllyBehind", TranslationKey = "event_kill_ally_behind", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillBehindAllyTeam", TranslationKey = "event_kill_ally_behind", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "DeathHuman", TranslationKey = "event_death_human", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Death", TranslationKey = "event_death", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },

                // ==========================================
                // 🗣️ EMOTES (Risas, Bromas, Respuestas)
                // ==========================================
                new EventRule { Keyword = "Joke", TranslationKey = "event_joke", Section = "EMOTES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Taunt", TranslationKey = "event_taunt", Section = "EMOTES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Laugh", TranslationKey = "event_laugh", Section = "EMOTES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "JokeGeneralEnd", TranslationKey = "event_joke_end", Section = "EMOTES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "JokeResponse", TranslationKey = "event_response_joke", Section = "EMOTES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "TauntResponse", TranslationKey = "event_response_taunt", Section = "EMOTES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "JokeTauntResponse", TranslationKey = "event_response_joke_taunt", Section = "EMOTES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },

                // ==========================================
                // 🗺️ MOVEMENT (Caminatas y Grieta)
                // ==========================================
                new EventRule { Keyword = "MoveFirst", TranslationKey = "interaction_move_first_target", Section = "MOVEMENT", IconType = "champion", Type = RuleType.Target, ExtractsTarget = true },
                new EventRule { Keyword = "MoveFirstAlly", TranslationKey = "interaction_move_first_ally", Section = "MOVEMENT", IconType = "champion", Type = RuleType.Interaction, ExtractsTarget = true },
                new EventRule { Keyword = "MoveFirstEnemy", TranslationKey = "interaction_move_first_enemy", Section = "MOVEMENT", IconType = "champion", Type = RuleType.Interaction, ExtractsTarget = true },
                new EventRule { Keyword = "MoveFirstGeneral", TranslationKey = "event_move_first", Section = "MOVEMENT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "MoveLong", TranslationKey = "event_move_long", Section = "MOVEMENT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "MoveStandard", TranslationKey = "event_move_standard", Section = "MOVEMENT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Move2DRiver", TranslationKey = "event_move_river", Section = "MOVEMENT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Move3DRiver", TranslationKey = "event_move_river", Section = "MOVEMENT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "MoveLongAllSwords", TranslationKey = "event_move_long_all_swords", Section = "MOVEMENT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Recall", TranslationKey = "event_recall_general", Section = "MOVEMENT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Recall", TranslationKey = "event_recall", Section = "MOVEMENT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Respawn", Section = "MOVEMENT", TranslationKey = "event_respawn", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },

                // ==========================================
                // 💰 ITEMS (Tienda y Objetos)
                // ==========================================
                new EventRule { Keyword = "BuyItem", TranslationKey = "event_buy_item", Section = "ITEMS", IconType = "item", Type = RuleType.Target, ExtractsTarget = true },
                new EventRule { Keyword = "UseItem", TranslationKey = "event_use_item", Section = "ITEMS", IconType = "item", Type = RuleType.Target, ExtractsTarget = true },
                new EventRule { Keyword = "Shop2DOpen", TranslationKey = "event_open_shop", Section = "ITEMS", IconType = "system", IconLookup = "Gold", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Shop3DOpen", TranslationKey = "event_open_shop", Section = "ITEMS", IconType = "system", IconLookup = "Gold", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Shop", TranslationKey = "event_shop_open", Section = "ITEMS", IconType = "system", IconLookup = "Gold", Type = RuleType.Simple, ExtractsTarget = false },

                // ==========================================
                // 📡 PINGS (Señales de Mapa)
                // ==========================================
                new EventRule { Keyword = "Ping2DAssistMe", TranslationKey = "event_ping_assist", Section = "PINGS", IconType = "system", IconLookup = "Assist Me", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Ping2DDanger", TranslationKey = "event_ping_danger", Section = "PINGS", IconType = "system", IconLookup = "Danger", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Ping2DEnemyMissing", TranslationKey = "event_ping_missing", Section = "PINGS", IconType = "system", IconLookup = "Enemy Missing", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Ping2DOnMyWay", TranslationKey = "event_ping_omw", Section = "PINGS", IconType = "system", IconLookup = "On My Way", Type = RuleType.Simple, ExtractsTarget = false },

                // ==========================================
                // ✨ ABILITIES (Habilidades y Rangos)
                // ==========================================
                new EventRule { Keyword = "SpellBuffReceive", TranslationKey = "event_buff_receive_general", Section = "ABILITIES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Spell2DRRankOne", TranslationKey = "event_spell_r_rank_one", Section = "ABILITIES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Spell2DRRankUp", TranslationKey = "event_spell_rank_up", Section = "ABILITIES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },

                // ==========================================
                // 🤝 INTERACTIONS (Encuentros Lore)
                // ==========================================
                new EventRule { Keyword = "FirstEncounter", TranslationKey = "interaction_first_encounter_one", Section = "INTERACTIONS", IconType = "champion", Type = RuleType.Interaction, ExtractsTarget = true },
                new EventRule { Keyword = "SecondEncounter", TranslationKey = "interaction_second_encounter_one", Section = "INTERACTIONS", IconType = "champion", Type = RuleType.Interaction, ExtractsTarget = true },
                new EventRule { Keyword = "Respawn", TranslationKey = "interaction_respawn_class", Section = "INTERACTIONS", IconType = "champion", Type = RuleType.Target, ExtractsTarget = true }
            };
        }
    }
}
