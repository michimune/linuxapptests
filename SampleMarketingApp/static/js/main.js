// Main JavaScript for Marketing App

document.addEventListener('DOMContentLoaded', function() {
    initializeApp();
});

function initializeApp() {
    // Initialize smooth scrolling
    initSmoothScrolling();
    
    // Initialize product filtering
    initProductFiltering();
    
    // Initialize product modals
    initProductModals();
    
    // Initialize add to cart functionality
    initAddToCart();
    
    // Initialize animations
    initAnimations();
}

// Smooth scrolling for anchor links
function initSmoothScrolling() {
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                target.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        });
    });
}

// Product filtering functionality
function initProductFiltering() {
    const categoryFilter = document.getElementById('categoryFilter');
    const sortBy = document.getElementById('sortBy');
    const productsGrid = document.getElementById('productsGrid');
    const emptyState = document.getElementById('emptyState');
    
    if (!categoryFilter || !sortBy || !productsGrid) return;
    
    categoryFilter.addEventListener('change', filterAndSortProducts);
    sortBy.addEventListener('change', filterAndSortProducts);
    
    function filterAndSortProducts() {
        const selectedCategory = categoryFilter.value;
        const selectedSort = sortBy.value;
        const products = Array.from(document.querySelectorAll('.product-item'));
        
        // Filter products
        let visibleProducts = products.filter(product => {
            const productCategory = product.dataset.category;
            return selectedCategory === '' || productCategory === selectedCategory;
        });
        
        // Sort products
        visibleProducts.sort((a, b) => {
            switch (selectedSort) {
                case 'name':
                    const nameA = a.querySelector('.product-title').textContent;
                    const nameB = b.querySelector('.product-title').textContent;
                    return nameA.localeCompare(nameB);
                case 'price-low':
                    return parseFloat(a.dataset.price) - parseFloat(b.dataset.price);
                case 'price-high':
                    return parseFloat(b.dataset.price) - parseFloat(a.dataset.price);
                default:
                    return 0;
            }
        });
        
        // Hide all products
        products.forEach(product => {
            product.style.display = 'none';
        });
        
        // Show filtered and sorted products
        if (visibleProducts.length > 0) {
            visibleProducts.forEach((product, index) => {
                product.style.display = 'block';
                product.style.order = index;
            });
            emptyState.style.display = 'none';
        } else {
            emptyState.style.display = 'block';
        }
        
        // Add fade-in animation
        visibleProducts.forEach((product, index) => {
            setTimeout(() => {
                product.classList.add('fade-in');
            }, index * 100);
        });
    }
}

// Product modal functionality
function initProductModals() {
    const productCards = document.querySelectorAll('.product-card');
    const modal = document.getElementById('productModal');
    
    if (!modal) return;
    
    productCards.forEach(card => {
        const quickViewBtn = card.querySelector('.product-overlay .btn');
        if (quickViewBtn) {
            quickViewBtn.addEventListener('click', function(e) {
                e.stopPropagation();
                openProductModal(card);
            });
        }
        
        // Also allow clicking on the product image to open modal
        const productImage = card.querySelector('.product-image');
        if (productImage) {
            productImage.addEventListener('click', function() {
                openProductModal(card);
            });
        }
    });
      function openProductModal(productCard) {
        const title = productCard.querySelector('.product-title').textContent;
        const description = productCard.querySelector('.product-description').textContent;
        const price = productCard.querySelector('.product-price').textContent;
        const category = productCard.querySelector('.badge').textContent;
        const image = productCard.querySelector('.product-img');
        
        document.getElementById('modalTitle').textContent = title;
        document.getElementById('modalDescription').textContent = description;
        document.getElementById('modalPrice').textContent = price;
        document.getElementById('modalCategory').textContent = category;
        
        // Update modal image if available
        const modalImage = document.getElementById('modalImage');
        if (image && modalImage) {
            modalImage.src = image.src;
            modalImage.alt = image.alt;
        }
        
        const bootstrapModal = new bootstrap.Modal(modal);
        bootstrapModal.show();
    }
}

// Add to cart functionality
function initAddToCart() {
    document.addEventListener('click', function(e) {
        if (e.target.classList.contains('add-to-cart-btn') || 
            e.target.closest('.add-to-cart-btn')) {
            
            const button = e.target.classList.contains('add-to-cart-btn') ? 
                          e.target : e.target.closest('.add-to-cart-btn');
            
            addToCart(button);
        }
    });
    
    function addToCart(button) {
        const productId = button.dataset.productId;
        const originalText = button.innerHTML;
        
        // Show loading state
        button.innerHTML = '<span class="loading"></span> Adding...';
        button.disabled = true;
        
        // Simulate API call
        setTimeout(() => {
            // Show success state
            button.innerHTML = '<i class="fas fa-check me-1"></i>Added!';
            button.classList.remove('btn-primary');
            button.classList.add('btn-success');
            
            // Show success notification
            showNotification('Product added to cart successfully!', 'success');
            
            // Reset button after 2 seconds
            setTimeout(() => {
                button.innerHTML = originalText;
                button.classList.remove('btn-success');
                button.classList.add('btn-primary');
                button.disabled = false;
            }, 2000);
        }, 1000);
    }
}

// Animation on scroll
function initAnimations() {
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };
    
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('fade-in');
                observer.unobserve(entry.target);
            }
        });
    }, observerOptions);
    
    // Observe elements for animation
    document.querySelectorAll('.feature-card, .product-card, .contact-item').forEach(el => {
        observer.observe(el);
    });
}

// Notification system
function showNotification(message, type = 'info') {
    const notification = document.createElement('div');
    notification.className = `alert alert-${type} alert-dismissible position-fixed`;
    notification.style.cssText = `
        top: 100px;
        right: 20px;
        z-index: 9999;
        min-width: 300px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
    `;
    
    notification.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    
    document.body.appendChild(notification);
    
    // Auto-dismiss after 5 seconds
    setTimeout(() => {
        if (notification.parentNode) {
            notification.remove();
        }
    }, 5000);
}

// API helper functions
async function fetchProducts() {
    try {
        const response = await fetch('/api/items');
        if (!response.ok) throw new Error('Failed to fetch products');
        return await response.json();
    } catch (error) {
        console.error('Error fetching products:', error);
        showNotification('Failed to load products. Please try again.', 'danger');
        return [];
    }
}

async function fetchProduct(productId) {
    try {
        const response = await fetch(`/api/items/${productId}`);
        if (!response.ok) throw new Error('Failed to fetch product');
        return await response.json();
    } catch (error) {
        console.error('Error fetching product:', error);
        showNotification('Failed to load product details. Please try again.', 'danger');
        return null;
    }
}

// Utility functions
function formatPrice(price) {
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD'
    }).format(price);
}

function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// Export functions for use in other scripts
window.MarketingApp = {
    showNotification,
    fetchProducts,
    fetchProduct,
    formatPrice
};
