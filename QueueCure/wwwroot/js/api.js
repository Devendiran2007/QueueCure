// Centralized Fetch Wrapper and Auth Storage for QueueCure AI+

const API_BASE = '/api';

const QueueCureAPI = {
    // Auth Token Storage helpers
    saveSession(sessionData) {
        localStorage.setItem('qc_token', sessionData.token);
        localStorage.setItem('qc_username', sessionData.username);
        localStorage.setItem('qc_fullName', sessionData.fullName);
        localStorage.setItem('qc_role', sessionData.role);
        localStorage.setItem('qc_userId', sessionData.userId);
    },

    clearSession() {
        localStorage.removeItem('qc_token');
        localStorage.removeItem('qc_username');
        localStorage.removeItem('qc_fullName');
        localStorage.removeItem('qc_role');
        localStorage.removeItem('qc_userId');
    },

    getSession() {
        return {
            token: localStorage.getItem('qc_token'),
            username: localStorage.getItem('qc_username'),
            fullName: localStorage.getItem('qc_fullName'),
            role: localStorage.getItem('qc_role'),
            userId: localStorage.getItem('qc_userId')
        };
    },

    isAuthenticated() {
        return !!localStorage.getItem('qc_token');
    },

    // HTTP request dispatcher
    async request(endpoint, method = 'GET', body = null, requireAuth = true) {
        const url = `${API_BASE}${endpoint.startsWith('/') ? endpoint : '/' + endpoint}`;
        
        const headers = {
            'Content-Type': 'application/json'
        };

        if (requireAuth) {
            const session = this.getSession();
            if (session.token) {
                headers['Authorization'] = `Bearer ${session.token}`;
            }
        }

        const config = {
            method,
            headers
        };

        if (body && (method === 'POST' || method === 'PUT')) {
            config.body = JSON.stringify(body);
        }

        try {
            const response = await fetch(url, config);
            
            // Handle HTTP 401 Unauthorized
            if (response.status === 401) {
                this.clearSession();
                // Redirect if we are in an admin/dashboard page
                if (window.location.pathname !== '/' && window.location.pathname !== '/index.html') {
                    window.location.href = '/index.html';
                }
                throw new Error("Session expired. Please log in again.");
            }

            const responseData = await response.text();
            let parsedData;
            try {
                parsedData = responseData ? JSON.parse(responseData) : null;
            } catch {
                parsedData = responseData;
            }

            if (!response.ok) {
                const errorMessage = parsedData && parsedData.message ? parsedData.message : 'An error occurred';
                throw new Error(errorMessage);
            }

            return parsedData;
        } catch (error) {
            console.error(`API Error [${method} ${endpoint}]:`, error);
            throw error;
        }
    }
};
window.QueueCureAPI = QueueCureAPI;
