import os
import re
import subprocess
from . import config

def detect_audio_directories(base_path):
    """
    Automatically detects all audio folders inside the 'audios' subfolder of the base path.
    """
    detected_dirs = []
    # Path to the 'audios' folder
    audios_path = os.path.join(base_path, "audios")
    
    # This pattern is used to identify audio directories, e.g., "champion_vo_audio_en"
    pattern = r".*vo_audio.*"
    
    if not os.path.exists(audios_path) or not os.path.isdir(audios_path):
        return []

    try:
        for item in os.listdir(audios_path):
            item_path = os.path.join(audios_path, item)
            if os.path.isdir(item_path) and re.match(pattern, item, re.IGNORECASE):
                print(f"Audio folder detected: {item}")
                detected_dirs.append(item_path)
    except FileNotFoundError:
        return []
            
    return detected_dirs

def check_ffmpeg_installed():
    """Checks if ffmpeg and ffprobe executables are available."""
    try:
        # Use capture_output=True to prevent ffmpeg version info from printing to console
        subprocess.run([config.FFMPEG_EXE, "-version"], capture_output=True, check=True)
        subprocess.run([config.FFPROBE_EXE, "-version"], capture_output=True, check=True)
        return True
    except (FileNotFoundError, subprocess.CalledProcessError):
        return False
