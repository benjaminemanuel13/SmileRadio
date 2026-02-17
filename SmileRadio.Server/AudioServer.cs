using Fleck;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmileRadio.Server
{
    public class AudioServer : IDisposable
    {
        private WebSocketServer _server;
        private WasapiLoopbackCapture _capture;
        private List<IWebSocketConnection> _clients = new List<IWebSocketConnection>();
        private bool _isRunning;

        public event Action<string> Log;
        public event Action<int> ClientCountChanged;

        public void Start(int port = 5555)
        {
            if (_isRunning) return;

            try
            {
                // Initialize WebSocket Server
                // Use 0.0.0.0 to allow connections from other machines
                _server = new WebSocketServer($"ws://0.0.0.0:{port}");
                // If 0.0.0.0 fails with permissions, try 127.0.0.1 (localhost only)
                // _server = new WebSocketServer($"ws://127.0.0.1:{port}");
                _server.Start(socket =>
                {
                    socket.OnOpen = () =>
                    {
                        lock (_clients)
                        {
                            _clients.Add(socket);
                        }
                        Log?.Invoke($"Client connected: {socket.ConnectionInfo.ClientIpAddress}");
                        
                        // Send format info
                        if (_capture != null) 
                        {
                           var format = _capture.WaveFormat;
                           string formatInfo = $"FORMAT:{format.SampleRate},{format.Channels}";
                           socket.Send(formatInfo);
                        }

                        ClientCountChanged?.Invoke(_clients.Count);
                    };

                    socket.OnClose = () =>
                    {
                        lock (_clients)
                        {
                            _clients.Remove(socket);
                        }
                        Log?.Invoke($"Client disconnected: {socket.ConnectionInfo.ClientIpAddress}");
                        ClientCountChanged?.Invoke(_clients.Count);
                    };
                });

                // Initialize Audio Capture
                _capture = new WasapiLoopbackCapture();
                _capture.DataAvailable += OnAudioDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;
                _capture.StartRecording();

                _isRunning = true;
                Log?.Invoke($"Server started on port {port}. Capturing audio...");
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Error starting server: {ex.Message}");
                Stop();
            }
        }

        private void OnAudioDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_clients.Count == 0) return;

            byte[] buffer = e.Buffer;
            int bytesRecorded = e.BytesRecorded;

            // We can send the raw buffer directly.
            // Note: WASAPI Loopback usually captures at system format (often IEEE Float 32 bit).
            // We'll send it as is and handle decoding on the client side or let the client know the format.
            // For now, let's just send the raw bytes.

            // Copy the relevant part of the buffer
            byte[] dataToSend = new byte[bytesRecorded];
            Array.Copy(buffer, dataToSend, bytesRecorded);

            lock (_clients)
            {
                foreach (var client in _clients.ToList()) // ToList to avoid modification issues if ensuring valid
                {
                    if (client.IsAvailable)
                    {
                        client.Send(dataToSend);
                    }
                }
            }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            Log?.Invoke("Audio capture driver stopped.");
            if (e.Exception != null)
            {
                Log?.Invoke($"Capture Error: {e.Exception.Message}");
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _capture?.StopRecording();
            _capture?.Dispose();
            _capture = null;

            foreach (var client in _clients)
            {
                client.Close();
            }
            _clients.Clear();

            _server?.Dispose();
            _server = null;

            _isRunning = false;
            Log?.Invoke("Server stopped.");
            ClientCountChanged?.Invoke(0);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
