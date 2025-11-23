import os
import re
import subprocess
from . import config

def detect_audio_directories():
    """Automatically detects ALL audio folders based on the naming pattern."""
    detected_dirs = []
    possible_patterns = [r".*vo_audio.*"]
    
    for item in os.listdir(config.BASE_DIR):
        if os.path.isdir(os.path.join(config.BASE_DIR, item)):
            for pattern in possible_patterns:
                if re.match(pattern, item):
                    print(f"Audio folder detected: {item}")
                    detected_dirs.append(os.path.join(config.BASE_DIR, item))
                    break
    
    if not detected_dirs:
        default_audio_dir = os.path.join(config.BASE_DIR, "darius_skin67_vo_audio_mx")
        if os.path.exists(default_audio_dir):
            print(f"No audio folders detected automatically. Using default: {os.path.basename(default_audio_dir)}")
            detected_dirs.append(default_audio_dir)
            
    return detected_dirs

def check_ffmpeg_installed():
    try:
        subprocess.run([config.FFMPEG_EXE, "-version"], capture_output=True, check=True)
        subprocess.run([config.FFPROBE_EXE, "-version"], capture_output=True, check=True)
        return True
    except (FileNotFoundError, subprocess.CalledProcessError):
        return False
