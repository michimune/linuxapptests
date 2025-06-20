from flask import Flask, render_template, jsonify
from flask_sqlalchemy import SQLAlchemy
from dotenv import load_dotenv
import os
import requests

# Load environment variables
load_dotenv()

app = Flask(__name__)

# Database configuration
app.config['SQLALCHEMY_DATABASE_URI'] = os.getenv('DATABASE_URL')
app.config['SQLALCHEMY_TRACK_MODIFICATIONS'] = False
app.config['SECRET_KEY'] = os.getenv('SECRET_KEY')

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
    try:
        # Get featured items from database
        featured_items = Item.query.limit(6).all()
        return render_template('index.html', featured_items=featured_items)
    except Exception as e:
        print(f"Exception in home(): {e}")
        try:
            from setup_db import setup_database
            setup_database()
        except Exception as setup_error:
            print(f"Error calling setup_database(): {setup_error}")
        # Return a basic response if database setup fails
        return render_template('index.html', featured_items=[])

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

@app.route('/api/faults/highmemory')
def high_memory_fault():
    """Endpoint that allocates 1GB of memory repeatedly until crash"""
    print("Starting high memory allocation...")
    memory_blocks = []
    block_size = 1024 * 1024 * 1024  # 1GB
    count = 0
    
    while True:
        try:
            count += 1
            print(f"Allocating block {count} (1GB)...")
            # Allocate 1GB of memory
            memory_block = bytearray(block_size)
            memory_blocks.append(memory_block)
            # Force the memory to be actually used
            for i in range(0, block_size, 1024):
                memory_block[i] = 1
        except MemoryError:
            print(f"Memory allocation failed after {count} blocks")
            break
        except Exception as e:
            print(f"Unexpected error during memory allocation: {e}")
            break
    
    return jsonify({"message": f"Memory allocation stopped after {count} blocks", "allocated_gb": count})

@app.route('/api/faults/snat')
def snat_fault():
    """Endpoint that creates multiple HttpClient instances and makes calls to www.bing.com"""
    print("Starting SNAT port exhaustion test...")
    
    successful_calls = 0
    failed_calls = 0
    total_calls = 500
    
    for i in range(total_calls):
        try:
            # Create a new session for each request to simulate creating new HttpClient instances
            session = requests.Session()
            
            print(f"Making request {i + 1}/{total_calls} to www.bing.com...")
            response = session.get('https://www.bing.com', timeout=10)
            
            if response.status_code == 200:
                successful_calls += 1
                print(f"Request {i + 1} successful (Status: {response.status_code})")
            else:
                failed_calls += 1
                print(f"Request {i + 1} failed with status: {response.status_code}")
            
            # Close the session to release resources
            session.close()
            
        except requests.exceptions.RequestException as e:
            failed_calls += 1
            print(f"Request {i + 1} failed with exception: {e}")
        except Exception as e:
            failed_calls += 1
            print(f"Request {i + 1} failed with unexpected error: {e}")
    
    result = {
        "message": f"Completed {total_calls} requests to www.bing.com",
        "successful_calls": successful_calls,
        "failed_calls": failed_calls,
        "total_calls": total_calls
    }
    
    print(f"SNAT test completed: {successful_calls} successful, {failed_calls} failed")
    return jsonify(result)

# Create tables
def create_tables():
    """Create database tables"""
    try:
        with app.app_context():
            db.create_all()
            
            # Add sample data if no items exist
            if Item.query.count() == 0:
                sample_items = [
                    Item(name="Premium Headphones", description="High-quality wireless headphones with noise cancellation", price=299.99, category="Electronics", image_url="/static/images/headphones.jpg"),
                    Item(name="Smart Watch", description="Feature-rich smartwatch with health monitoring", price=399.99, category="Electronics", image_url="/static/images/smartwatch.jpg"),
                    Item(name="Laptop Stand", description="Ergonomic aluminum laptop stand", price=79.99, category="Accessories", image_url="/static/images/laptop-stand.jpg"),
                    Item(name="Wireless Mouse", description="Precision wireless mouse with long battery life", price=49.99, category="Accessories", image_url="/static/images/mouse.jpg"),
                    Item(name="Bluetooth Speaker", description="Portable speaker with rich sound quality", price=129.99, category="Electronics", image_url="/static/images/speaker.jpg"),
                    Item(name="USB-C Hub", description="Multi-port USB-C hub with HDMI and USB 3.0", price=89.99, category="Accessories", image_url="/static/images/usb-hub.jpg")
                ]
                
                for item in sample_items:
                    db.session.add(item)
                
                db.session.commit()
                print("Sample data added to database")
    except Exception as e:
        print(f"Error in create_tables(): {e}")
        print("Calling setup_database() from setup_db.py to initialize database...")
        try:
            from setup_db import setup_database
            setup_database()
        except Exception as setup_error:
            print(f"Error calling setup_database(): {setup_error}")
            raise

if __name__ == '__main__':
    create_tables()
    app.run(debug=True, host='0.0.0.0', port=5000)
