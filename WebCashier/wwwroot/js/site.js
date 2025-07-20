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
    console.log(`Total slides: ${totalSlides}`);
    
    currentSlide += direction;
    
    // Wrap around if needed
    if (currentSlide < 0) {
        currentSlide = totalSlides - 1;
    } else if (currentSlide >= totalSlides) {
        currentSlide = 0;
    }
    
    // Move the track (90px per item + 8px gap = 98px total)
    const translateX = currentSlide * -98;
    track.style.transform = `translateX(${translateX}px)`;
    
    console.log(`Moved to slide ${currentSlide}, translateX: ${translateX}px`);
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
});
