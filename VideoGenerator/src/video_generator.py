import os
import subprocess
from . import config

def get_audio_duration(audio_path):
    try:
        cmd = [config.FFPROBE_EXE, "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", audio_path]
        result = subprocess.run(cmd, capture_output=True, text=True, check=True)
        return float(result.stdout.strip())
    except FileNotFoundError:
        print(f"Error: '{config.FFPROBE_EXE}' not found. Make sure the file is in the 'bin' folder.")
        return None
    except (subprocess.CalledProcessError, ValueError) as e:
        print(f"Error getting duration for {audio_path}: {e}")
        return None

def create_video(image_path, audio_file_paths, output_video_path, silence_duration=0.0):
    if os.path.exists(image_path) is False:
        print(f"Error: Image not found: {image_path}")
        return False
    if not audio_file_paths:
        print(f"Error: No audio files provided.")
        return False

    temp_audio_path = None
    final_audio_input = None
    concat_list_path = None
    silent_audio_path = None

    try:
        if len(audio_file_paths) > 1:
            # Create a silent audio file if needed
            if silence_duration > 0:
                silent_audio_path = os.path.join(config.CACHE_DIR, "silent_audio.wav")
                silence_cmd = [
                    config.FFMPEG_EXE, "-f", "lavfi", "-i", f"anullsrc=r=48000:cl=mono",
                    "-t", str(silence_duration), "-c:a", "pcm_s16le", "-y", silent_audio_path
                ]
                subprocess.run(silence_cmd, capture_output=True, check=True, text=True)

            concat_list_path = os.path.join(config.CACHE_DIR, "concat_list.txt")
            with open(concat_list_path, "w", encoding="utf-8") as f:
                for i, audio_path in enumerate(audio_file_paths):
                    f.write(f"file '{audio_path}'\n")
                    # Add silence after each file except the last one
                    if silent_audio_path and i < len(audio_file_paths) - 1:
                        f.write(f"file '{silent_audio_path}'\n")

            temp_audio_path = os.path.join(config.CACHE_DIR, "temp_audio.wav")
            concat_cmd = [
                config.FFMPEG_EXE, "-f", "concat", "-safe", "0", "-i", concat_list_path,
                "-c:a", "pcm_s16le", "-y", temp_audio_path
            ]
            subprocess.run(concat_cmd, capture_output=True, check=True, text=True)
            final_audio_input = temp_audio_path
        else:
            final_audio_input = audio_file_paths[0]

        audio_duration = get_audio_duration(final_audio_input)
        if audio_duration is None:
            print(f"Error: Could not get audio duration. Please check the audio file.")
            return False

        cmd = [
            config.FFMPEG_EXE, "-loop", "1", "-i", image_path, "-i", final_audio_input,
            "-c:v", "libx264", "-tune", "stillimage", "-preset", "superfast",
            "-threads", "2", "-crf", "25", "-c:a", "aac", "-t", str(audio_duration),
            "-b:a", "128k", "-pix_fmt", "yuv420p", "-shortest", "-y", output_video_path
        ]

        print(f"Creating video: {os.path.basename(output_video_path)}")
        subprocess.run(cmd, capture_output=True, check=True, text=True)
        print(f"Video saved: {output_video_path}")
        return True

    except subprocess.CalledProcessError as e:
        print(f"Error creating video with FFmpeg. Exit code: {e.returncode}\nStdout: {e.stdout}\nStderr: {e.stderr}")
        return False
    except Exception as e:
        print(f"An unexpected error occurred during video creation: {e}")
        return False
    finally:
        if concat_list_path and os.path.exists(concat_list_path):
            os.remove(concat_list_path)
        if temp_audio_path and os.path.exists(temp_audio_path):
            os.remove(temp_audio_path)
        if silent_audio_path and os.path.exists(silent_audio_path):
            os.remove(silent_audio_path)
