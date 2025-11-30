import re
from . import config
from src import data_fetcher
from src import translation

def find_skin_name(champion_name, skin_id):
    """Finds the name of a specific skin."""
    skins_data = data_fetcher.get_skins_data()
    if not skins_data:
        return None

    for skin_id_key, skin in skins_data.items():
        skin_name = skin.get('name', '')
        if champion_name.lower() in skin_name.lower():
            splash_path = skin.get('splashPath', '')
            if splash_path:
                skin_match = re.search(r'Skin(\d+)', splash_path)
                if skin_match and skin_match.group(1) == f"{skin_id:02d}":
                    return skin_name
    return None

def parse_skin_info(text):
    """Extracts skin information from text."""
    skin_pattern = r'(\w+)Skin(\d+)'
    match = re.search(skin_pattern, text)
    if match:
        champion_name = match.group(1)
        skin_id = int(match.group(2))
        return champion_name, skin_id
    return None, None

def get_display_skin_name(champion_name, full_skin_name):
    """Extracts the skin theme from the full skin name."""
    if full_skin_name.lower().endswith(champion_name.lower()):
        theme_name = full_skin_name[:-len(champion_name)].strip()
        if theme_name.lower() == "base":
            return ""
        if theme_name == "Spirit Blossom Springs":
            return "Springs Spirit Blossom"
        return theme_name

    if full_skin_name.lower() == champion_name.lower():
        return ""
    return full_skin_name

