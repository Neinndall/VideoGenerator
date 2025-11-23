import os
import re
import difflib
from . import config
from src import data_fetcher

ITEM_ICON_FILENAMES = data_fetcher.get_all_item_icon_filenames()

def get_item_icon(item_name):
    """Gets an item's icon by searching for the best fuzzy match."""
    item_name_lower = item_name.lower()
    
    best_match_filename = None
    highest_score = 0.0
    MATCH_THRESHOLD = 0.6 # Lowered threshold to catch partial matches like 'runaans' in 'runaanshurricane'

    for filename in ITEM_ICON_FILENAMES:
        # Clean and split filename into parts. Also handle hyphens.
        filename_parts = filename.lower().replace('.png', '').replace('-', '_').split('_')
        
        current_max_score = 0.0
        for f_part in filename_parts:
            # remove all non-alphabetic characters
            f_part_clean = re.sub(r'[^a-z]', '', f_part)
            if not f_part_clean:
                continue
            
            score = difflib.SequenceMatcher(None, item_name_lower, f_part_clean).ratio()
            
            # Boost score if a part is a substring of the item name (e.g. 'runaans' in 'runaanshurricane')
            if f_part_clean in item_name_lower and len(f_part_clean) > 3: # Add len check to avoid short common substrings
                score = max(score, 0.9) # Give it a high score
            
            if score > current_max_score:
                current_max_score = score
        
        if current_max_score > highest_score:
            highest_score = current_max_score
            best_match_filename = filename

    if highest_score < MATCH_THRESHOLD:
        print(f"Item icon not found for: {item_name} (Best score: {highest_score:.2f} < {MATCH_THRESHOLD})")
        return None

    found_filename = best_match_filename
    print(f"Found best match for '{item_name}': '{found_filename}' with score {highest_score:.2f}")
    icon_path = os.path.join(config.ITEM_ICON_CACHE_DIR, found_filename)

    if os.path.exists(icon_path):
        print(f"Using item icon from cache: {found_filename}")
        return icon_path

    url = f"https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/default/assets/items/icons2d/{found_filename}"
    print(f"Downloading new item icon: {found_filename}")
    os.makedirs(os.path.dirname(icon_path), exist_ok=True)
    return data_fetcher.download_icon(url, icon_path)

def get_cached_icons():
    """Gets the list of icons already downloaded in the cache."""
    if os.path.exists(config.ICON_CACHE_DIR) is False:
        os.makedirs(config.ICON_CACHE_DIR, exist_ok=True)
        return set()
    
    cached_icons = set()
    for filename in os.listdir(config.ICON_CACHE_DIR):
        if filename.endswith('.png'):
            champion_name = filename[:-4]
            cached_icons.add(champion_name)
    return cached_icons

def get_champion_icon(champion_name, version, cache_dir):
    """Gets a champion's icon, reusing the cache if it exists."""
    champion_name_formatted = champion_name.replace(" ", "").replace("'", "")
    name_map = {"Wukong": "MonkeyKing", "MasterYi": "MasterYi", "XinZhao": "XinZhao", "LeeSin": "LeeSin", "LeBlanc": "Leblanc"}
    champion_name_formatted = name_map.get(champion_name_formatted, champion_name_formatted)

    icon_path = os.path.join(cache_dir, f"{champion_name_formatted}.png")
    if os.path.exists(icon_path):
        print(f"Using icon from cache: {champion_name_formatted}")
        return icon_path

    if version is None:
        print(f"Cannot download icon for {champion_name_formatted}: version not available")
        return None

    url = f"http://ddragon.leagueoflegends.com/cdn/{version}/img/champion/{champion_name_formatted}.png"
    print(f"Downloading new icon: {champion_name_formatted}")
    return data_fetcher.download_icon(url, icon_path)

def print_cache_stats():
    """Prints statistics of the icon cache."""
    cached_icons = get_cached_icons()
    print(f"\n--- CACHE STATISTICS ---")
    print(f"Icons in cache: {len(cached_icons)}")
    if cached_icons:
        print("Champions available in cache:")
        for icon in sorted(cached_icons):
            print(f"  - {icon}")
    print("--------------------------")
