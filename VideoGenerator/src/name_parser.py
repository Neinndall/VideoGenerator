"""
This module is responsible for parsing the event folder names into human-readable
text and identifying the target for icon fetching. It uses a modular, chain-of-responsibility
pattern with event handlers.
"""
import re
from . import event_handlers as handlers

# --- Data for Handlers ---

SIMPLE_KEYWORD_MAP = {
    "Kill3DPenta": ("Penta Kill", "Penta", "generic"),
    "Kill3DFirst": ("First Blood", "FirstBlood", "generic"),
    "Kill3DTurret": ("Turret Takedown", "Turret", "generic"),
    "Kill3DAheadAllyTeam": ("Kill (Ally Team Ahead)", "General", "generic"),
    "Kill3DBehindAllyTeam": ("Kill (Ally Team Behind)", "General", "generic"),
    "Shop2DOpen": ("Open Item Shop", "NoIcon", "generic"),
    "Shop3DOpen": ("Open Item Shop", "NoIcon", "generic"),
    "Move2DRiver": ("Movement in River", "NoIcon", "generic"),
    "Move3DRiver": ("Movement in River", "NoIcon", "generic"),
}

INTERACTION_MAP = {
    "FirstEncounter": "First Encounter with {0}",
    "SecondEncounter": "Second Encounter with {0}",
    "MoveFirst": "First Movement",
    "MoveLong": "Long Movement",
    "MoveStandard": "Standard Movement",
}

SPECIFIC_TEXT_MAP = {
    "JokeTauntResponse": "Respond to Jokes/Taunts",
    "TauntResponse": "Respond to Taunt",
    "JokeResponse": "Respond to Joke",
    "Taunt": "Taunt",
    "Joke": "Joke",
    "Respawn": "Respawn",
    "Recall": "Recall",
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
    handlers.PrefixedEventHandler("Assist", "Assist {0}", "champion"),
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
