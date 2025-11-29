from PIL import Image, ImageDraw, ImageFont, ImageOps
import random
import os
from . import config
from src import icon_manager

def create_image(interaction_data, lol_version):
    display_text = interaction_data["display_text"]
    original_folder = interaction_data["original_folder"]
    icon_path = interaction_data.get("icon_path")
    icon_type = interaction_data.get("icon_type")
    output_dir = interaction_data["output_dir"]
    
    try:
        background = Image.open(config.BACKGROUND_IMAGE_PATH).convert("RGBA").resize((1920, 1080))
    except FileNotFoundError:
        print(f"CRITICAL ERROR: Background file not found at {config.BACKGROUND_IMAGE_PATH}")
        print("Please make sure the 'background.png' file is in the same directory as the script.")
        return None

    icon_image = None
    if icon_path:
        try:
            border_size = 2
            final_size = (180, 180)
            icon_original = Image.open(icon_path).convert("RGBA")
            bordered_icon = Image.new('RGBA', final_size, (255, 255, 255, 255))
            icon_resized = icon_original.resize(
                (final_size[0] - border_size * 2, final_size[1] - border_size * 2),
                Image.Resampling.LANCZOS
            )
            paste_position = (border_size, border_size)
            bordered_icon.paste(icon_resized, paste_position, icon_resized)
            icon_image = bordered_icon
        except Exception as e:
            print(f"Error loading or applying border to icon from path '{icon_path}': {e}")
            icon_image = None # Ensure icon_image is None if there's an error
    
    if icon_image is None and icon_path is not None: # If icon_path was provided but image failed to load
         print(f"WARNING: Could not load icon from path '{icon_path}'.")

    draw = ImageDraw.Draw(background)
    try:
        font = ImageFont.truetype(config.FONT_PATH, size=52)
    except FileNotFoundError:
        print(f"WARNING: Font not found at {config.FONT_PATH}. Using default font.")
        font = ImageFont.load_default(size=60)

    draw.text((960, 958), display_text, font=font, fill="white", anchor="mm")

    text_bbox = draw.textbbox((960, 958), display_text, font=font, anchor="mm")
    PADDING = 20
    rect_y0 = text_bbox[1] - PADDING

    if icon_image:
        ICON_MARGIN_LEFT = 50
        ICON_MARGIN_BOTTOM = 20
        icon_x = ICON_MARGIN_LEFT
        icon_y = int(rect_y0 - icon_image.height - ICON_MARGIN_BOTTOM)
        background.paste(icon_image, (icon_x, icon_y), icon_image)

    output_filename = f"{original_folder}.png"
    output_path = os.path.join(output_dir, output_filename)
    background.save(output_path)
    return output_path
