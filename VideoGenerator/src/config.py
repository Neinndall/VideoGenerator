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
AUDIO_DIR = os.path.join(BASE_DIR, "darius_skin67_vo_audio_mx")

# Files you MUST provide in the same folder as the script
BACKGROUND_IMAGE_PATH = os.path.join(UTILS_DIR, "background.png")
FONT_PATH = os.path.join(UTILS_DIR, "font.ttf")

# Path to the FFmpeg bin folder inside your working directory
FFMPEG_BIN_DIR = os.path.join(UTILS_DIR, "bin")
FFMPEG_EXE = os.path.join(FFMPEG_BIN_DIR, "ffmpeg.exe")
FFPROBE_EXE = os.path.join(FFMPEG_BIN_DIR, "ffprobe.exe")

# --- LANGUAGE SELECTION ---
# Supported languages: "EN" (English), "TR" (Turkish)
SELECTED_LANGUAGE = "EN"

# Cache for skins data
SKINS_CACHE_PATH = os.path.join(CACHE_DIR, "skins_data.json")

# Dictionary of champions by region/type
CHAMPIONS_BY_CATEGORY = {
    "Darkin": ["Aatrox", "Kayn", "Varus", "Naafiri"],
    "Noxus": ["Darius", "Draven", "Katarina", "Swain", "Talon", "Vladimir", "Sion", "Kled", "Samira", "Rell", "Briar", "LeBlanc", "Cassiopeia", "Riven"],
    "Ionia": ["Ahri", "Akali", "Irelia", "Jhin", "Karma", "Kayn", "Kennen", "Lee Sin", "Lillia", "Master Yi", "Rakan", "Sett", "Shen", "Syndra", "Varus", "Wukong", "Xayah", "Yasuo", "Yone", "Zed"],
    "Vastaya": ["Ahri", "Nami", "Neeko", "Rakan", "Rengar", "Wukong", "Xayah"],
    "SpiritBlossom": ["Ahri", "Aphelios", "Cassiopeia", "Darius", "Evelynn", "Kindred", "Lillia", "Master Yi", "Riven", "Sett", "Soraka", "Syndra", "Teemo", "Thresh", "Tristana", "Vayne", "Yasuo", "Yone", "Yorick", "Ashe", "Bard", "Irelia", "Ivern", "Karma", "Lux", "Morgana", "Nidalee", "Varus", "Zed", "Zyra", "Sona", "Volibear"],
    "Demacia": ["Fiora", "Galio", "Garen", "Jarvan IV", "Kayle", "Lucian", "Lux", "Morgana", "Poppy", "Quinn", "Shyvana", "Sona", "Sylas", "Vayne", "Xin Zhao"],
    "Demon": ["Fiddlesticks", "Evelynn", "Nocturne", "Shaco", "Tahm Kench", "Swain", "Yone", "Annie"],
    "Ascended": ["Azir", "Pantheon"],
    "Void": ["VelKoz", "RekSai", "Kassadin", "Kaisa"],
    "Kinkou": ["Shen", "Akali", "Kennen"],
    "Shurima": ["Akshan", "Amumu", "Sivir", "Renekton", "Taliyah", "Nasus", "Rammus", "kSante"],
    "Winterwonder": ["Lulu", "Karma", "Neeko", "Master Yi", "Annie"],
    "Coven": ["Nilah", "Elise", "Akali", "Syndra", "Nami"]
}
