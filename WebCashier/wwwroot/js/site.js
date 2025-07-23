// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Carousel functionality
let currentSlide = 0;

function moveCarousel(direction) {
    const track = document.querySelector('.carousel-track');
    if (!track) {
        console.log('Carousel track not found');
        return;
    }
    
    const totalSlides = document.querySelectorAll('.carousel-item').length;
    const newSlide = currentSlide + direction;
    
    // Don't move if we're at the boundaries
    if (newSlide < 0 || newSlide >= totalSlides) {
        console.log(`Cannot move to slide ${newSlide}, at boundary`);
        return;
    }
    
    currentSlide = newSlide;
    
    // Move the track (90px per item + 8px gap = 98px total)
    const translateX = currentSlide * -98;
    track.style.transform = `translateX(${translateX}px)`;
    
    // Update arrow states
    updateArrowStates();
    
    console.log(`Moved to slide ${currentSlide}, translateX: ${translateX}px`);
}

function updateArrowStates() {
    const prevButton = document.querySelector('.carousel-arrow.carousel-prev');
    const nextButton = document.querySelector('.carousel-arrow.carousel-next');
    const totalSlides = document.querySelectorAll('.carousel-item').length;
    
    if (prevButton) {
        prevButton.disabled = currentSlide === 0;
    }
    
    if (nextButton) {
        nextButton.disabled = currentSlide >= totalSlides - 1;
    }
}

function goToSlide(slideIndex) {
    const track = document.querySelector('.carousel-track');
    const totalSlides = document.querySelectorAll('.carousel-item').length;
    
    if (!track || slideIndex < 0 || slideIndex >= totalSlides) {
        return;
    }
    
    currentSlide = slideIndex;
    
    // Move the track
    const translateX = currentSlide * -98;
    track.style.transform = `translateX(${translateX}px)`;
    
    // Update arrow states
    updateArrowStates();
    
    console.log(`Jumped to slide ${currentSlide}, translateX: ${translateX}px`);
}

function initializeSwipeGestures() {
    const carouselContainer = document.querySelector('.carousel-container');
    const methodCarousel = document.querySelector('.method-carousel');
    const track = document.querySelector('.carousel-track');
    
    if (!carouselContainer || !methodCarousel || !track) {
        console.log('Carousel elements not found for swipe gestures');
        return;
    }
    
    let startX = 0;
    let startY = 0;
    let deltaX = 0;
    let deltaY = 0;
    let isSwipe = false;
    let startTime = 0;
    let initialTransform = 0;
    
    // Touch start
    methodCarousel.addEventListener('touchstart', function(e) {
        startX = e.touches[0].clientX;
        startY = e.touches[0].clientY;
        deltaX = 0;
        deltaY = 0;
        isSwipe = false;
        startTime = Date.now();
        
        // Get current transform value
        const currentTransform = track.style.transform;
        const match = currentTransform.match(/translateX\(([^)]+)px\)/);
        initialTransform = match ? parseFloat(match[1]) : 0;
        
        // Disable transition for responsive dragging
        track.classList.add('swiping');
        
    }, { passive: true });
    
    // Touch move
    methodCarousel.addEventListener('touchmove', function(e) {
        if (!startX || !startY) return;
        
        const currentX = e.touches[0].clientX;
        const currentY = e.touches[0].clientY;
        
        deltaX = currentX - startX;
        deltaY = currentY - startY;
        
        // Check if this is a horizontal swipe (more horizontal than vertical movement)
        const absX = Math.abs(deltaX);
        const absY = Math.abs(deltaY);
        
        if (absX > absY && absX > 10) {
            // This is a horizontal swipe, prevent vertical scrolling
            isSwipe = true;
            e.preventDefault();
            
            // Provide visual feedback by moving the carousel with resistance
            const resistance = 0.3; // Reduce movement for better control
            const newTransform = initialTransform + (deltaX * resistance);
            track.style.transform = `translateX(${newTransform}px)`;
        }
    }, { passive: false });
    
    // Touch end
    methodCarousel.addEventListener('touchend', function(e) {
        // Re-enable transitions
        track.classList.remove('swiping');
        
        if (!isSwipe || !startX) {
            startX = 0;
            startY = 0;
            return;
        }
        
        const endTime = Date.now();
        const swipeTime = endTime - startTime;
        const absX = Math.abs(deltaX);
        const swipeSpeed = absX / swipeTime; // pixels per millisecond
        
        // Minimum swipe distance and speed thresholds
        const minSwipeDistance = 30;
        const minSwipeSpeed = 0.1; // pixels per millisecond
        
        let moved = false;
        
        if (absX > minSwipeDistance && swipeSpeed > minSwipeSpeed) {
            if (deltaX > 0) {
                // Swipe right - move to previous slide
                moveCarousel(-1);
                moved = true;
                console.log('Swipe right detected - moving to previous slide');
            } else {
                // Swipe left - move to next slide
                moveCarousel(1);
                moved = true;
                console.log('Swipe left detected - moving to next slide');
            }
        }
        
        // If no movement occurred, snap back to current position
        if (!moved) {
            const currentTransform = currentSlide * -98;
            track.style.transform = `translateX(${currentTransform}px)`;
        }
        
        // Reset values
        startX = 0;
        startY = 0;
        deltaX = 0;
        deltaY = 0;
        isSwipe = false;
        
    }, { passive: false });
    
    // Handle touch cancel
    methodCarousel.addEventListener('touchcancel', function(e) {
        track.classList.remove('swiping');
        // Snap back to current position
        const currentTransform = currentSlide * -98;
        track.style.transform = `translateX(${currentTransform}px)`;
        
        startX = 0;
        startY = 0;
        deltaX = 0;
        deltaY = 0;
        isSwipe = false;
    });
    
    // Add mouse support for desktop testing of swipe gestures
    let isMouseDown = false;
    
    methodCarousel.addEventListener('mousedown', function(e) {
        startX = e.clientX;
        startY = e.clientY;
        isMouseDown = true;
        isSwipe = false;
        startTime = Date.now();
        
        // Get current transform value
        const currentTransform = track.style.transform;
        const match = currentTransform.match(/translateX\(([^)]+)px\)/);
        initialTransform = match ? parseFloat(match[1]) : 0;
        
        track.classList.add('swiping');
        e.preventDefault();
    });
    
    methodCarousel.addEventListener('mousemove', function(e) {
        if (!isMouseDown) return;
        
        deltaX = e.clientX - startX;
        deltaY = e.clientY - startY;
        
        const absX = Math.abs(deltaX);
        const absY = Math.abs(deltaY);
        
        if (absX > absY && absX > 10) {
            isSwipe = true;
            // Provide visual feedback
            const resistance = 0.5;
            const newTransform = initialTransform + (deltaX * resistance);
            track.style.transform = `translateX(${newTransform}px)`;
        }
    });
    
    methodCarousel.addEventListener('mouseup', function(e) {
        track.classList.remove('swiping');
        
        let moved = false;
        
        if (isMouseDown && isSwipe) {
            const endTime = Date.now();
            const swipeTime = endTime - startTime;
            const absX = Math.abs(deltaX);
            const swipeSpeed = absX / swipeTime;
            
            if (absX > 30 && swipeSpeed > 0.1) {
                if (deltaX > 0) {
                    moveCarousel(-1);
                    moved = true;
                } else {
                    moveCarousel(1);
                    moved = true;
                }
            }
        }
        
        // If no movement occurred, snap back to current position
        if (!moved) {
            const currentTransform = currentSlide * -98;
            track.style.transform = `translateX(${currentTransform}px)`;
        }
        
        isMouseDown = false;
        startX = 0;
        startY = 0;
        isSwipe = false;
    });
    
    methodCarousel.addEventListener('mouseleave', function(e) {
        if (isMouseDown) {
            track.classList.remove('swiping');
            // Snap back to current position
            const currentTransform = currentSlide * -98;
            track.style.transform = `translateX(${currentTransform}px)`;
        }
        
        isMouseDown = false;
        startX = 0;
        startY = 0;
        isSwipe = false;
    });
    
    console.log('Swipe gestures initialized for carousel');
}

