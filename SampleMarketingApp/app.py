from flask import Flask, render_template, jsonify, abort
from flask_sqlalchemy import SQLAlchemy
from dotenv import load_dotenv
import os
import requests
import sys

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

# Wait 5 seconds before continuing initialization
import time
print("Starting application...")
print("Waiting 5 seconds before initialization...")
time.sleep(5)
print("Initialization starting...")

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
    # Check if products feature is enabled
    products_enabled = os.getenv('PRODUCTS_ENABLED', '0')
    if products_enabled == '0' or not products_enabled:
        abort(404)
    
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

@app.route('/api/faults/badwrite')
def bad_write_fault():
    """Endpoint that attempts to write to a restricted/non-existent file path and exits the program"""
    try:
        print("Starting bad write test...")
        
        file_path = "/proc/badwrite"
        content = "ABC"
        
        print(f"Attempting to write '{content}' to {file_path}...")
        
        # Attempt to write to the restricted/non-existent path
        try:
            with open(file_path, 'w') as f:
                f.write(content)
            
            # If we somehow succeed (shouldn't happen), still exit
            print(f"Unexpected success writing to {file_path} - exiting anyway")
            sys.exit(0)
            
        except PermissionError as e:
            print(f"Permission denied writing to {file_path}: {e}")
            print("Exiting program due to permission error")
            sys.exit(1)
            
        except FileNotFoundError as e:
            print(f"File not found: {file_path}: {e}")
            print("Exiting program due to file not found error")
            sys.exit(1)
            
        except OSError as e:
            print(f"OS error writing to {file_path}: {e}")
            print("Exiting program due to OS error")
            sys.exit(1)
            
        except IOError as e:
            print(f"IO error writing to {file_path}: {e}")
            print("Exiting program due to IO error")
            sys.exit(1)
    
    except Exception as e:
        print(f"Bad write fault endpoint failed: {e}")
        print("Exiting program due to unexpected error")
        sys.exit(1)

@app.route('/api/faults/badtls')
def bad_tls_fault():
    """Endpoint that attempts to make HTTPS connection with deprecated TLS 1.0"""
    try:
        print("Starting bad TLS test...")
        
        # Get the target URL from environment variable
        webapi_url = os.getenv('WEBAPI_URL')
        if not webapi_url:
            print("ERROR: WEBAPI_URL environment variable is not defined!")
            result = {
                "error": "Configuration error",
                "details": "WEBAPI_URL environment variable is not set",
                "error_type": "ConfigurationError"
            }
            return jsonify(result), 500
        
        print(f"Attempting HTTPS connection with TLS 1.0 to: {webapi_url}")
        
        # Import required modules for TLS configuration
        try:
            import ssl
            import urllib3
            from urllib3.util.ssl_ import create_urllib3_context
            from requests.adapters import HTTPAdapter
        except ImportError as import_error:
            print(f"Missing required dependencies for TLS configuration: {import_error}")
            result = {
                "error": "Missing dependencies",
                "details": f"Required modules not available: {str(import_error)}",
                "error_type": "ImportError"
            }
            return jsonify(result), 500
        
        try:
            # Create a custom SSL context that forces TLS 1.0
            context = ssl.SSLContext(ssl.PROTOCOL_TLSv1)  # Force TLS 1.0 only
            context.check_hostname = False
            context.verify_mode = ssl.CERT_NONE
            
            # Disable urllib3 warnings for unverified HTTPS requests
            urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
            
            # Create a custom adapter for requests that uses TLS 1.0
            class TLS10Adapter(HTTPAdapter):
                def init_poolmanager(self, *args, **kwargs):
                    ctx = create_urllib3_context()
                    ctx.set_ciphers('DEFAULT@SECLEVEL=1')
                    ctx.minimum_version = ssl.TLSVersion.TLSv1
                    ctx.maximum_version = ssl.TLSVersion.TLSv1
                    kwargs['ssl_context'] = ctx
                    return super().init_poolmanager(*args, **kwargs)
            
            # Create a session with the TLS 1.0 adapter
            session = requests.Session()
            session.mount('https://', TLS10Adapter())
            
            print("Making HTTPS request with TLS 1.0...")
            response = session.get(webapi_url, timeout=30, verify=False)
            
            # If we somehow succeed (very unlikely with modern servers)
            result = {
                "message": f"Unexpected success with TLS 1.0 connection to {webapi_url}",
                "url": webapi_url,
                "status_code": response.status_code,
                "tls_version": "1.0",
                "response_size": len(response.content)
            }
            print(f"Unexpected TLS 1.0 success: {response.status_code}")
            return jsonify(result)
            
        except ssl.SSLError as e:
            print(f"SSL error with TLS 1.0 connection: {e}")
            result = {
                "error": "SSL/TLS error",
                "url": webapi_url,
                "tls_version": "1.0",
                "details": str(e),
                "error_type": "SSLError"
            }
            return jsonify(result), 500
            
        except requests.exceptions.SSLError as e:
            print(f"Requests SSL error with TLS 1.0: {e}")
            result = {
                "error": "HTTPS connection failed",
                "url": webapi_url,
                "tls_version": "1.0", 
                "details": str(e),
                "error_type": "RequestsSSLError"
            }
            return jsonify(result), 500
            
        except requests.exceptions.ConnectionError as e:
            print(f"Connection error with TLS 1.0: {e}")
            result = {
                "error": "Connection failed",
                "url": webapi_url,
                "tls_version": "1.0",
                "details": str(e),
                "error_type": "ConnectionError"
            }
            return jsonify(result), 500
            
        except requests.exceptions.Timeout as e:
            print(f"Timeout error with TLS 1.0: {e}")
            result = {
                "error": "Request timeout",
                "url": webapi_url,
                "tls_version": "1.0",
                "details": str(e),
                "error_type": "TimeoutError"
            }
            return jsonify(result), 500
            
        except Exception as e:
            print(f"Unexpected error during TLS 1.0 connection: {e}")
            result = {
                "error": "Unexpected connection error",
                "url": webapi_url,
                "tls_version": "1.0",
                "details": str(e),
                "error_type": type(e).__name__
            }
            return jsonify(result), 500
    
    except Exception as e:
        print(f"Bad TLS fault endpoint failed: {e}")
        return jsonify({"error": "Bad TLS fault failed", "details": str(e)}), 500

