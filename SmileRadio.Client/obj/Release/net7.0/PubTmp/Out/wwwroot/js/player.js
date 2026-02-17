window.smileRadio = {
    audioContext: null,
    socket: null,
    nextStartTime: 0,
    sampleRate: 48000,
    channels: 2,
    isPlaying: false,

    start: function (serverUrl) {
        if (this.isPlaying) return;
        
        console.log("Connecting " + serverUrl);
        this.socket = new WebSocket(serverUrl);
        this.socket.binaryType = 'arraybuffer';

        this.socket.onopen = () => {
            console.log("Connected to Stream");
            this.isPlaying = true;
        };

        this.socket.onmessage = (event) => {
            if (typeof event.data === 'string') {
                if (event.data.startsWith("FORMAT:")) {
                    const parts = event.data.substring(7).split(',');
                    this.sampleRate = parseInt(parts[0]);
                    this.channels = parseInt(parts[1]);
                    console.log(`Format received: ${this.sampleRate}Hz, ${this.channels}ch`);
                    this.initAudioContext();
                }
            } else {
                this.playChunk(event.data);
            }
        };

        this.socket.onclose = () => {
            console.log("Disconnected. Reconnecting in 2s...");
            this.isPlaying = false;
            setTimeout(() => this.start(serverUrl), 2000);
        };
    },

    initAudioContext: function () {
        if (!this.audioContext) {
            this.audioContext = new (window.AudioContext || window.webkitAudioContext)({
                sampleRate: this.sampleRate
            });
            this.nextStartTime = this.audioContext.currentTime;
        }
    },

    playChunk: function (arrayBuffer) {
        if (!this.audioContext) return;

        // WASAPI Loopback captures Float32 by default.
        // Incoming data is raw bytes.
        // We need to convert it to Float32Array.

        const float32Data = new Float32Array(arrayBuffer);
        
        // AudioBuffer expects planar data (separated channels), but interleaved is common in streams.
        // Wasapi usually gives interleaved (L, R, L, R...)
        // We need to de-interleave.
        
        const frameCount = float32Data.length / this.channels;
        const audioBuffer = this.audioContext.createBuffer(this.channels, frameCount, this.sampleRate);

        for (let channel = 0; channel < this.channels; channel++) {
            const channelData = audioBuffer.getChannelData(channel);
            for (let i = 0; i < frameCount; i++) {
                channelData[i] = float32Data[i * this.channels + channel];
            }
        }

        const source = this.audioContext.createBufferSource();
        source.buffer = audioBuffer;
        source.connect(this.audioContext.destination);

        // Schedule playback
        // A simple jitter buffer strategy:
        // ensure nextStartTime is at least currentTime
        if (this.nextStartTime < this.audioContext.currentTime) {
             this.nextStartTime = this.audioContext.currentTime + 0.1; // small buffer
        }

        source.start(this.nextStartTime);
        this.nextStartTime += audioBuffer.duration;
    },

    stop: function () {
        if (this.socket) {
            this.socket.close();
            this.socket = null;
        }
        if (this.audioContext) {
            this.audioContext.close();
            this.audioContext = null;
        }
        this.isPlaying = false;
    }
};
