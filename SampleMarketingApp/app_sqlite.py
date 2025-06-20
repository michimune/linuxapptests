"""
SQLite setup for development/testing without PostgreSQL
"""

from flask import Flask, render_template, jsonify
from flask_sqlalchemy import SQLAlchemy
from dotenv import load_dotenv
import os

# Load environment variables
load_dotenv()

app = Flask(__name__)

# Use SQLite for development if PostgreSQL is not available
app.config['SQLALCHEMY_DATABASE_URI'] = 'sqlite:///marketing_app.db'
app.config['SQLALCHEMY_TRACK_MODIFICATIONS'] = False
app.config['SECRET_KEY'] = os.getenv('SECRET_KEY', 'dev-secret-key')

db = SQLAlchemy(app)

# Database Models
class Item(db.Model):
    __tablename__ = 'items'
    
    id = db.Column(db.Integer, primary_key=True)
    name = db.Column(db.String(100), nullable=False)
    description = db.Column(db.Text)
    price = db.Column(db.Numeric(10, 2), nullable=False)
    category = db.Column(db.String(50))
    image_url = db.Column(db.String(255))
    
    def to_dict(self):
        return {
            'id': self.id,
            'name': self.name,
            'description': self.description,
            'price': float(self.price),
            'category': self.category,
            'image_url': self.image_url
        }

# Routes
@app.route('/')
def home():
    """Marketing landing page"""
    # Get featured items from database
    featured_items = Item.query.limit(6).all()
    return render_template('index.html', featured_items=featured_items)

@app.route('/api/items')
def get_items():
    """API endpoint to get all items"""
    items = Item.query.all()
    return jsonify([item.to_dict() for item in items])

@app.route('/api/items/<int:item_id>')
def get_item(item_id):
    """API endpoint to get a specific item"""
    item = Item.query.get_or_404(item_id)
    return jsonify(item.to_dict())

@app.route('/products')
def products():
    """Products page showing all items"""
    items = Item.query.all()
    return render_template('products.html', items=items)

# Create tables
def create_tables():
    """Create database tables"""
    with app.app_context():
        db.create_all()
        
        # Add sample data if no items exist
        if Item.query.count() == 0:
            sample_items = [
                Item(name="Premium Wireless Headphones", description="High-quality wireless headphones with active noise cancellation, 30-hour battery life, and premium sound quality.", price=299.99, category="Electronics", image_url="/static/images/headphones.jpg"),
                Item(name="Smart Fitness Watch", description="Advanced smartwatch with heart rate monitoring, GPS tracking, and waterproof design for active lifestyles.", price=399.99, category="Electronics", image_url="/static/images/smartwatch.jpg"),
                Item(name="Ergonomic Laptop Stand", description="Adjustable aluminum laptop stand designed to improve posture and reduce neck strain during long work sessions.", price=79.99, category="Accessories", image_url="/static/images/laptop-stand.jpg"),
                Item(name="Wireless Gaming Mouse", description="High-precision wireless gaming mouse with customizable RGB lighting and programmable buttons.", price=89.99, category="Accessories", image_url="/static/images/gaming-mouse.jpg"),
                Item(name="Portable Bluetooth Speaker", description="Compact portable speaker with 360-degree sound, waterproof design, and 12-hour battery life.", price=129.99, category="Electronics", image_url="/static/images/speaker.jpg"),
                Item(name="USB-C Multi-Port Hub", description="Versatile USB-C hub with HDMI, USB 3.0 ports, SD card reader, and power delivery support.", price=89.99, category="Accessories", image_url="/static/images/usb-hub.jpg"),
                Item(name="Mechanical Keyboard", description="Premium mechanical keyboard with tactile switches, customizable backlighting, and durable construction.", price=159.99, category="Accessories", image_url="/static/images/keyboard.jpg"),
                Item(name="Wireless Charging Pad", description="Fast wireless charging pad compatible with all Qi-enabled devices, with LED indicator and non-slip surface.", price=39.99, category="Electronics", image_url="/static/images/wireless-charger.jpg"),
                Item(name="HD Webcam", description="1080p HD webcam with auto-focus, built-in microphone, and privacy shutter for video calls and streaming.", price=69.99, category="Electronics", image_url="/static/images/webcam.jpg"),
                Item(name="Phone Stand", description="Adjustable phone stand made from premium materials, perfect for video calls, watching videos, and charging.", price=24.99, category="Accessories", image_url="/static/images/phone-stand.jpg"),
                Item(name="Tablet Case", description="Protective tablet case with keyboard attachment, multiple viewing angles, and premium leather finish.", price=59.99, category="Accessories", image_url="/static/images/tablet-case.jpg"),
                Item(name="Smart Home Hub", description="Central smart home hub that connects and controls all your smart devices with voice commands and app control.", price=199.99, category="Electronics", image_url="/static/images/smart-hub.jpg")
            ]
            
            for item in sample_items:
                db.session.add(item)
            
            db.session.commit()
            print("Sample data added to database")

if __name__ == '__main__':
    create_tables()
    app.run(debug=True, host='0.0.0.0', port=5000)
