// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Carousel functionality
let currentSlide = 0;
const totalSlides = document.querySelectorAll('.carousel-item').length;

function moveCarousel(direction) {
    const track = document.querySelector('.carousel-track');
    if (!track) return;
    
    currentSlide += direction;
    
    // Wrap around if needed
    if (currentSlide < 0) {
        currentSlide = totalSlides - 1;
    } else if (currentSlide >= totalSlides) {
        currentSlide = 0;
    }
    
    // Move the track (100px per item)
    const translateX = currentSlide * -100;
    track.style.transform = `translateX(${translateX}px)`;
}

// Initialize carousel when page loads
document.addEventListener('DOMContentLoaded', function() {
    const prevButton = document.querySelector('.carousel-arrow.prev');
    const nextButton = document.querySelector('.carousel-arrow.next');
    
    if (prevButton) {
        prevButton.addEventListener('click', () => moveCarousel(-1));
    }
    
    if (nextButton) {
        nextButton.addEventListener('click', () => moveCarousel(1));
    }
});
