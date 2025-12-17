// Custom component helpers

// Dialog management
window.customDialog = {
    modals: {},
    
    show: function(modalId) {
        const modal = document.getElementById(modalId);
        if (modal) {
            modal.style.display = 'block';
            modal.classList.add('show');
            document.body.classList.add('modal-open');
            
            // Add backdrop
            const backdrop = document.createElement('div');
            backdrop.className = 'modal-backdrop fade show';
            backdrop.id = modalId + '-backdrop';
            document.body.appendChild(backdrop);
        }
    },
    
    hide: function(modalId) {
        const modal = document.getElementById(modalId);
        if (modal) {
            modal.style.display = 'none';
            modal.classList.remove('show');
            document.body.classList.remove('modal-open');
            
            // Remove backdrop
            const backdrop = document.getElementById(modalId + '-backdrop');
            if (backdrop) {
                backdrop.remove();
            }
        }
    },
    
    toggle: function(modalId) {
        const modal = document.getElementById(modalId);
        if (modal && modal.classList.contains('show')) {
            this.hide(modalId);
        } else {
            this.show(modalId);
        }
    }
};

// Theme management
window.themeManager = {
    isDarkMode: true,
    
    toggle: function() {
        this.isDarkMode = !this.isDarkMode;
        document.body.classList.toggle('light-mode', !this.isDarkMode);
        localStorage.setItem('qingfeng-theme', this.isDarkMode ? 'dark' : 'light');
        return this.isDarkMode;
    },
    
    initialize: function() {
        const savedTheme = localStorage.getItem('qingfeng-theme');
        if (savedTheme === 'light') {
            this.isDarkMode = false;
            document.body.classList.add('light-mode');
        }
    }
};

// Drawer/Sidebar management
window.drawerManager = {
    isOpen: true,
    
    toggle: function() {
        this.isOpen = !this.isOpen;
        const sidebar = document.querySelector('.sidebar-custom');
        if (sidebar) {
            sidebar.classList.toggle('collapsed', !this.isOpen);
        }
        localStorage.setItem('qingfeng-drawer', this.isOpen ? 'open' : 'closed');
        return this.isOpen;
    },
    
    initialize: function() {
        const savedState = localStorage.getItem('qingfeng-drawer');
        if (savedState === 'closed') {
            this.isOpen = false;
            const sidebar = document.querySelector('.sidebar-custom');
            if (sidebar) {
                sidebar.classList.add('collapsed');
            }
        }
    }
};

// Snackbar/Toast notifications
window.snackbar = {
    show: function(message, type = 'info', duration = 3000) {
        const container = this.getContainer();
        
        const snackbar = document.createElement('div');
        snackbar.className = `alert alert-${type} alert-dismissible fade show`;
        snackbar.style.cssText = 'margin-bottom: 1rem; min-width: 300px;';
        snackbar.innerHTML = `
            ${message}
            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="alert" aria-label="Close"></button>
        `;
        
        container.appendChild(snackbar);
        
        // Auto remove after duration
        setTimeout(() => {
            snackbar.classList.remove('show');
            setTimeout(() => snackbar.remove(), 300);
        }, duration);
        
        return snackbar;
    },
    
    getContainer: function() {
        let container = document.getElementById('snackbar-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'snackbar-container';
            container.style.cssText = 'position: fixed; top: 20px; right: 20px; z-index: 9999;';
            document.body.appendChild(container);
        }
        return container;
    }
};

// Initialize on page load
document.addEventListener('DOMContentLoaded', function() {
    themeManager.initialize();
    drawerManager.initialize();
});

// Utility functions
window.customUtils = {
    // Copy to clipboard
    copyToClipboard: async function(text) {
        try {
            await navigator.clipboard.writeText(text);
            snackbar.show('已复制到剪贴板', 'success', 2000);
            return true;
        } catch (err) {
            console.error('Failed to copy:', err);
            snackbar.show('复制失败', 'danger', 2000);
            return false;
        }
    },
    
    // Format bytes to human readable
    formatBytes: function(bytes, decimals = 2) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const dm = decimals < 0 ? 0 : decimals;
        const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
    }
};
