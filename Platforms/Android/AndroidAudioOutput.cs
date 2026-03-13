// Android platform audio output — uses Android.Media.AudioTrack.
// Compiled only on Android (condition in .csproj).

#if ANDROID
using System;
using Android.Media;
using WhiteNoise.Audio;

namespace WhiteNoise.Platforms.Android
{
    public class AndroidAudioOutput : IPlatformAudioOutput
    {
        private AudioTrack?                   _track;
        private Func<float[], int, bool>?     _callback;
        private System.Threading.Thread?      _fillThread;
        private volatile bool                 _running;

        private int _sampleRate;
        private int _channels;
        private int _bufferFrames;

        public void Initialize(int sampleRate, int channels, int bufferFrames)
        {
            _sampleRate   = sampleRate;
            _channels     = channels;
            _bufferFrames = bufferFrames;

            var channelMask = channels == 2
                ? ChannelOut.Stereo
                : ChannelOut.Mono;

            int minBuf = AudioTrack.GetMinBufferSize(
                sampleRate, channelMask, Encoding.PcmFloat);
            int bufSize = Math.Max(minBuf, bufferFrames * channels * sizeof(float));

            _track = new AudioTrack.Builder()
                .SetAudioAttributes(new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Media)
                    .SetContentType(AudioContentType.Music)
                    .Build()!)
                .SetAudioFormat(new AudioFormat.Builder()
                    .SetSampleRate(sampleRate)
                    .SetEncoding(Encoding.PcmFloat)
                    .SetChannelMask(channelMask)
                    .Build()!)
                .SetBufferSizeInBytes(bufSize)
                .SetTransferMode(AudioTrackMode.Stream)
                .Build()!;
        }

        public void Start(Func<float[], int, bool> fillBufferCallback)
        {
            _callback = fillBufferCallback;
            _running  = true;
            _track!.Play();

            _fillThread = new System.Threading.Thread(() =>
            {
                var buf = new float[_bufferFrames * _channels];
                while (_running)
                {
                    bool cont = _callback(buf, _bufferFrames);
                    _track.Write(buf, 0, buf.Length, WriteMode.Blocking);
                    if (!cont) { _running = false; break; }
                }
            })
            { IsBackground = true, Name = "AudioFillThread" };

            _fillThread.Start();
        }

        public void Stop()
        {
            _running = false;
            _fillThread?.Join(500);
            _track?.Stop();
        }

        public void Dispose()
        {
            Stop();
            _track?.Release();
            _track?.Dispose();
        }
    }
}
#endif
