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
        
        // Add touch support for mobile devices
        item.addEventListener('touchend', function(e) {
            e.preventDefault(); // Prevent double-click on mobile
            const radioInput = item.querySelector('input[type="radio"]');
            if (radioInput && !radioInput.checked) {
                radioInput.checked = true;
                radioInput.dispatchEvent(new Event('change'));
            }
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
