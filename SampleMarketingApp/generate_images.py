"""
Generate placeholder JPEG images for the SampleMarketingApp
"""

from PIL import Image, ImageDraw, ImageFont
import os

def create_product_image(filename, product_name, color, icon_text="📦"):
    """Create a placeholder product image"""
    # Image dimensions
    width, height = 400, 300
    
    # Create image with gradient background
    image = Image.new('RGB', (width, height), color)
    draw = ImageDraw.Draw(image)
    
    # Create a subtle gradient effect
    for y in range(height):
        opacity = int(255 * (1 - y / height * 0.3))
        draw.line([(0, y), (width, y)], fill=(*color, opacity) if len(color) == 3 else color)
    
    # Try to use a system font, fallback to default
    try:
        # Try to load a nice font
        font_large = ImageFont.truetype("arial.ttf", 48)
        font_medium = ImageFont.truetype("arial.ttf", 24)
        font_small = ImageFont.truetype("arial.ttf", 16)
    except:
        # Fallback to default font
        font_large = ImageFont.load_default()
        font_medium = ImageFont.load_default()
        font_small = ImageFont.load_default()
    
    # Draw icon/emoji in center
    icon_bbox = draw.textbbox((0, 0), icon_text, font=font_large)
    icon_width = icon_bbox[2] - icon_bbox[0]
    icon_height = icon_bbox[3] - icon_bbox[1]
    icon_x = (width - icon_width) // 2
    icon_y = (height - icon_height) // 2 - 20
    
    # Draw semi-transparent overlay
    overlay = Image.new('RGBA', (width, height), (255, 255, 255, 100))
    image = Image.alpha_composite(image.convert('RGBA'), overlay).convert('RGB')
    draw = ImageDraw.Draw(image)
    
    # Draw icon
    draw.text((icon_x, icon_y), icon_text, fill=(80, 80, 80), font=font_large)
    
    # Draw product name
    name_bbox = draw.textbbox((0, 0), product_name, font=font_medium)
    name_width = name_bbox[2] - name_bbox[0]
    name_x = (width - name_width) // 2
    name_y = icon_y + 80
    
    # Draw text background
    draw.rectangle([name_x - 10, name_y - 5, name_x + name_width + 10, name_y + 35], 
                   fill=(255, 255, 255, 200))
    draw.text((name_x, name_y), product_name, fill=(60, 60, 60), font=font_medium)
    
    # Add "Sample Product" watermark
    watermark = "SAMPLE"
    watermark_bbox = draw.textbbox((0, 0), watermark, font=font_small)
    watermark_width = watermark_bbox[2] - watermark_bbox[0]
    draw.text((width - watermark_width - 10, height - 25), watermark, 
              fill=(200, 200, 200), font=font_small)
    
    return image

def generate_all_images():
    """Generate all product placeholder images"""
    
    # Ensure the images directory exists
    images_dir = "static/images"
    os.makedirs(images_dir, exist_ok=True)
    
    # Product definitions with colors and icons
    products = [
        {
            "filename": "headphones.jpg",
            "name": "Headphones",
            "color": (106, 90, 205),  # Slate blue
            "icon": "🎧"
        },
        {
            "filename": "smartwatch.jpg", 
            "name": "Smart Watch",
            "color": (70, 130, 180),  # Steel blue
            "icon": "⌚"
        },
        {
            "filename": "laptop-stand.jpg",
            "name": "Laptop Stand", 
            "color": (169, 169, 169),  # Dark gray
            "icon": "💻"
        },
        {
            "filename": "gaming-mouse.jpg",
            "name": "Gaming Mouse",
            "color": (220, 20, 60),  # Crimson
            "icon": "🖱️"
        },
        {
            "filename": "speaker.jpg",
            "name": "BT Speaker",
            "color": (34, 139, 34),  # Forest green
            "icon": "🔊"
        },
        {
            "filename": "usb-hub.jpg",
            "name": "USB Hub",
            "color": (255, 140, 0),  # Dark orange
            "icon": "🔌"
        },
        {
            "filename": "keyboard.jpg",
            "name": "Keyboard",
            "color": (75, 0, 130),  # Indigo
            "icon": "⌨️"
        },
        {
            "filename": "wireless-charger.jpg",
            "name": "Wireless Charger",
            "color": (0, 191, 255),  # Deep sky blue
            "icon": "⚡"
        },
        {
            "filename": "webcam.jpg",
            "name": "HD Webcam",
            "color": (178, 34, 34),  # Firebrick
            "icon": "📹"
        },
        {
            "filename": "phone-stand.jpg",
            "name": "Phone Stand",
            "color": (128, 128, 0),  # Olive
            "icon": "📱"
        },
        {
            "filename": "tablet-case.jpg",
            "name": "Tablet Case",
            "color": (139, 69, 19),  # Saddle brown
            "icon": "📱"
        },
        {
            "filename": "smart-hub.jpg",
            "name": "Smart Hub",
            "color": (25, 25, 112),  # Midnight blue
            "icon": "🏠"
        }
    ]
    
    print("Generating product images...")
    
    for product in products:
        print(f"Creating {product['filename']}...")
        
        # Create the image
        image = create_product_image(
            product["filename"],
            product["name"], 
            product["color"],
            product.get("icon", "📦")
        )
        
        # Save as JPEG
        filepath = os.path.join(images_dir, product["filename"])
        image.save(filepath, "JPEG", quality=85, optimize=True)
        
        print(f"✓ Saved {filepath}")
    
    print(f"\n🎉 Successfully generated {len(products)} product images!")
    print(f"Images saved to: {os.path.abspath(images_dir)}")

if __name__ == "__main__":
    try:
        generate_all_images()
    except Exception as e:
        print(f"Error generating images: {e}")
        import traceback
        traceback.print_exc()
