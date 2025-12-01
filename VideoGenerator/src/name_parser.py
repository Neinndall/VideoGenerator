"""
This module is responsible for parsing the event folder names into human-readable
text and identifying the target for icon fetching. It uses a modular, chain-of-responsibility
pattern with event handlers.
"""
import re
from . import event_handlers as handlers

# --- Data for Handlers ---

SIMPLE_KEYWORD_MAP = {
    "Kill3DPenta": ("event_penta_kill", "Penta", "generic"),
    "Kill3DFirst": ("event_first_blood", "FirstBlood", "generic"),
    "Kill3DTurret": ("event_turret_takedown", "Turret", "generic"),
    "Kill3DAheadAllyTeam": ("event_kill_ally_ahead", "General", "generic"),
    "Kill3DBehindAllyTeam": ("event_kill_ally_behind", "General", "generic"),
    "Shop2DOpen": ("event_open_shop", "NoIcon", "generic"),
    "Shop3DOpen": ("event_open_shop", "NoIcon", "generic"),
    "Move2DRiver": ("event_move_river", "NoIcon", "generic"),
    "Move3DRiver": ("event_move_river", "NoIcon", "generic"),
}

INTERACTION_MAP = {
    "FirstEncounter": "interaction_first_encounter_one",
    "SecondEncounter": "interaction_second_encounter_one",
    "MoveFirst": "event_move_first",
    "MoveLong": "event_move_long",
    "MoveStandard": "event_move_standard",
}

SPECIFIC_TEXT_MAP = {
    "JokeTauntResponse": "event_response_joke_taunt",
    "TauntResponse": "event_response_taunt",
    "JokeResponse": "event_response_joke",
    "Taunt": "event_taunt",
    "Joke": "event_joke",
    "Respawn": "event_respawn",
    "Recall": "event_recall",
}

# --- Handler Instantiation ---

# The order of this list is crucial. Handlers are checked sequentially.
# More specific handlers should come before more general ones.
EVENT_HANDLERS = [
    handlers.SimpleKeywordHandler(SIMPLE_KEYWORD_MAP),
    handlers.ItemEventHandler(),
    handlers.SkinInteractionHandler(),
    handlers.MonsterAttackHandler(),
    handlers.MappedInteractionHandler(SPECIFIC_TEXT_MAP),
    handlers.MappedInteractionHandler(INTERACTION_MAP),
    handlers.PrefixedEventHandler("Kill", "Kill {0}", "generic"),
    handlers.PrefixedEventHandler("Assist", "interaction_assist_one", "champion"),
    handlers.PrefixedEventHandler("MoveFirstAlly", "First Movement (Ally: {0})", "generic"),
    handlers.PrefixedEventHandler("SpellPRevive", "Revive {0}", "generic"),
    handlers.DefaultHandler() # This should always be last
]

def parse_folder_name(folder_name, translations, selected_language):
    """
    Parses a folder name to extract display text and icon information using a
    chain of event handlers.

    :param folder_name: The original name of the folder to parse.
    :param translations: The dictionary of translations.
    :param selected_language: The selected language code.
    :return: A tuple of (display_text, target_for_icon, icon_type).
    """
    # Pre-process the name to simplify it for handlers
    name_part = re.sub(r'^Play_vo_\w+_', '', folder_name)
    name_part = name_part.replace("3D", "").replace("2D", "")

    # Iterate through the handlers until one can process the name
    for handler in EVENT_HANDLERS:
        if handler.can_handle(name_part, folder_name):
            result = handler.parse(name_part, folder_name, translations, selected_language)
            if result:
                display_text, target_for_icon, icon_type = result
                # Post-process to handle category-based icons
                if icon_type == "champion":
                    from . import config
                    all_categories = {**config.CHAMPIONS_BY_REGIONS, **config.CHAMPIONS_BY_SKINS}
                    for category in all_categories:
                        if target_for_icon.startswith(category):
                            return display_text, category, "champion"
                return result

    # This part should ideally not be reached if DefaultHandler is correctly implemented
    return "Unknown Event", "General", "generic"
