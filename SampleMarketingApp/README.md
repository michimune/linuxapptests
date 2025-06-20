# SampleMarketingApp

A modern Python web application built with Flask that showcases a marketing landing page with PostgreSQL database integration for product management.

## Features

- **Beautiful Marketing Landing Page**: Modern, responsive design with hero section, features showcase, and product highlights
- **Product Management**: Complete product catalog with filtering and sorting capabilities
- **PostgreSQL Integration**: Robust database integration for storing and managing product data
- **RESTful API**: JSON API endpoints for product data access
- **Responsive Design**: Mobile-first responsive design using Bootstrap 5
- **Modern UI/UX**: Clean, professional interface with smooth animations and interactions

## Technology Stack

- **Backend**: Python Flask
- **Database**: PostgreSQL with SQLAlchemy ORM
- **Frontend**: HTML5, CSS3, JavaScript (ES6+)
- **Styling**: Bootstrap 5, Font Awesome icons
- **Database ORM**: Flask-SQLAlchemy

## Project Structure

```
SampleMarketingApp/
├── app.py                 # Main Flask application
├── setup_db.py          # Database setup script
├── requirements.txt      # Python dependencies
├── .env                 # Environment configuration
├── templates/           # HTML templates
│   ├── base.html       # Base template
│   ├── index.html      # Landing page
│   └── products.html   # Products page
└── static/             # Static assets
    ├── css/
    │   └── style.css   # Custom styles
    ├── js/
    │   └── main.js     # JavaScript functionality
    └── images/         # Product images
```

## Setup Instructions

### Prerequisites

- Python 3.8 or higher
- PostgreSQL database server
- pip (Python package installer)

### Installation

1. **Clone or navigate to the project directory**:
   ```bash
   cd SampleMarketingApp
   ```

2. **Create a virtual environment** (recommended):
   ```bash
   python -m venv venv
   
   # On Windows:
   venv\Scripts\activate
   
   # On macOS/Linux:
   source venv/bin/activate
   ```

3. **Install required packages**:
   ```bash
   pip install -r requirements.txt
   ```

4. **Configure the database**:
   - Install and start PostgreSQL
   - Create a new database named `marketing_db`
   - Update the `.env` file with your database credentials:
     ```
     DATABASE_URL=postgresql://username:password@localhost:5432/marketing_db
     ```

5. **Set up the database**:
   ```bash
   python setup_db.py
   ```

6. **Run the application**:
   ```bash
   python app.py
   ```

7. **Access the application**:
   Open your browser and navigate to `http://localhost:5000`

## Database Schema

### Items Table
- `id`: Primary key (Integer)
- `name`: Product name (String, 100 chars)
- `description`: Product description (Text)
- `price`: Product price (Decimal, 10,2)
- `category`: Product category (String, 50 chars)
- `image_url`: Product image URL (String, 255 chars)

## API Endpoints

- `GET /api/items` - Get all items
- `GET /api/items/<id>` - Get specific item by ID

## Features Overview

### Landing Page
- Hero section with call-to-action
- Features showcase
- Featured products display
- About section with statistics
- Contact information

### Products Page
- Complete product catalog
- Category filtering
- Price sorting (low to high, high to low)
- Product quick view modal
- Responsive grid layout

### Interactive Features
- Smooth scrolling navigation
- Product filtering and sorting
- Add to cart functionality (UI only)
- Product quick view modals
- Responsive design for all devices

## Environment Variables

Create a `.env` file in the root directory with the following variables:

```env
# Database configuration
DATABASE_URL=postgresql://username:password@localhost:5432/marketing_db

# Flask configuration
FLASK_ENV=development
SECRET_KEY=your-secret-key-here

# Application settings
DEBUG=True
```

## Development

### Adding New Products

You can add new products by modifying the `setup_db.py` script or by creating a simple admin interface. Each product should have:
- Name
- Description
- Price
- Category
- Image URL (optional)

### Customizing the Design

- Modify `static/css/style.css` for styling changes
- Update `templates/base.html` for layout modifications
- Edit `static/js/main.js` for functionality enhancements

## Production Deployment

For production deployment:

1. Set `DEBUG=False` in your environment
2. Use a production WSGI server like Gunicorn
3. Configure a reverse proxy (nginx)
4. Use environment variables for sensitive configuration
5. Set up proper database connection pooling

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## License

This project is created for demonstration purposes. Feel free to use and modify as needed.

## Support

For questions or issues, please check the code comments or create an issue in the repository.
