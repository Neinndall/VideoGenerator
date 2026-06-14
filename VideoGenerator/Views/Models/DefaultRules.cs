using System.Collections.Generic;

namespace VideoGenerator.Views.Models
{
    public static class DefaultRules
    {
        public static List<EventRule> Get()
        {
            return new List<EventRule>
            {
                // 1. Interactions
                new EventRule { Keyword = "FirstEncounter", TranslationKey = "interaction_first_encounter_one", IconType = "champion", Type = RuleType.Interaction, ExtractsTarget = true },
                new EventRule { Keyword = "SecondEncounter", TranslationKey = "interaction_second_encounter_one", IconType = "champion", Type = RuleType.Interaction, ExtractsTarget = true },
                new EventRule { Keyword = "MoveFirstAlly", TranslationKey = "interaction_move_first_ally", IconType = "champion", Type = RuleType.Interaction, ExtractsTarget = true },
                new EventRule { Keyword = "MoveFirstEnemy", TranslationKey = "interaction_move_first_enemy", IconType = "champion", Type = RuleType.Interaction, ExtractsTarget = true },
                new EventRule { Keyword = "MoveFirst", TranslationKey = "interaction_move_first_target", IconType = "champion", Type = RuleType.Target, ExtractsTarget = true },
                new EventRule { Keyword = "MoveLongAllSwords", TranslationKey = "event_move_long_all_swords", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "MoveFirstGeneral", TranslationKey = "event_move_first", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "MoveLong", TranslationKey = "event_move_long", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "MoveStandard", TranslationKey = "event_move_standard", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },

                // 2. Specific Responses
                new EventRule { Keyword = "JokeGeneralEnd", TranslationKey = "event_joke_end", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "JokeTauntResponse", TranslationKey = "event_response_joke_taunt", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "TauntResponse", TranslationKey = "event_response_taunt", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "JokeResponse", TranslationKey = "event_response_joke", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },

                // 3. Prefixed rules (Target extraction)
                new EventRule { Keyword = "KillAllyAhead", TranslationKey = "event_kill_ally_ahead", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillAllyBehind", TranslationKey = "event_kill_ally_behind", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillFirst", TranslationKey = "event_first_blood", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "KillPenta", TranslationKey = "event_penta_kill", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Kill", TranslationKey = "interaction_kill_one", IconType = "champion", Type = RuleType.Target, ExtractsTarget = true },
                new EventRule { Keyword = "KillTurret", TranslationKey = "event_turret_takedown", IconType = "structure", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Assist", TranslationKey = "interaction_assist_one", IconType = "champion", Type = RuleType.Target, ExtractsTarget = true },
                
                // 4. Simple generic rules
                new EventRule { Keyword = "Recall", TranslationKey = "event_recall", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Respawn", TranslationKey = "event_respawn", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Death", TranslationKey = "event_death", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Laugh", TranslationKey = "event_laugh", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Joke", TranslationKey = "event_joke", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Taunt", TranslationKey = "event_taunt", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Attack", TranslationKey = "event_attack", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Ping", TranslationKey = "event_ping", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Spell", TranslationKey = "event_spell", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "SpellBuffReceive", TranslationKey = "event_buff_receive_general", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Spell2DRRankOne", TranslationKey = "event_spell_r_rank_one", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Spell2DRankUp", TranslationKey = "event_spell_rank_up", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                
                new EventRule { Keyword = "Shop2DOpen", TranslationKey = "event_open_shop", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Shop3DOpen", TranslationKey = "event_open_shop", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Move2DRiver", TranslationKey = "event_move_river", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "Move3DRiver", TranslationKey = "event_move_river", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "cast", TranslationKey = "event_spell", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false },
                new EventRule { Keyword = "hit", TranslationKey = "event_spell", IconType = "generic", Type = RuleType.Simple, ExtractsTarget = false }
            };
        }
    }
}
