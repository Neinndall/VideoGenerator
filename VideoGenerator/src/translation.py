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
    """Gets translated and formatted text safely."""
    try:
        base_text = translations[selected_language][key]
    except KeyError:
        print(f"Warning: Translation key '{key}' not found for '{selected_language}'. Falling back to English.")
        try:
            # Attempt to fall back to English
            base_text = translations["EN"][key]
        except KeyError:
            # If it's not in English either, use the key itself as the text
            base_text = key
            
    # Only attempt to format the string if there are arguments to format it with
    if args or kwargs:
        try:
            return base_text.format(*args, **kwargs)
        except (IndexError, KeyError):
            # This can happen if the base_text (e.g., from a bad key) has placeholders
            # but the number of args doesn't match. Return the raw text as a safe fallback.
            print(f"Warning: Formatting error for key '{key}' or its fallback. Returning raw text.")
            return base_text
    
    # If no args were provided, return the unformatted text.
    # This is safe for checks that don't intend to format.
    return base_text