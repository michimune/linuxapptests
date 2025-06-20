"""
Database setup script for SampleMarketingApp
This script creates the database tables and populates them with sample data.
"""

from app import app, db, Item

def setup_database():
    """Create database tables and populate with sample data"""
    with app.app_context():
        # Drop all tables (use with caution in production)
        db.drop_all()
        
        # Create all tables
        db.create_all()
        
        # Sample items data
        sample_items = [
            {
                'name': 'Premium Wireless Headphones',
                'description': 'High-quality wireless headphones with active noise cancellation, 30-hour battery life, and premium sound quality.',
                'price': 299.99,
                'category': 'Electronics',
                'image_url': '/static/images/headphones.jpg'
            },
            {
                'name': 'Smart Fitness Watch',
                'description': 'Advanced smartwatch with heart rate monitoring, GPS tracking, and waterproof design for active lifestyles.',
                'price': 399.99,
                'category': 'Electronics',
                'image_url': '/static/images/smartwatch.jpg'
            },
            {
                'name': 'Ergonomic Laptop Stand',
                'description': 'Adjustable aluminum laptop stand designed to improve posture and reduce neck strain during long work sessions.',
                'price': 79.99,
                'category': 'Accessories',
                'image_url': '/static/images/laptop-stand.jpg'
            },
            {
                'name': 'Wireless Gaming Mouse',
                'description': 'High-precision wireless gaming mouse with customizable RGB lighting and programmable buttons.',
                'price': 89.99,
                'category': 'Accessories',
                'image_url': '/static/images/gaming-mouse.jpg'
            },
            {
                'name': 'Portable Bluetooth Speaker',
                'description': 'Compact portable speaker with 360-degree sound, waterproof design, and 12-hour battery life.',
                'price': 129.99,
                'category': 'Electronics',
                'image_url': '/static/images/speaker.jpg'
            },
            {
                'name': 'USB-C Multi-Port Hub',
                'description': 'Versatile USB-C hub with HDMI, USB 3.0 ports, SD card reader, and power delivery support.',
                'price': 89.99,
                'category': 'Accessories',
                'image_url': '/static/images/usb-hub.jpg'
            },
            {
                'name': 'Mechanical Keyboard',
                'description': 'Premium mechanical keyboard with tactile switches, customizable backlighting, and durable construction.',
                'price': 159.99,
                'category': 'Accessories',
                'image_url': '/static/images/keyboard.jpg'
            },
            {
                'name': 'Wireless Charging Pad',
                'description': 'Fast wireless charging pad compatible with all Qi-enabled devices, with LED indicator and non-slip surface.',
                'price': 39.99,
                'category': 'Electronics',
                'image_url': '/static/images/wireless-charger.jpg'
            },
            {
                'name': 'HD Webcam',
                'description': '1080p HD webcam with auto-focus, built-in microphone, and privacy shutter for video calls and streaming.',
                'price': 69.99,
                'category': 'Electronics',
                'image_url': '/static/images/webcam.jpg'
            },
            {
                'name': 'Phone Stand',
                'description': 'Adjustable phone stand made from premium materials, perfect for video calls, watching videos, and charging.',
                'price': 24.99,
                'category': 'Accessories',
                'image_url': '/static/images/phone-stand.jpg'
            },
            {
                'name': 'Tablet Case',
                'description': 'Protective tablet case with keyboard attachment, multiple viewing angles, and premium leather finish.',
                'price': 59.99,
                'category': 'Accessories',
                'image_url': '/static/images/tablet-case.jpg'
            },
            {
                'name': 'Smart Home Hub',
                'description': 'Central smart home hub that connects and controls all your smart devices with voice commands and app control.',
                'price': 199.99,
                'category': 'Electronics',
                'image_url': '/static/images/smart-hub.jpg'
            }
        ]
        
        # Add items to database
        for item_data in sample_items:
            item = Item(
                name=item_data['name'],
                description=item_data['description'],
                price=item_data['price'],
                category=item_data['category'],
                image_url=item_data['image_url']
            )
            db.session.add(item)
        
        # Commit all changes
        db.session.commit()
        
        print(f"Database setup complete! Added {len(sample_items)} items to the database.")
        print("You can now run the application with: python app.py")

if __name__ == '__main__':
    setup_database()