// Initialize carousel when page loads
document.addEventListener('DOMContentLoaded', function() {
    // Reset carousel to show first item properly
    const track = document.querySelector('.carousel-track');
    if (track) {
        track.style.transform = 'translateX(0px)';
        console.log('Carousel initialized');
    }
    
    // Debug: log all carousel items
    const items = document.querySelectorAll('.carousel-item');
    console.log(`Found ${items.length} carousel items`);
    
    const prevButton = document.querySelector('.carousel-arrow.carousel-prev');
    const nextButton = document.querySelector('.carousel-arrow.carousel-next');
    
    if (prevButton) {
        prevButton.addEventListener('click', () => moveCarousel(-1));
        console.log('Previous button attached');
    } else {
        console.log('Previous button not found');
    }
    
    if (nextButton) {
        nextButton.addEventListener('click', () => moveCarousel(1));
        console.log('Next button attached');
    } else {
        console.log('Next button not found');
    }
    
    // Set initial arrow states
    updateArrowStates();
    
    // Add swipe gesture support for the entire carousel area
    initializeSwipeGestures();
    
    // Add tap/touch support for carousel items
    items.forEach((item, index) => {
        item.addEventListener('click', function(e) {
            // Don't interfere with radio button selection
            const radioInput = item.querySelector('input[type="radio"]');
            if (radioInput && !radioInput.checked) {
                radioInput.checked = true;
                // Trigger change event for the radio button
                radioInput.dispatchEvent(new Event('change'));
            }
            
            // Move carousel to show this item
            goToSlide(index);
        });
        
        // Make items focusable for keyboard accessibility
        item.setAttribute('tabindex', '0');
        
        // Add keyboard support
        item.addEventListener('keydown', function(e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                const radioInput = item.querySelector('input[type="radio"]');
                if (radioInput && !radioInput.checked) {
                    radioInput.checked = true;
                    radioInput.dispatchEvent(new Event('change'));
                }
                goToSlide(index);
            }
        });
    });
});
