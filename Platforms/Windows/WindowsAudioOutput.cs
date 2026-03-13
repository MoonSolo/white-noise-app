// Windows platform audio output — uses NAudio's WaveOutEvent.
// Add NAudio via NuGet: dotnet add package NAudio
// This file is compiled only on Windows (condition in .csproj).

#if WINDOWS
using System;
using NAudio.Wave;
using WhiteNoise.Audio;

namespace WhiteNoise.Platforms.Windows
{
    public class WindowsAudioOutput : IPlatformAudioOutput
    {
        private WaveOutEvent?       _waveOut;
        private BufferedWaveProvider? _buffer;
        private Func<float[], int, bool>? _callback;
        private System.Threading.Thread? _fillThread;
        private volatile bool _running;

        private int _sampleRate;
        private int _channels;
        private int _bufferFrames;

        public void Initialize(int sampleRate, int channels, int bufferFrames)
        {
            _sampleRate   = sampleRate;
            _channels     = channels;
            _bufferFrames = bufferFrames;

            var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            _buffer   = new BufferedWaveProvider(format)
            {
                BufferDuration    = TimeSpan.FromSeconds(2),
                DiscardOnBufferOverflow = true
            };
            _waveOut = new WaveOutEvent { DesiredLatency = 120 };
            _waveOut.Init(_buffer);
        }

        public void Start(Func<float[], int, bool> fillBufferCallback)
        {
            _callback = fillBufferCallback;
            _running  = true;
            _waveOut!.Play();

            // Producer thread: fills the NAudio ring buffer
            _fillThread = new System.Threading.Thread(() =>
            {
                var floatBuf = new float[_bufferFrames * _channels];
                var byteBuf  = new byte[floatBuf.Length * sizeof(float)];

                while (_running)
                {
                    bool cont = _callback(floatBuf, _bufferFrames);

                    // Convert float[] → byte[] (little-endian IEEE 754)
                    Buffer.BlockCopy(floatBuf, 0, byteBuf, 0, byteBuf.Length);
                    _buffer!.AddSamples(byteBuf, 0, byteBuf.Length);

                    if (!cont) { _running = false; break; }

                    // Throttle: sleep roughly half a buffer duration
                    int sleepMs = (_bufferFrames * 1000 / _sampleRate) / 2;
                    System.Threading.Thread.Sleep(Math.Max(1, sleepMs));
                }
            })
            { IsBackground = true, Name = "AudioFillThread" };

            _fillThread.Start();
        }

        public void Stop()
        {
            _running = false;
            _fillThread?.Join(500);
            _waveOut?.Stop();
        }

        public void Dispose()
        {
            Stop();
            _waveOut?.Dispose();
        }
    }
}
#endif
