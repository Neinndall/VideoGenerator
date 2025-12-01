import os

# --- CONFIGURATION ---
BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UTILS_DIR = os.path.join(BASE_DIR, "utils")
OUTPUT_BASE_DIR = os.path.join(BASE_DIR, "output")
OUTPUT_IMAGES_DIR = os.path.join(OUTPUT_BASE_DIR, "output_images")
OUTPUT_VIDEOS_DIR = os.path.join(OUTPUT_BASE_DIR, "output_videos")
CACHE_DIR = os.path.join(UTILS_DIR, "cache")
ICON_CACHE_DIR = os.path.join(CACHE_DIR, "icon_cache")
ITEM_ICON_CACHE_DIR = os.path.join(CACHE_DIR, "item_cache")
MONSTER_ICON_CACHE_DIR = os.path.join(CACHE_DIR, "monsters_cache")

# URL for monster data
MONSTER_WIKI_BASE_URL = "https://wiki.leagueoflegends.com"
MONSTER_WIKI_URL = "https://wiki.leagueoflegends.com/en-us/Monster"

FONT_PATH = os.path.join(UTILS_DIR, "font.ttf")

# Path to the FFmpeg bin folder inside your working directory
FFMPEG_BIN_DIR = os.path.join(UTILS_DIR, "bin")
FFMPEG_EXE = os.path.join(FFMPEG_BIN_DIR, "ffmpeg.exe")
FFPROBE_EXE = os.path.join(FFMPEG_BIN_DIR, "ffprobe.exe")

# --- LANGUAGE SELECTION ---
# Supported languages: "EN" (English), "TR" (Turkish)
SELECTED_LANGUAGE = "EN"

# --- AUDIO SILENCE ---
# Default silence duration between audio tracks in seconds.
SILENCE_DURATION = 0.0

# Cache for skins data
SKINS_CACHE_PATH = os.path.join(CACHE_DIR, "skins_data.json")


# Dictionary of champions by region/type
CHAMPIONS_BY_REGIONS = {
    "Bandle City": ["Corki", "Lulu", "Yuumi", "Veigar"],
    "Bilgewater": ["Fizz", "Gangplank", "Graves", "Nautilus"],
    "Demacia": ["Fiora", "Galio", "Garen", "Lux", "Poppy", "Lucian"],
    "Ixtal": ["Malphite", "Milio", "Neeko", "Nidalee", "Qiyana", "Rengar"],
    "Darkin": ["Aatrox", "Kayn", "Varus", "Naafiri"],
    "Noxus": ["Darius", "Draven", "Katarina", "Swain", "Talon", "Vladimir", "Sion", "Kled", "Samira", "Rell", "Briar", "LeBlanc", "Cassiopeia", "Riven"],
    "Ionia": ["Ahri", "Akali", "Irelia", "Jhin", "Karma", "Kayn", "Kennen", "Lee Sin", "Lillia", "Master Yi", "Rakan", "Sett", "Shen", "Syndra", "Varus", "Wukong", "Xayah", "Yasuo", "Yone", "Zed"],
    "Vastaya": ["Ahri", "Nami", "Neeko", "Rakan", "Rengar", "Wukong", "Xayah"],
    "Demon": ["Fiddlesticks", "Evelynn", "Nocturne", "Shaco", "Tahm Kench", "Swain", "Yone", "Annie"],
    "Ascended": ["Azir", "Pantheon"],
    "Void": ["VelKoz", "RekSai", "Kassadin", "Kaisa"],
    "Kinkou": ["Shen", "Akali", "Kennen"],
    "Shurima": ["Akshan", "Amumu", "Sivir", "Renekton", "Taliyah", "Nasus", "Rammus", "kSante"],
}

CHAMPIONS_BY_SKINS = {
    "Spirit Blossom": ["Ahri", "Aphelios", "Cassiopeia", "Darius", "Evelynn", "Kindred", "Lillia", "Master Yi", "Riven", "Sett", "Soraka", "Syndra", "Teemo", "Thresh", "Tristana", "Vayne", "Yasuo", "Yone", "Yorick", "Ashe", "Bard", "Irelia", "Ivern", "Karma", "Lux", "Morgana", "Nidalee", "Varus", "Zed", "Zyra", "Sona", "Volibear"],
    "Winterwonder": ["Lulu", "Karma", "Neeko", "Master Yi", "Annie"],
    "Coven": ["Nilah", "Elise", "Akali", "Syndra", "Nami"]
}