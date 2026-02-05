"""
This module contains the event handlers for parsing folder names.
Each handler is responsible for a specific type of event folder structure.
"""
import re
import random
from abc import ABC, abstractmethod
from . import translation
from . import data_fetcher
from . import config

class EventHandler(ABC):
    """
    Abstract base class for an event handler.
    """
    def _get_text(self, translations, selected_language, key, *args, **kwargs):
        """Helper to call the translation function."""
        return translation.get_text(translations, selected_language, key, *args, **kwargs)

    @abstractmethod
    def can_handle(self, name_part, folder_name):
        """
        Checks if this handler can process the given folder name.
        """
        pass

    @abstractmethod
    def parse(self, name_part, folder_name, translations, selected_language):
        """
        Parses the folder name to extract event details.
        """
        pass

class SimpleKeywordHandler(EventHandler):
    """
    Handles events that are identified by a simple, unique keyword in the folder name.
    """
    def __init__(self, keyword_map):
        self.keyword_map = keyword_map
        self._found_keyword = None

    def can_handle(self, name_part, folder_name):
        for keyword in self.keyword_map:
            if keyword in folder_name:
                self._found_keyword = keyword
                return True
        return False

    def parse(self, name_part, folder_name, translations, selected_language):
        if self._found_keyword:
            text_key, icon_target, icon_type = self.keyword_map[self._found_keyword]
            display_text = self._get_text(translations, selected_language, text_key)
            return display_text, icon_target, icon_type
        return None, None, None

class ItemEventHandler(EventHandler):
    """
    Handles item-related events like buying or using items.
    """
    def can_handle(self, name_part, folder_name):
        return "BuyItem" in folder_name or "UseItem" in folder_name

    def parse(self, name_part, folder_name, translations, selected_language):
        item_match = re.search(r'(BuyItem|UseItem)(2D|3D)(.*)', folder_name)
        if not item_match:
            return None, None, None

        action = item_match.group(1)
        item_name = item_match.group(3)
        
        text_key = "event_buy_item" if action == "BuyItem" else "event_use_item"
        display_text = self._get_text(translations, selected_language, text_key, item_name=item_name)
        
        return display_text, item_name, "item"

class MonsterAttackHandler(EventHandler):
    """
    Handles events related to attacking monsters.
    """
    def can_handle(self, name_part, folder_name):
        return "Attack2D" in folder_name and "General" not in folder_name

    def parse(self, name_part, folder_name, translations, selected_language):
        attack_match = re.search(r'Attack2D([A-Za-z]+)', folder_name)
        if not attack_match:
            return None, None, None

        monster_name_raw = attack_match.group(1)
        # Format monster name for consistency (e.g., BlueSentinel -> Blue_Sentinel)
        monster_name_formatted = re.sub(r'(?<!^)(?=[A-Z])', r'_', monster_name_raw)
        
        # Create a display-friendly version of the monster name
        display_monster_name = monster_name_formatted.replace("_", " ")

        display_text = self._get_text(translations, selected_language, "interaction_attack_monster", monster=display_monster_name)
        
        return display_text, monster_name_formatted, "monster"

