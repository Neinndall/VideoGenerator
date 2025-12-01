from PIL import Image, ImageDraw, ImageFont
import random
import os
from . import config

def get_random_background(utils_path):
    """Scans the utils directory for image files and returns a path to a random one."""
    supported_extensions = ('.png', '.jpg', '.jpeg')
    try:
        all_files = os.listdir(utils_path)
        image_files = [f for f in all_files if f.lower().endswith(supported_extensions)]
        if not image_files:
            return None
        return os.path.join(utils_path, random.choice(image_files))
    except FileNotFoundError:
        return None

def create_image(interaction_data, lol_version):
    display_text = interaction_data["display_text"]
    original_folder = interaction_data["original_folder"]
    icon_path = interaction_data.get("icon_path")
    output_dir = interaction_data["output_dir"]
    
    # 1. Select a random background
    background_path = get_random_background(config.UTILS_DIR)
    if not background_path:
        print(f"CRITICAL ERROR: No background image files (.png, .jpg) found in {config.UTILS_DIR}")
        print("Please make sure there is at least one image file in the 'utils' directory.")
        return None
        
    try:
        # Open all images and convert to RGBA for consistency
        background = Image.open(background_path).convert("RGBA").resize((1920, 1080))
    except Exception as e:
        print(f"CRITICAL ERROR: Could not open or process background file '{background_path}'. Error: {e}")
        return None

    # 2. Load icon if available
    icon_image = None
    if icon_path:
        try:
            border_size = 2
            final_size = (180, 180)
            icon_original = Image.open(icon_path).convert("RGBA")
            
            # Create a white background for the border
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
            # Ensure icon_image is None if there's an error
    
    if icon_image is None and icon_path is not None:
         print(f"WARNING: Could not load icon from path '{icon_path}'.")

    # 3. Prepare to draw
    draw = ImageDraw.Draw(background)
    try:
        font = ImageFont.truetype(config.FONT_PATH, size=52)
    except FileNotFoundError:
        print(f"WARNING: Font not found at {config.FONT_PATH}. Using default font.")
        font = ImageFont.load_default(size=60)

    # 4. Define fixed ribbon position for consistency
    # The ribbon is vertically centered around the text's y-anchor of 958.
    ribbon_height = 120
    text_anchor_y = 958
    ribbon_y0 = text_anchor_y - (ribbon_height // 2)
    ribbon_y1 = text_anchor_y + (ribbon_height // 2)
    
    # Create a transparent layer for the ribbon
    ribbon_layer = Image.new('RGBA', background.size, (0, 0, 0, 0))
    ribbon_draw = ImageDraw.Draw(ribbon_layer)

    # Draw the semi-transparent rectangle with a white border at a fixed position
    # Extend the rectangle horizontally beyond the canvas to hide the side borders
    ribbon_draw.rectangle(
        [-5, ribbon_y0, 1925, ribbon_y1], # Draw from x=-5 to x=1925
        fill=(0, 0, 0, 150),       # Semi-transparent black
        outline=(255, 255, 255, 255), # Solid white
        width=2
    )

    # Composite the ribbon onto the background
    background = Image.alpha_composite(background, ribbon_layer)
    draw = ImageDraw.Draw(background) # Re-create draw context for the new composited image

    # 5. Draw the text on top of the ribbon
    # The "mm" anchor ensures the text is perfectly centered within the fixed ribbon area.
    text_anchor = (960, text_anchor_y)
    draw.text(text_anchor, display_text, font=font, fill="white", anchor="mm")

    # 6. Place the icon above the ribbon
    if icon_image:
        ICON_MARGIN_LEFT = 50
        ICON_MARGIN_BOTTOM = 20
        icon_x = ICON_MARGIN_LEFT
        # Position the icon relative to the new fixed ribbon top
        icon_y = int(ribbon_y0 - icon_image.height - ICON_MARGIN_BOTTOM)
        background.paste(icon_image, (icon_x, icon_y), icon_image)

    # 7. Add a 1px white border to the entire image
    draw.rectangle((0, 0, 1919, 1079), outline="white", width=1)

    # 8. Save the final image
    output_filename = f"{original_folder}.png"
    output_path = os.path.join(output_dir, output_filename)
    background.save(output_path)
    return output_path
