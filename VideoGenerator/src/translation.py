import os
import json

def load_translations(utils_dir):
    """Loads translations from the JSON file."""
    translations_path = os.path.join(utils_dir, "translations.json")
    try:
        with open(translations_path, 'r', encoding='utf-8') as f:
            return json.load(f)
    except FileNotFoundError:
        print(f"CRITICAL ERROR: 'translations.json' not found in '{utils_dir}'.")
        print("Please make sure the translations file is present.")
        return {"EN": {"Penta Kill": "Penta Kill"}, "TR": {"Penta Kill": "Penta Kill"}}
    except json.JSONDecodeError:
        print("CRITICAL ERROR: Could not parse 'translations.json'. Check for syntax errors.")
        return {"EN": {"Penta Kill": "Penta Kill"}, "TR": {"Penta Kill": "Penta Kill"}}

def get_text(translations, selected_language, key, *args, **kwargs):
    """Gets translated text for a given key."""
    try:
        base_text = translations[selected_language][key]
        return base_text.format(*args, **kwargs)
    except KeyError:
        # Fallback to English if a key is missing in the selected language
        print(f"Warning: Translation key '{key}' not found for '{selected_language}'. Falling back to English.")
        base_text = translations["EN"].get(key, key)
        return base_text.format(*args, **kwargs)