class SkinInteractionHandler(EventHandler):
    """
    Handles complex interactions involving one or more champion skins.
    """
    def _find_skin_name(self, champion_name, skin_id):
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

    def _get_display_skin_name(self, champion_name, full_skin_name):
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

    def can_handle(self, name_part, folder_name):
        return "Skin" in name_part

    def parse(self, name_part, folder_name, translations, selected_language):
        skin_matches = re.findall(r'(\w+)Skin(\d+)', name_part)
        if not skin_matches:
            return None, None, None

        processed_champions = []
        last_actual_champion_name = None
        for champion_name_from_regex, skin_id in skin_matches:
            actual_champion_name = champion_name_from_regex
            interaction_prefixes = ["Kill", "FirstEncounter", "SecondEncounter", "MoveFirstAlly", "Move", "Assist", "AttackNear"]
            for prefix in interaction_prefixes:
                if actual_champion_name.startswith(prefix):
                    actual_champion_name = actual_champion_name[len(prefix):]
                    break

            skin_name = self._find_skin_name(actual_champion_name, int(skin_id))
            if skin_name:
                display_skin_name = self._get_display_skin_name(actual_champion_name, skin_name)
                if display_skin_name:
                    processed_champions.append(f"{actual_champion_name} {display_skin_name}")
                else:
                    processed_champions.append(actual_champion_name)
            else:
                processed_champions.append(f"{actual_champion_name} (Skin {skin_id})")
            last_actual_champion_name = actual_champion_name

        display_text = ""
        # Determine the correct translation key based on the event type and number of champions
        if "Kill" in name_part:
            key = "interaction_kill_one" if len(processed_champions) == 1 else "interaction_kill_two"
            display_text = self._get_text(translations, selected_language, key, *processed_champions)
        elif "Death" in name_part:
            key = "interaction_death_one" if len(processed_champions) == 1 else "interaction_death_two"
            display_text = self._get_text(translations, selected_language, key, *processed_champions)
        elif "Assist" in name_part:
            key = "interaction_assist_one" # Assuming assist is usually one person
            display_text = self._get_text(translations, selected_language, key, processed_champions[0])
        elif "AttackNear" in name_part:
            key = "interaction_attack_near_one" # Assuming attack near is one person
            display_text = self._get_text(translations, selected_language, key, processed_champions[0])
        elif "FirstEncounter" in name_part:
            key = "interaction_first_encounter_one" if len(processed_champions) == 1 else "interaction_first_encounter_two"
            display_text = self._get_text(translations, selected_language, key, *processed_champions)
        elif "SecondEncounter" in name_part:
            key = "interaction_second_encounter_one" if len(processed_champions) == 1 else "interaction_second_encounter_two"
            display_text = self._get_text(translations, selected_language, key, *processed_champions)
        elif "MoveFirstAlly" in name_part:
            key = "interaction_move_first_ally"
            display_text = self._get_text(translations, selected_language, key, ', '.join(processed_champions))
        else:
            key = "interaction_generic_one" if len(processed_champions) == 1 else "interaction_generic_two"
            display_text = self._get_text(translations, selected_language, key, *processed_champions)
        
        return display_text, last_actual_champion_name, "champion"

class GroupInteractionHandler(EventHandler):
    """
    Handles interactions based on champion classes or special groups (e.g., Kill3DAssassin, Respawn2DFirstNemesis).
    """
    def can_handle(self, name_part, folder_name):
        # Check against all keys in CHAMPIONS_BY_CLASS
        for group_name in config.CHAMPIONS_BY_CLASS:
            if name_part.endswith(group_name):
                return True
        return False

    def parse(self, name_part, folder_name, translations, selected_language):
        matched_group = None
        for group_name in config.CHAMPIONS_BY_CLASS:
            if name_part.endswith(group_name):
                matched_group = group_name
                break
        
        if not matched_group:
            return None, None, None

        # Determine the action prefix (e.g., "Kill", "Respawn", "FirstEncounter")
        action_prefix = name_part[:-len(matched_group)]
        
        # Get a random representative champion for the icon
        champions = config.CHAMPIONS_BY_CLASS[matched_group]
        icon_target = random.choice(champions) if champions else "General"
        
        # Get the translated name of the group
        group_translation_key = f"class_{matched_group.lower()}"
        display_group_name = self._get_text(translations, selected_language, group_translation_key)
        
        # Determine the display text based on the action (standard groups)
        if "Kill" in action_prefix:
            display_text = self._get_text(translations, selected_language, "interaction_kill_class", class_name=display_group_name)
            return display_text, icon_target, "champion"
        elif "Respawn" in action_prefix:
            display_text = self._get_text(translations, selected_language, "interaction_respawn_class", class_name=display_group_name)
            return display_text, "General", "generic"
        elif "FirstEncounter" in action_prefix:
            display_text = self._get_text(translations, selected_language, "interaction_first_encounter_class", class_name=display_group_name)
            return display_text, icon_target, "champion"
        else:
            # Fallback for other potential actions
            display_text = f"{action_prefix} {display_group_name}"
            return display_text, "General", "generic"

