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
                new EventRule { Keyword = "KillFirstAllyTeam", TranslationKey = "event_kill_first_ally_team", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillFirstEnemyTeam", TranslationKey = "event_kill_first_enemy_team", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillPenta", TranslationKey = "event_penta_kill", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillDouble", TranslationKey = "event_double_kill", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillTriple", TranslationKey = "event_triple_kill", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillQuadra", TranslationKey = "event_quadra_kill", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillTurret", TranslationKey = "event_turret_takedown", Section = "COMBAT", IconType = "structure", IconLookup = "Turret", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillBaronSteal", TranslationKey = "event_baron_steal", Section = "COMBAT", IconType = "monster", IconLookup = "Baron", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillDragonSteal", TranslationKey = "event_dragon_steal", Section = "COMBAT", IconType = "monster", IconLookup = "Dragon", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillElderSteal", TranslationKey = "event_elder_steal", Section = "COMBAT", IconType = "monster", IconLookup = "Elder Dragon", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillAllyAhead", TranslationKey = "event_kill_ally_ahead", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillAheadAllyTeam", TranslationKey = "event_kill_ally_ahead", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillAllyBehind", TranslationKey = "event_kill_ally_behind", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillBehindAllyTeam", TranslationKey = "event_kill_ally_behind", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "DeathHuman", TranslationKey = "event_death_human", Section = "COMBAT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },

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
                new EventRule { Keyword = "MoveRiver", TranslationKey = "event_move_river", Section = "MOVEMENT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "MoveLongAllSwords", TranslationKey = "event_move_long_all_swords", Section = "MOVEMENT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Recall", TranslationKey = "event_recall", Section = "MOVEMENT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Respawn", Section = "MOVEMENT", TranslationKey = "event_respawn", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "LevelUp", TranslationKey = "event_level_up", Section = "ABILITIES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "MoveAllyNear", TranslationKey = "event_move_ally_near", Section = "MOVEMENT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "MoveEnemyNear", TranslationKey = "event_move_enemy_near", Section = "MOVEMENT", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },

                // ==========================================
                // 💰 ITEMS (Tienda y Objetos)
                // ==========================================
                new EventRule { Keyword = "BuyItem", TranslationKey = "event_buy_item", Section = "ITEMS", IconType = "item", Type = RuleType.Target, ExtractsTarget = true },
                new EventRule { Keyword = "UseItem", TranslationKey = "event_use_item", Section = "ITEMS", IconType = "item", Type = RuleType.Target, ExtractsTarget = true },
                new EventRule { Keyword = "ShopOpen", TranslationKey = "event_open_shop", Section = "ITEMS", IconType = "system", IconLookup = "Gold", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Shop", TranslationKey = "event_shop_open", Section = "ITEMS", IconType = "system", IconLookup = "Gold", Type = RuleType.Simple, ExtractsTarget = false },

                // ==========================================
                // 📡 PINGS (Señales de Mapa)
                // ==========================================
                new EventRule { Keyword = "Ping2DAssistMe", TranslationKey = "event_ping_assist", Section = "PINGS", IconType = "system", IconLookup = "Assist Me", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Ping2DDanger", TranslationKey = "event_ping_danger", Section = "PINGS", IconType = "system", IconLookup = "Danger", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Ping2DEnemyMissing", TranslationKey = "event_ping_missing", Section = "PINGS", IconType = "system", IconLookup = "Enemy Missing", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Ping2DOnMyWay", TranslationKey = "event_ping_omw", Section = "PINGS", IconType = "system", IconLookup = "On My Way", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Ping2DRetreat", TranslationKey = "event_ping_retreat", Section = "PINGS", IconType = "system", IconLookup = "Retreat", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Ping2DPush", TranslationKey = "event_ping_push", Section = "PINGS", IconType = "system", IconLookup = "Push", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Ping2DBait", TranslationKey = "event_ping_bait", Section = "PINGS", IconType = "system", IconLookup = "Bait", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Ping2DHold", TranslationKey = "event_ping_hold", Section = "PINGS", IconType = "system", IconLookup = "Hold", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Ping2DAllIn", TranslationKey = "event_ping_all_in", Section = "PINGS", IconType = "system", IconLookup = "All In", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Ping2DTarget", TranslationKey = "event_ping_target", Section = "PINGS", IconType = "system", IconLookup = "Target", Type = RuleType.Simple, ExtractsTarget = false },

                // ==========================================
                // ✨ ABILITIES (Habilidades y Rangos)
                // ==========================================
                new EventRule { Keyword = "SpellBuffReceive", TranslationKey = "event_buff_receive", Section = "ABILITIES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "SpellRRankOne", TranslationKey = "event_spell_r_rank_one", Section = "ABILITIES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "SpellRRankUp", TranslationKey = "event_spell_rank_up", Section = "ABILITIES", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },

                // ==========================================
                // 🤝 INTERACTIONS (Encuentros Lore)
                // ==========================================
                new EventRule { Keyword = "FirstEncounter", TranslationKey = "interaction_first_encounter_one", Section = "INTERACTIONS", IconType = "champion", Type = RuleType.Interaction, ExtractsTarget = true },
                new EventRule { Keyword = "SecondEncounter", TranslationKey = "interaction_second_encounter_one", Section = "INTERACTIONS", IconType = "champion", Type = RuleType.Interaction, ExtractsTarget = true },
                new EventRule { Keyword = "GameEndDefeat", TranslationKey = "event_game_end_defeat", Section = "OTHER", IconType = "generic", IconLookup = "", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "GameEndVictory", TranslationKey = "event_game_end_victory", Section = "OTHER", IconType = "generic", IconLookup = "", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Unique", TranslationKey = "interaction_unique_emote", Section = "OTHER", IconType = "generic", Type = RuleType.Target, ExtractsTarget = true },
                new EventRule { Keyword = "UniqueTransformAhead", TranslationKey = "event_unique_transform_ahead", Section = "OTHER", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "UniqueTransformBehinf", TranslationKey = "event_unique_transform_behind", Section = "OTHER", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "UniqueTransformGeneral", TranslationKey = "event_unique_transform_general", Section = "OTHER", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false }
            };
        }
    }
}
