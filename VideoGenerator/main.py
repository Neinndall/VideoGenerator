import os
import sys
import traceback
import random
from src import config
from src import (
    data_fetcher,
    icon_manager,
    image_generator,
    name_parser,
    translation,
    utils,
    video_generator,
)

def print_help():
    print("""
    Video Creator Help
    ------------------
    This script generates videos from audio files and images.

    Usage:
    python main.py

    Instructions:
    1. Place the folder with the audio files in the same directory as this script.
    2. Make sure the 'background.png' file is in the same directory (1920x1080).
    3. Make sure the 'font.ttf' file is in the same directory.
    4. The script automatically detects the audio folder and processes its contents.
    5. Output images are saved in the 'output_images' folder.
    6. Output videos are saved in the 'output_videos' folder.

    Arguments:
    -h, --help: Show this help message.
    """)

def select_language_interactively(translations):
    """Asks the user to select a language and returns the code."""
    print("Please select a language:")
    
    lang_codes = list(translations.keys())
    lang_map = {}
    
    for i, code in enumerate(lang_codes):
        full_name = "English" if code == "EN" else "Turkish" if code == "TR" else code
        print(f"  {i+1}. {full_name}")
        lang_map[str(i+1)] = code
        
    choice = input(f"Enter number (1-{len(lang_codes)}): ")
    
    if choice in lang_map:
        selected_lang = lang_map[choice]
        print(f"Language set to {selected_lang}.")
    else:
        default_lang = config.SELECTED_LANGUAGE
        print(f"Invalid choice. Defaulting to {default_lang}.")
        selected_lang = default_lang
        
    print("-" * 20)
    return selected_lang

def select_silence_duration_interactively():
    """Asks the user to select the silence duration between audio tracks."""
    print("Please select silence duration between concatenated audio tracks:")
    
    silence_options = {
        "1": 0.0,
        "2": 1.0,
        "3": 2.0,
        "4": 3.0,
    }
    
    for key, value in silence_options.items():
        label = "None" if value == 0.0 else f"{value}s"
        print(f"  {key}. {label}")
        
    choice = input(f"Enter number (1-{len(silence_options)}): ")
    
    if choice in silence_options:
        duration = silence_options[choice]
        print(f"Silence duration set to {duration}s.")
    else:
        duration = config.SILENCE_DURATION
        print(f"Invalid choice. Defaulting to {duration}s.")
        
    print("-" * 20)
    return duration

