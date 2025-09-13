class Toaster {
    constructor() {
        this.container = null;
        this.init();
    }

    init() {
        // Create the toaster container if it doesn't exist
        if (!document.getElementById('toaster-container')) {
            this.container = document.createElement('div');
            this.container.id = 'toaster-container';
            this.container.className = 'toast-container position-fixed top-0 end-0 p-3';
            this.container.style.zIndex = '1080';
            document.body.appendChild(this.container);
        } else {
            this.container = document.getElementById('toaster-container');
        }
    }

    show(message, type = 'info', duration = 3000) {
        const toastId = `toast-${Date.now()}`;
        const bgClass = this.getBackgroundClass(type);
        const icon = this.getIcon(type);

        const toastHtml = `
            <div id="${toastId}" class="toast align-items-center text-white ${bgClass} border-0" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">
                        ${icon} ${message}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
            </div>
        `;

        // Add the toast to container
        this.container.insertAdjacentHTML('beforeend', toastHtml);

        // Initialize and show the toast
        const toastElement = document.getElementById(toastId);
        const toast = new bootstrap.Toast(toastElement, {
            autohide: duration > 0,
            delay: duration
        });

        // Remove the toast element after it's hidden
        toastElement.addEventListener('hidden.bs.toast', () => {
            toastElement.remove();
        });

        toast.show();
    }

    getBackgroundClass(type) {
        const classes = {
            'success': 'bg-success',
            'error': 'bg-danger',
            'warning': 'bg-warning',
            'info': 'bg-info',
            'primary': 'bg-primary'
        };
        return classes[type] || 'bg-secondary';
    }

    getIcon(type) {
        const icons = {
            'success': '<i class="bi bi-check-circle-fill me-2"></i>',
            'error': '<i class="bi bi-x-circle-fill me-2"></i>',
            'warning': '<i class="bi bi-exclamation-triangle-fill me-2"></i>',
            'info': '<i class="bi bi-info-circle-fill me-2"></i>',
            'primary': '<i class="bi bi-bell-fill me-2"></i>'
        };
        return icons[type] || '<i class="bi bi-info-circle-fill me-2"></i>';
    }

    success(message, duration = 3000) {
        this.show(message, 'success', duration);
    }

    error(message, duration = 5000) {
        this.show(message, 'error', duration);
    }

    warning(message, duration = 4000) {
        this.show(message, 'warning', duration);
    }

    info(message, duration = 3000) {
        this.show(message, 'info', duration);
    }
}

// Create a global instance
window.toaster = new Toaster();