def parse_folder_name(folder_name, translations, selected_language):
    def _get_text(key, *args, **kwargs):
        """Inner helper to call translation function with context."""
        return translation.get_text(translations, selected_language, key, *args, **kwargs)

    name_part = re.sub(r'^Play_vo_\w+_', '', folder_name)
    name_part = name_part.replace("3D", "").replace("2D", "")

    if "Kill3DPenta" in folder_name:
        return _get_text("Penta Kill"), "Penta"
    if "Kill3DFirst" in folder_name:
        return _get_text("First Blood"), "FirstBlood"
    if "Kill3DTurret" in folder_name:
        return _get_text("Turret Takedown"), "Turret"
    if "Kill3DAheadAllyTeam" in folder_name:
        return _get_text("Kill (Ally Team Ahead)"), "General"
    if "Kill3DBehindAllyTeam" in folder_name:
        return _get_text("Kill (Ally Team Behind)"), "General"
    if "Shop2DOpen" in folder_name or "Shop3DOpen" in folder_name:
        return _get_text("Open Item Shop"), "NoIcon"
    if "Move2DRiver" in folder_name or "Move3DRiver" in folder_name:
        return _get_text("Movement in River"), "NoIcon"

    if "BuyItem2D" in folder_name or "BuyItem3D" in folder_name:
        match = re.search(r'BuyItem(2D|3D)(.*)', folder_name)
        if match:
            item_name = match.group(2)
            return _get_text("Buy Item ({item_name})", item_name=item_name), item_name, "item"
    
    if "UseItem2D" in folder_name or "UseItem3D" in folder_name:
        match = re.search(r'UseItem(2D|3D)(.*)', folder_name)
        if match:
            item_name = match.group(2)
            return _get_text("Use Item ({item_name})", item_name=item_name), item_name, "item"

    # Handle Attack2D monster interactions
    if "Attack2D" in folder_name:
        attack_match = re.search(r'Attack2D([A-Za-z]+)', folder_name)
        if attack_match:
            monster_name_raw = attack_match.group(1)
            # Format monster name for consistency (e.g., BlueSentinel -> Blue_Sentinel)
            # This regex inserts '_' before every uppercase letter that is not the first character of the string.
            monster_name_formatted = re.sub(r'(?<!^)(?=[A-Z])', r'_', monster_name_raw)
            
            # Create a display-friendly version of the monster name
            display_monster_name = monster_name_formatted.replace("_", " ")

            return _get_text("Attack {monster}", monster=display_monster_name), monster_name_formatted, "monster"
    
    skin_matches = re.findall(r'(\w+)Skin(\d+)', name_part)
    if skin_matches:
        processed_champions = []
        last_actual_champion_name = None
        for champion_name_from_regex, skin_id in skin_matches:
            actual_champion_name = champion_name_from_regex
            interaction_prefixes = ["Kill", "FirstEncounter", "SecondEncounter", "MoveFirstAlly", "Move", "Assist", "AttackNear"]
            for prefix in interaction_prefixes:
                if actual_champion_name.startswith(prefix):
                    actual_champion_name = actual_champion_name[len(prefix):]
                    break

            skin_name = find_skin_name(actual_champion_name, int(skin_id))
            if skin_name:
                display_skin_name = get_display_skin_name(actual_champion_name, skin_name)
                if display_skin_name:
                    processed_champions.append(f"{actual_champion_name} {display_skin_name}")
                else:
                    processed_champions.append(actual_champion_name)
            else:
                processed_champions.append(f"{actual_champion_name} (Skin {skin_id})")
            last_actual_champion_name = actual_champion_name

        display_text = ""
        if "Kill" in name_part:
            display_text = _get_text("Kill {0}", processed_champions[0]) if len(processed_champions) == 1 else _get_text("Kill {0} vs {1}", processed_champions[0], processed_champions[1])
        elif "Death" in name_part:
            display_text = _get_text("Death {0}", processed_champions[0]) if len(processed_champions) == 1 else _get_text("Death {0} vs {1}", processed_champions[0], processed_champions[1])
        elif "Assist" in name_part:
            display_text = _get_text("Assist {0}", processed_champions[0]) if len(processed_champions) == 1 else _get_text("Assist {0}", processed_champions[1])
        elif "AttackNear" in name_part:
            display_text = _get_text("Attack near {0}", processed_champions[0]) if len(processed_champions) == 1 else _get_text("Attack near {0}", processed_champions[1])
        elif "FirstEncounter" in name_part:
            display_text = _get_text("First Encounter with {0}", processed_champions[0]) if len(processed_champions) == 1 else _get_text("First Encounter: {0} vs {1}", processed_champions[0], processed_champions[1])
        elif "SecondEncounter" in name_part:
            display_text = _get_text("Second Encounter with {0}", processed_champions[0]) if len(processed_champions) == 1 else _get_text("Second Encounter: {0} vs {1}", processed_champions[0], processed_champions[1])
        elif "MoveFirstAlly" in name_part:
            display_text = _get_text("First Movement (Ally: {0})", processed_champions[0]) if len(processed_champions) == 1 else _get_text("First Movement (Ally: {0})", ', '.join(processed_champions))
        else:
            display_text = _get_text("Interaction with {0}", processed_champions[0]) if len(processed_champions) == 1 else _get_text("Interaction: {0} vs {1}", processed_champions[0], processed_champions[1])
        return display_text, last_actual_champion_name, "champion"

    if "MoveFirstAlly" in name_part:
        target_name = name_part.replace("MoveFirstAlly", "")
        return _get_text("First Movement (Ally: {0})", target_name), target_name, "generic"

    if name_part.startswith("SpellPRevive"):
        target_name = name_part.replace("SpellPRevive", "")
        if target_name:
            if target_name in translations["EN"]:
                display_name = _get_text(target_name)
                return _get_text("Revive {0}", display_name), target_name, "generic"
            else:
                return _get_text("Revive {0}", target_name), target_name, "generic"
        else:
            return _get_text("Revive"), "General", "generic"

    if name_part.startswith("Kill"):
        target_name = name_part[4:]
        if target_name == "General":
            return _get_text("Kill in General"), "General", "generic"
        else:
            return _get_text("Kill {0}", target_name), target_name, "generic"

    interaction_map = {
        "FirstEncounter": "First Encounter with {0}",
        "SecondEncounter": "Second Encounter with {0}",
        "MoveFirst": "First Movement",
        "MoveLong": "Long Movement",
        "MoveStandard": "Standard Movement",
    }
    
    specific_text_map = {
        "JokeTauntResponse": "Respond to Jokes/Taunts",
        "TauntResponse": "Respond to Taunt",
        "JokeResponse": "Respond to Joke",
        "Taunt": "Taunt",
        "Joke": "Joke",
        "Respawn": "Respawn",
        "Recall": "Recall",
    }

    for key in sorted(specific_text_map.keys(), key=len, reverse=True):
        if name_part.startswith(key):
            base_text_key = specific_text_map[key]
            target_suffix = name_part[len(key):]
            if target_suffix == "General":
                return _get_text(base_text_key) if key in ["Recall", "Respawn"] else _get_text(base_text_key) + " in General", "General", "generic"
            elif target_suffix == "Rare":
                return _get_text(base_text_key) + " Rare", "Rare", "generic"
            else:
                return _get_text(base_text_key) + f" {target_suffix}", target_suffix, "generic"

    for key in sorted(interaction_map.keys(), key=len, reverse=True):
        if name_part.startswith(key):
            interaction_text_key = interaction_map[key]
            target_name = name_part[len(key):]
            if not target_name: target_name = "General"

            if target_name == "General":
                if "{0}" in interaction_text_key:
                    return _get_text("First Encounter in General"), target_name, "generic"
                else:
                    return _get_text(interaction_text_key), target_name, "generic"

            if "{0}" in interaction_text_key:
                if target_name in translations["EN"]:
                    display_name = _get_text(target_name)
                    result_text = _get_text(interaction_text_key, display_name)
                    icon_target = target_name
                    return result_text, icon_target, "champion"
                
                all_categories = {**config.CHAMPIONS_BY_REGIONS, **config.CHAMPIONS_BY_SKINS}
                for category in all_categories.keys():
                    if target_name.startswith(category):
                        result_text = _get_text(interaction_text_key, target_name)
                        icon_target = category
                        return result_text, icon_target, "champion"

                result_text = _get_text(interaction_text_key, target_name)
                return result_text, target_name, "champion"
            else:
                return _get_text(interaction_text_key), target_name, "generic"

    if name_part.startswith("Assist"):
        target_name = name_part.replace("Assist", "")
        return _get_text("Assist {0}", target_name), target_name if target_name else "General", "champion"

    parsed_text = re.sub(r'((?<=[a-z])[A-Z]|(?<!\A)[A-Z](?=[a-z]))', r' \1', name_part).strip()
    words = parsed_text.split()
    target_for_icon = words[-1] if words else name_part
    return parsed_text, target_for_icon, "generic"