class MappedInteractionHandler(EventHandler):
    """
    Handles interactions based on a predefined map of keywords.
    """
    def __init__(self, interaction_map, icon_type="generic"):
        self.interaction_map = interaction_map
        self.icon_type = icon_type
        self._found_key = None

    def can_handle(self, name_part, folder_name):
        # Sort keys by length, descending, to match longer keys first
        for key in sorted(self.interaction_map.keys(), key=len, reverse=True):
            if name_part.startswith(key):
                self._found_key = key
                return True
        return False

    def parse(self, name_part, folder_name, translations, selected_language):
        if not self._found_key:
            return None, None, None

        base_text_key = self.interaction_map[self._found_key]
        target_suffix = name_part[len(self._found_key):]

        # This handler uses the translation keys directly from the map
        raw_translation = translations.get(selected_language, {}).get(base_text_key, "")
        if "{" in raw_translation: # Check if the text has a placeholder
            target_name = target_suffix or "General"
            if target_name == "General":
                if "first_encounter" in base_text_key:
                     return self._get_text(translations, selected_language, "event_first_encounter_general"), "General", "generic"
                return self._get_text(translations, selected_language, base_text_key, "General"), "General", "generic"
            
            # Clean up the target name to check for skin themes
            # Convert camel case to spaced words for matching against config.CHAMPIONS_BY_SKINS
            cleaned_target_name = re.sub(r'(?<!^)(?=[A-Z])', ' ', target_suffix).strip()
            
            # Handle special case for 'AppearanceDragon'
            if target_suffix == 'AppearanceDragon':
                champions = config.CHAMPIONS_BY_SKINS["Dragonmancer"]
                icon_target = random.choice(champions) if champions else "Aurelion Sol"
                display_name = "Appearance Dragon"
                result_text = self._get_text(translations, selected_language, base_text_key, display_name)
                return result_text, icon_target, "champion"
            
            # Check if the cleaned name corresponds to a skin theme
            if cleaned_target_name in config.CHAMPIONS_BY_SKINS:
                champions = config.CHAMPIONS_BY_SKINS[cleaned_target_name]
                icon_target = random.choice(champions) if champions else "General"
                
                skin_theme_translation_key = f"skin_theme_{cleaned_target_name.lower().replace(' ', '_')}"
                display_name = self._get_text(translations, selected_language, skin_theme_translation_key)
                
                result_text = self._get_text(translations, selected_language, base_text_key, display_name)
                return result_text, icon_target, "champion"
            
            if target_name in translations.get(selected_language, {}):
                display_name = self._get_text(translations, selected_language, target_name)
                result_text = self._get_text(translations, selected_language, base_text_key, display_name)
                return result_text, target_name, "champion"
            else:
                result_text = self._get_text(translations, selected_language, base_text_key, target_name)
                return result_text, target_name, "champion"
        else:
            if target_suffix == "General":
                final_text = self._get_text(translations, selected_language, base_text_key) + self._get_text(translations, selected_language, "suffix_in_general")
                return final_text, "General", self.icon_type
            else:
                final_text = self._get_text(translations, selected_language, base_text_key)
                if target_suffix and target_suffix != "Rare":
                    final_text += f" {target_suffix}"
                return final_text, target_suffix or "General", self.icon_type

class PrefixedEventHandler(EventHandler):
    """
    Handles events identified by a prefix, like "Kill" or "Assist".
    """
    def __init__(self, prefix, text_key, icon_type):
        self.prefix = prefix
        self.text_key = text_key
        self.icon_type = icon_type

    def can_handle(self, name_part, folder_name):
        return name_part.startswith(self.prefix)

    def parse(self, name_part, folder_name, translations, selected_language):
        target_name = name_part[len(self.prefix):]
        if self.prefix == "Kill" and target_name == "General":
            return self._get_text(translations, selected_language, "event_kill_general"), "General", "generic"
        
        display_text = self._get_text(translations, selected_language, self.text_key, target_name)
        icon_target = target_name if target_name else "General"
        return display_text, icon_target, self.icon_type

class DefaultHandler(EventHandler):
    """
    A fallback handler for any event that is not specifically handled.
    """
    def can_handle(self, name_part, folder_name):
        return True  # Always handles if no other handler does

    def parse(self, name_part, folder_name, translations, selected_language):
        parsed_text = re.sub(r'((?<=[a-z])[A-Z]|(?<!\A)[A-Z](?=[a-z]))', r' \1', name_part).strip()
        words = parsed_text.split()
        target_for_icon = words[-1] if words else name_part
        return parsed_text, target_for_icon, "generic"