def main():
    if "-h" in sys.argv or "--help" in sys.argv:
        print_help()
        return

    # Load translations once
    translations = translation.load_translations(config.UTILS_DIR)

    # Set language and silence duration at the beginning
    selected_language = select_language_interactively(translations)
    silence_duration = select_silence_duration_interactively()


    print("Starting automated image and video generator...")
    os.makedirs(config.OUTPUT_IMAGES_DIR, exist_ok=True)
    os.makedirs(config.OUTPUT_VIDEOS_DIR, exist_ok=True)
    os.makedirs(config.ICON_CACHE_DIR, exist_ok=True)
    os.makedirs(config.ITEM_ICON_CACHE_DIR, exist_ok=True)
    os.makedirs(config.MONSTER_ICON_CACHE_DIR, exist_ok=True)


    icon_manager.print_cache_stats()

    if os.path.exists(config.BACKGROUND_IMAGE_PATH) is False or os.path.exists(config.FONT_PATH) is False:
        print("\n--- ACTION REQUIRED! ---")
        print("Missing essential files. Please ensure the following files exist in the utils folder directory:")
        print(f"- Background: '{os.path.basename(config.BACKGROUND_IMAGE_PATH)}' (1920x1080 image)")
        print(f"- Font: '{os.path.basename(config.FONT_PATH)}' (.ttf or .otf font file)")
        print("--------------------------")
        return

    if utils.check_ffmpeg_installed() is False:
        print("\n--- FFMPEG NOT FOUND! ---")
        print(f"Make sure 'ffmpeg.exe' and 'ffprobe.exe' are in the '{config.FFMPEG_BIN_DIR}' folder.")
        print("------------------------------")
        return

    lol_version = data_fetcher.get_latest_lol_version()
    if lol_version is None:
        print("Could not get LoL version, but will try to use existing cache.")


    audio_directories = utils.detect_audio_directories()
    if not audio_directories:
        print("No audio folders found to process.")
        return

    total_images_generated = 0
    total_videos_generated = 0
    initial_cache_size = len(icon_manager.get_cached_icons())

    print(f"\n--- {len(audio_directories)} AUDIO FOLDERS WILL BE PROCESSED ---")
    for i, audio_dir in enumerate(audio_directories, 1):
        print(f"\n--- ({i}/{len(audio_directories)}) Processing Audio Directory: {os.path.basename(audio_dir)} ---")
        
        audio_folder_name = os.path.basename(audio_dir)
        specific_video_output_dir = os.path.join(config.OUTPUT_VIDEOS_DIR, audio_folder_name)
        os.makedirs(specific_video_output_dir, exist_ok=True)
        specific_image_output_dir = os.path.join(config.OUTPUT_IMAGES_DIR, audio_folder_name)
        os.makedirs(specific_image_output_dir, exist_ok=True)

        if not os.path.isdir(audio_dir):
            print(f"Error: Audio directory '{audio_dir}' does not exist. Skipping...")
            continue
            
        folders = [d for d in os.listdir(audio_dir) if os.path.isdir(os.path.join(audio_dir, d))]
        print(f"Found {len(folders)} event folders in '{audio_folder_name}'")
        
        for folder in folders:
            print(f"\n- Processing event: {folder}")
            try:
                expected_image_filename = f"{folder}.png"
                image_output_path = os.path.join(specific_image_output_dir, expected_image_filename)

                if os.path.exists(image_output_path):
                    print(f"  ✓ Image already exists: '{expected_image_filename}'. Skipping creation.")
                else:
                    print("  - Creating image...")
                    # Pass the selected language to the parsing function
                    display_text, target_for_icon, icon_type = name_parser.parse_folder_name(folder, translations, selected_language)
                    print(f"  Folder Processed: {folder} --> Parsed Text: {display_text}")
                    
                    # If the target is a category (like "Void", "Noxus"), pick a random champion from it.
                    icon_lookup_name = target_for_icon
                    all_categories = {**config.CHAMPIONS_BY_REGIONS, **config.CHAMPIONS_BY_SKINS}
                    if target_for_icon in all_categories:
                        icon_lookup_name = random.choice(all_categories[target_for_icon])
                        print(f"  - Category '{target_for_icon}' detected, randomly selected champion: {icon_lookup_name}")

                    icon_path = None
                    if icon_type == "item":
                        icon_path = icon_manager.get_item_icon(target_for_icon)
                    elif icon_type == "monster":
                        icon_path = icon_manager.get_monster_icon(target_for_icon)
                    elif icon_type == "champion":
                        # lol_version is needed for champion icons
                        # Use the icon_lookup_name which could be a random champion
                        icon_path = icon_manager.get_champion_icon(icon_lookup_name, lol_version, config.ICON_CACHE_DIR)
                    # For "generic" icon_type, icon_path remains None

                    interaction_data = {
                        "original_folder": folder,
                        "display_text": display_text,
                        "target_for_icon": target_for_icon, # Keep for potential debug/logging if needed
                        "icon_path": icon_path,
                        "icon_type": icon_type,
                        "output_dir": specific_image_output_dir
                    }
                    
                    created_path = image_generator.create_image(interaction_data, lol_version)
                    if created_path:
                        total_images_generated += 1
                        print(f"  ✓ Image created: {os.path.basename(created_path)}")
                        image_output_path = created_path
                    else:
                        print(f"  ✗ ERROR: Could not create image for '{folder}'.")
                        image_output_path = None

                if image_output_path:
                    audio_files_in_folder = []
                    for root, _, files in os.walk(os.path.join(audio_dir, folder)):
                        for f in files:
                            if f.lower().endswith(('.ogg')):
                                audio_files_in_folder.append(os.path.join(root, f))
                    
                    if audio_files_in_folder:
                        video_output_filename = f"{folder}.mp4"
                        video_output_path = os.path.join(specific_video_output_dir, video_output_filename)
                        
                        if len(audio_files_in_folder) > 1:
                            print(f"  - Concatenating {len(audio_files_in_folder)} audio files and creating video...")
                        else:
                            print(f"  - Creating video...")
                            
                        if video_generator.create_video(image_output_path, audio_files_in_folder, video_output_path, silence_duration):
                            total_videos_generated += 1
                            print(f"  ✓ Video created: {video_output_filename}")
                        else:
                            print(f"  ✗ ERROR creating video for '{folder}'.")
                    else:
                        print(f"  ⚠ WARNING: No audio files found in '{os.path.join(audio_dir, folder)}'.")
                else:
                    print(f"  ✗ ERROR: No image available for '{folder}'. Skipping video creation.")

            except Exception as e:
                print(f"  ✗ CRITICAL ERROR processing folder '{folder}': {e}")
                traceback.print_exc()

    final_cache_size = len(icon_manager.get_cached_icons())
    new_icons_downloaded = final_cache_size - initial_cache_size
    
    print(f"\n=== EXECUTION SUMMARY ===")
    print(f"Processed audio directories: {len(audio_directories)}")
    print(f"New images generated: {total_images_generated}")
    print(f"Videos generated: {total_videos_generated}")
    print(f"Initial cache size: {initial_cache_size}")
    print(f"New icons downloaded: {new_icons_downloaded}")
    print(f"Final cache size: {final_cache_size}")
    print(f"Image location: '{config.OUTPUT_IMAGES_DIR}'")
    print(f"Video location: '{config.OUTPUT_VIDEOS_DIR}'")
    print(f"Cache location: '{config.ICON_CACHE_DIR}'")
    print("=========================")

if __name__ == "__main__":
    main()

