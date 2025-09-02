/*!
 * Theme Toggle for Bootstrap 5
 * Switches between light and dark themes
 */

(function() {
    'use strict';

    // Get stored theme or default to 'light'
    const getStoredTheme = () => localStorage.getItem('theme') || 'light';
    
    // Store theme preference
    const setStoredTheme = theme => localStorage.setItem('theme', theme);
    
    // Get preferred theme from system
    const getPreferredTheme = () => {
        const storedTheme = getStoredTheme();
        if (storedTheme) {
            return storedTheme;
        }
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    };

    // Set theme on document
    const setTheme = theme => {
        document.documentElement.setAttribute('data-bs-theme', theme);
        updateThemeToggle(theme);
    };

    // Update the theme toggle button appearance
    const updateThemeToggle = theme => {
        const themeIcon = document.getElementById('theme-icon');
        const themeText = document.getElementById('theme-text');
        
        if (themeIcon && themeText) {
            if (theme === 'dark') {
                themeIcon.className = 'bi bi-moon-fill';
                themeText.textContent = 'Dark';
            } else {
                themeIcon.className = 'bi bi-sun-fill';
                themeText.textContent = 'Light';
            }
        }
    };

    // Toggle between light and dark themes
    const toggleTheme = () => {
        const currentTheme = document.documentElement.getAttribute('data-bs-theme');
        const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
        setStoredTheme(newTheme);
        setTheme(newTheme);
    };

    // Initialize theme on page load
    const initializeTheme = () => {
        const theme = getPreferredTheme();
        setTheme(theme);
    };

    // Watch for system theme changes
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
        const storedTheme = getStoredTheme();
        if (!storedTheme) {
            setTheme(getPreferredTheme());
        }
    });

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeTheme);
    } else {
        initializeTheme();
    }

    // Add click event listener when DOM is ready
    document.addEventListener('DOMContentLoaded', function() {
        const themeToggle = document.getElementById('theme-toggle');
        if (themeToggle) {
            themeToggle.addEventListener('click', toggleTheme);
        }
    });

})();