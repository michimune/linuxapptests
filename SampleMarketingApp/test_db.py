"""
Simple test to create database
"""

from flask import Flask
from flask_sqlalchemy import SQLAlchemy

app = Flask(__name__)
app.config['SQLALCHEMY_DATABASE_URI'] = 'sqlite:///marketing_app.db'
app.config['SQLALCHEMY_TRACK_MODIFICATIONS'] = False
db = SQLAlchemy(app)

class Item(db.Model):
    __tablename__ = 'items'
    
    id = db.Column(db.Integer, primary_key=True)
    name = db.Column(db.String(100), nullable=False)
    description = db.Column(db.Text)
    price = db.Column(db.Numeric(10, 2), nullable=False)
    category = db.Column(db.String(50))
    image_url = db.Column(db.String(255))

with app.app_context():
    db.create_all()
    print("Database created successfully!")
    
    # Add a test item
    if Item.query.count() == 0:
        test_item = Item(
            name="Test Product",
            description="This is a test product",
            price=99.99,
            category="Test",
            image_url="/static/images/test.jpg"
        )
        db.session.add(test_item)
        db.session.commit()
        print("Test data added!")
    
    # Count items
    count = Item.query.count()
    print(f"Total items in database: {count}")
