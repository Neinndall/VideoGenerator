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
