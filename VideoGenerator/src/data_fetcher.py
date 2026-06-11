import os
import json
import time
import requests
import re
from . import config

def get_skins_data():
    """Gets the skin data, using cache if available."""
    if os.path.exists(config.SKINS_CACHE_PATH):
        try:
            cache_age = time.time() - os.path.getmtime(config.SKINS_CACHE_PATH)
            if cache_age < 86400:  # 24 hours in seconds
                print("Using skins cache...")
                with open(config.SKINS_CACHE_PATH, 'r', encoding='utf-8') as f:
                    return json.load(f)
        except Exception as e:
            print(f"Error reading skins cache: {e}")
            return []

    print("Downloading skins data...")
    try:
        response = requests.get("https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/default/v1/skins.json")
        response.raise_for_status()
        skins_data = response.json()

        os.makedirs(config.CACHE_DIR, exist_ok=True)
        with open(config.SKINS_CACHE_PATH, 'w', encoding='utf-8') as f:
            json.dump(skins_data, f, ensure_ascii=False, indent=2)

        print(f"Skins data downloaded and saved to cache ({len(skins_data)} skins)")
        return skins_data
    except requests.RequestException as e:
        print(f"Error downloading skins data: {e}")
        return []

def get_latest_lol_version():
    try:
        response = requests.get("https://ddragon.leagueoflegends.com/api/versions.json")
        response.raise_for_status()
        return response.json()[0]
    except requests.RequestException as e:
        print(f"Error getting LoL version: {e}")
        return None

def download_icon(url, save_path):
    """Generic function to download an image from a URL."""
    try:
        print(f"Downloading from: {url}")
        response = requests.get(url, headers={'User-Agent': 'My-Agent/1.0'})
        response.raise_for_status()
        with open(save_path, "wb") as f:
            f.write(response.content)
        return save_path
    except requests.RequestException as e:
        print(f"Error downloading {url}: {e}")
        return None

def fetch_item_icon_html():
    """Fetches the HTML content from the item icon URL."""
    try:
        response = requests.get("https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/default/assets/items/icons2d/")
        response.raise_for_status()
        return response.text
    except requests.RequestException as e:
        print(f"Error fetching item icon HTML: {e}")
        return ""

def get_all_item_icon_filenames():
    """Parses the item icon HTML to create a list of all icon filenames."""
    html_content = fetch_item_icon_html()
    if not html_content:
        return []
    pattern = re.compile(r'<a href="([^"]+\.png)"')
    return pattern.findall(html_content)



def get_monster_wiki_content():
    """Fetches the HTML content of the monster wiki page directly from the web."""
    print("Fetching monster wiki page from the web...")
    try:
        response = requests.get(config.MONSTER_WIKI_URL, headers={'User-Agent': 'My-Agent/1.0'})
        response.raise_for_status()
        return response.text
    except requests.RequestException as e:
        print(f"Error downloading monster wiki page: {e}")
        return ""

def get_monster_icon_url(monster_name_formatted):
    """
    Scrapes the League of Legends wiki for the icon URL of a given monster.
    Expects monster_name_formatted to be like "Baron_Nashor" or "Blue_Sentinel".
    """
    html_content = get_monster_wiki_content()
    if not html_content:
        return None

    # Base search names, starting with the formatted name
    current_potential_search_names = [monster_name_formatted.replace(" ", "_")]
    
    # Specific overrides or additions based on the formatted name
    if monster_name_formatted == "Elemental_Dragon":
        # If it's a generic elemental dragon, try a list of common drakes
        current_potential_search_names.extend([
            "Infernal_Drake", "Mountain_Drake", "Ocean_Drake", 
            "Cloud_Drake", "Hextech_Drake", "Chemtech_Drake", 
            "Elder_Dragon" # Include Elder as a possible fallback if specific drakes don't work
        ])
    elif "Dragon" in monster_name_formatted:
        # For other dragon-related terms (e.g., Elder_Dragon, if it comes in formatted differently)
        current_potential_search_names.append("Elder_Dragon") 
    elif "Baron" in monster_name_formatted:
        current_potential_search_names.append("Baron_Nashor")
    elif "Blue_Sentinel" in monster_name_formatted:
        current_potential_search_names.append("Blue_Buff")
    elif "Red_Brambleback" in monster_name_formatted:
        current_potential_search_names.append("Red_Buff")
    elif "Murkwolf" in monster_name_formatted:
        current_potential_search_names.append("Greater_Murk_Wolf")

    # Remove duplicates and maintain order preference (more specific or user-requested first)
    unique_search_names = []
    for name in current_potential_search_names:
        if name not in unique_search_names:
            unique_search_names.append(name)
    potential_search_names = unique_search_names

    for p_name in potential_search_names:
        # Regex to find a srcset attribute whose value contains "p_name" and "Square.png"
        # It specifically targets the "/en-us/images/thumb/...Square.png/..." pattern
        srcset_pattern = re.compile(
            r'srcset="(/en-us/images/thumb/[^"]*' + re.escape(p_name) + r'Square\.png/[^"\s]+)\s\dx"',
            re.IGNORECASE
        )
        match = srcset_pattern.search(html_content)
        
        if match:
            relative_url = match.group(1)
            full_url = f"{config.MONSTER_WIKI_BASE_URL}{relative_url}"
            return full_url
    
    # Fallback: if no srcset match, try a simpler src attribute search for the main image
    for p_name in potential_search_names:
        src_pattern = re.compile(
            r'src="(/en-us/images/[^"]*' + re.escape(p_name) + r'Square\.png)"',
            re.IGNORECASE
        )
        match = src_pattern.search(html_content)
        if match:
            relative_url = match.group(1)
            full_url = f"{config.MONSTER_WIKI_BASE_URL}{relative_url}"
            return full_url

    print(f"Could not find icon URL for monster: {monster_name_formatted}")
    return None
