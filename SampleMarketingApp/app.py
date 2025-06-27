from flask import Flask, render_template, jsonify
from flask_sqlalchemy import SQLAlchemy
from dotenv import load_dotenv
import os
import requests

# Load environment variables
load_dotenv()

# Check required environment variables
database_url = os.getenv('DATABASE_URL')
secret_key = os.getenv('SECRET_KEY')

if not database_url:
    print("ERROR: DATABASE_URL environment variable is not defined!")
    print("Please set DATABASE_URL in your environment or .env file")
    exit(1)

if not secret_key:
    print("ERROR: SECRET_KEY environment variable is not defined!")
    print("Please set SECRET_KEY in your environment or .env file")
    exit(1)

app = Flask(__name__)

# Database configuration
app.config['SQLALCHEMY_DATABASE_URI'] = database_url
app.config['SQLALCHEMY_TRACK_MODIFICATIONS'] = False
app.config['SECRET_KEY'] = secret_key

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
    try:
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
    
    except Exception as e:
        print(f"High memory fault endpoint failed: {e}")
        return jsonify({"error": "High memory fault failed", "details": str(e)}), 500

@app.route('/api/faults/snat')
def snat_fault():
    """Endpoint that creates multiple HttpClient instances and makes calls to www.bing.com"""
    try:
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
    
    except Exception as e:
        print(f"SNAT fault endpoint failed: {e}")
        return jsonify({"error": "SNAT fault failed", "details": str(e)}), 500

@app.route('/api/faults/highcpu')
def high_cpu_fault():
    """Endpoint that creates high CPU usage by running intensive calculations"""
    try:
        print("Starting high CPU usage test...")
        
        import threading
        import time
        import math
        
        # Track completion status
        results = {"completed_threads": 0, "total_operations": 0}
        
        def cpu_intensive_work(thread_id, duration_seconds=30):
            """Perform CPU-intensive calculations for specified duration"""
            start_time = time.time()
            operations = 0
            
            print(f"Thread {thread_id} starting CPU-intensive work...")
            
            while time.time() - start_time < duration_seconds:
                # Perform various CPU-intensive operations
                for i in range(10000):
                    # Mathematical calculations
                    _ = math.sqrt(i * math.pi)
                    _ = math.sin(i) * math.cos(i)
                    _ = math.factorial(min(i % 10, 10))  # Limit factorial to prevent overflow
                    
                    # String operations
                    _ = str(i) * 100
                    _ = "test" + str(i) + "data" * 50
                    
                    operations += 5
                
                # Brief yield to prevent complete system lock
                time.sleep(0.001)
            
            results["total_operations"] += operations
            results["completed_threads"] += 1
            print(f"Thread {thread_id} completed {operations} operations")
        
        # Get number of CPU cores (default to 4 if can't determine)
        try:
            import os
            cpu_count = os.cpu_count() or 4
        except:
            cpu_count = 4
        
        # Create threads equal to CPU count for maximum CPU usage
        threads = []
        duration = 30  # Run for 30 seconds
        
        print(f"Creating {cpu_count} threads for {duration} seconds of high CPU usage...")
        
        for i in range(cpu_count):
            thread = threading.Thread(target=cpu_intensive_work, args=(i, duration))
            threads.append(thread)
            thread.start()
        
        # Wait for all threads to complete
        for thread in threads:
            thread.join()
        
        result = {
            "message": f"High CPU test completed with {cpu_count} threads",
            "duration_seconds": duration,
            "threads_used": cpu_count,
            "completed_threads": results["completed_threads"],
            "total_operations": results["total_operations"]
        }
        
        print(f"High CPU test completed: {results['completed_threads']} threads, {results['total_operations']} operations")
        return jsonify(result)
    
    except Exception as e:
        print(f"High CPU fault endpoint failed: {e}")
        return jsonify({"error": "High CPU fault failed", "details": str(e)}), 500

@app.route('/api/faults/threads')
def thread_exhaustion_fault():
    """Endpoint that keeps creating new threads until system limits are reached"""
    try:
        print("Starting thread exhaustion test...")
        
        import threading
        import time
        
        threads = []
        thread_count = 0
        
        def dummy_thread_work(thread_id):
            """Simple thread work that keeps the thread alive"""
            try:
                print(f"Thread {thread_id} started and waiting...")
                # Keep thread alive for a while
                time.sleep(300)  # Sleep for 5 minutes
                print(f"Thread {thread_id} completed")
            except Exception as e:
                print(f"Thread {thread_id} error: {e}")
        
        # Keep creating threads until we hit system limits
        while True:
            try:
                thread_count += 1
                print(f"Creating thread {thread_count}...")
                
                # Create new thread
                thread = threading.Thread(
                    target=dummy_thread_work, 
                    args=(thread_count,),
                    daemon=True  # Daemon threads will be cleaned up when main process exits
                )
                
                threads.append(thread)
                thread.start()
                
                # Brief pause between thread creation
                time.sleep(0.01)
                
                # Safety check - if we've created a lot of threads, let's check if we should stop
                if thread_count % 100 == 0:
                    print(f"Created {thread_count} threads so far...")
                    
                # Optional: Add a reasonable upper limit to prevent complete system crash
                if thread_count >= 5000:
                    print(f"Reached safety limit of {thread_count} threads")
                    break
                    
            except OSError as e:
                # This is expected when we hit system thread limits
                print(f"Thread creation failed after {thread_count} threads: {e}")
                break
            except Exception as e:
                # Any other unexpected error
                print(f"Unexpected error creating thread {thread_count}: {e}")
                break
        
        # Give threads a moment to start
        time.sleep(1)
        
        # Count active threads
        active_threads = threading.active_count()
        
        result = {
            "message": f"Thread exhaustion test completed after creating {thread_count} threads",
            "threads_created": thread_count,
            "active_threads": active_threads,
            "status": "Thread limit reached" if thread_count >= 5000 else "System limit encountered"
        }
        
        print(f"Thread exhaustion test completed: {thread_count} threads created, {active_threads} active")
        
        # Return 500 to indicate the fault condition was reached
        return jsonify(result), 500
    
    except Exception as e:
        print(f"Thread exhaustion fault endpoint failed: {e}")
        return jsonify({"error": "Thread exhaustion fault failed", "details": str(e)}), 500

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