@app.route('/api/faults/slowcall')
def slow_call_fault():
    """Endpoint that makes HTTP connection to a slow API endpoint"""
    try:
        print("Starting slow call test...")
        
        # Get the target URL from environment variable
        webapi_url = os.getenv('WEBAPI_URL')
        if not webapi_url:
            print("ERROR: WEBAPI_URL environment variable is not defined!")
            result = {
                "error": "Configuration error",
                "details": "WEBAPI_URL environment variable is not set",
                "error_type": "ConfigurationError"
            }
            return jsonify(result), 500
        
        # Construct the slow API endpoint URL
        slow_api_url = f"{webapi_url.rstrip('/')}/slowapi"
        print(f"Making HTTP request to slow endpoint: {slow_api_url}")
        
        try:
            import time
            start_time = time.time()
            
            # Make request with extended timeout for slow responses
            print("Sending request to slow API endpoint...")
            response = requests.get(slow_api_url, timeout=120)  # 2 minute timeout
            
            end_time = time.time()
            response_time = end_time - start_time
            
            print(f"Request completed in {response_time:.2f} seconds")
            
            result = {
                "message": f"Slow call completed to {slow_api_url}",
                "url": slow_api_url,
                "status_code": response.status_code,
                "response_time_seconds": round(response_time, 2),
                "response_size": len(response.content),
                "content_type": response.headers.get('content-type', 'unknown')
            }
            
            # Log response details
            print(f"Response: {response.status_code}, Time: {response_time:.2f}s, Size: {len(response.content)} bytes")
            
            # Return 500 if the response indicates an error or if it took too long
            if response.status_code >= 400:
                result["error"] = f"HTTP error {response.status_code}"
                return jsonify(result), 500
            elif response_time > 60:  # Consider calls over 1 minute as problematic
                result["error"] = "Response time exceeded acceptable threshold"
                return jsonify(result), 500
            else:
                return jsonify(result)
            
        except requests.exceptions.Timeout as e:
            print(f"Request timeout to slow API: {e}")
            result = {
                "error": "Request timeout",
                "url": slow_api_url,
                "timeout_seconds": 120,
                "details": str(e),
                "error_type": "TimeoutError"
            }
            return jsonify(result), 500
            
        except requests.exceptions.ConnectionError as e:
            print(f"Connection error to slow API: {e}")
            result = {
                "error": "Connection failed",
                "url": slow_api_url,
                "details": str(e),
                "error_type": "ConnectionError"
            }
            return jsonify(result), 500
            
        except requests.exceptions.HTTPError as e:
            print(f"HTTP error from slow API: {e}")
            result = {
                "error": "HTTP error",
                "url": slow_api_url,
                "details": str(e),
                "error_type": "HTTPError"
            }
            return jsonify(result), 500
            
        except requests.exceptions.RequestException as e:
            print(f"Request exception to slow API: {e}")
            result = {
                "error": "Request failed",
                "url": slow_api_url,
                "details": str(e),
                "error_type": "RequestException"
            }
            return jsonify(result), 500
            
        except Exception as e:
            print(f"Unexpected error during slow API call: {e}")
            result = {
                "error": "Unexpected error",
                "url": slow_api_url,
                "details": str(e),
                "error_type": type(e).__name__
            }
            return jsonify(result), 500
    
    except Exception as e:
        print(f"Slow call fault endpoint failed: {e}")
        return jsonify({"error": "Slow call fault failed", "details": str(e)}), 500

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
