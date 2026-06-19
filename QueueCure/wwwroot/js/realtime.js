// SignalR Real-time communication wrapper for QueueCure AI+

const QueueCureRealtime = {
    connection: null,
    listeners: new Set(),

    init() {
        if (this.connection) return;

        // Establish Hub connection pointing to program endpoint
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/hub/queue", {
                // If authenticated, send token for auth inside Hub
                accessTokenFactory: () => {
                    const session = window.QueueCureAPI ? window.QueueCureAPI.getSession() : null;
                    return session ? session.token : null;
                }
            })
            .withAutomaticReconnect()
            .build();

        // Bind incoming events to listeners
        this.connection.on("QueueUpdated", () => {
            this.trigger("QueueUpdated");
        });

        this.connection.on("TokenCalled", (data) => {
            this.trigger("TokenCalled", data);
        });

        this.connection.on("DoctorQueueUpdated", (doctorId) => {
            this.trigger("DoctorQueueUpdated", doctorId);
        });
    },

    async start() {
        this.init();
        if (this.connection.state === signalR.HubConnectionState.Disconnected) {
            try {
                await this.connection.start();
                console.log("SignalR Connection Started Successfully.");
            } catch (err) {
                console.error("SignalR Connection Failure:", err);
                // Retry after 5s
                setTimeout(() => this.start(), 5000);
            }
        }
    },

    // Event register systems
    on(event, callback) {
        this.listeners.add({ event, callback });
    },

    off(event, callback) {
        for (let listener of this.listeners) {
            if (listener.event === event && listener.callback === callback) {
                this.listeners.delete(listener);
            }
        }
    },

    trigger(event, data) {
        for (let listener of this.listeners) {
            if (listener.event === event) {
                listener.callback(data);
            }
        }
    },

    // Doctor specific live groups
    async joinDoctorGroup(doctorId) {
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
            await this.connection.invoke("JoinDoctorGroup", doctorId);
        }
    },

    async leaveDoctorGroup(doctorId) {
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
            await this.connection.invoke("LeaveDoctorGroup", doctorId);
        }
    }
};

window.QueueCureRealtime = QueueCureRealtime;
