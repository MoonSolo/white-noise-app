using System;
using WhiteNoise.Audio;

namespace WhiteNoise.Services
{
    public class AudioService : IDisposable
    {
        private const int SampleRate   = 44100;
        private const int Channels     = 2;
        private const int BufferFrames = 1024;

        private readonly AudioEngine _engine = new AudioEngine();
        private IPlatformAudioOutput? _output;
        private bool _isPlaying = false;

        public float Volume
        {
            get => _engine.Volume;
            set => _engine.Volume = Math.Clamp(value, 0f, 1f);
        }

        public NoisePattern Pattern
        {
            get => _engine.Pattern;
            set => _engine.Pattern = value;
        }

        public bool CrackleEnabled
        {
            get => _engine.CrackleEnabled;
            set => _engine.CrackleEnabled = value;
        }

        public float CrackleIntensity
        {
            get => _engine.CrackleIntensity;
            set => _engine.CrackleIntensity = Math.Clamp(value, 0f, 1f);
        }

        public float NoiseFrequency
        {
            get => _engine.NoiseFrequency;
            set => _engine.NoiseFrequency = Math.Clamp(value, 80f, 20000f);
        }

        public float WaveFrequency
        {
            get => _engine.WaveFrequency;
            set => _engine.WaveFrequency = Math.Clamp(value, 1f, 60f);
        }

        public bool CustomWaveEnabled
        {
            get => _engine.CustomWaveEnabled;
            set => _engine.CustomWaveEnabled = value;
        }

        /// <summary>Duration in seconds. 0 = play forever.</summary>
        public float Duration
        {
            get => _engine.Duration;
            set => _engine.Duration = Math.Max(0f, value);
        }

        public bool FadeOut
        {
            get => _engine.FadeOut;
            set => _engine.FadeOut = value;
        }

        /// <summary>Fade duration in seconds.</summary>
        public float FadeDuration
        {
            get => _engine.FadeDuration;
            set => _engine.FadeDuration = Math.Max(0f, value);
        }

        public bool IsPlaying => _isPlaying;

        public void Play()
        {
            if (_isPlaying) return;
            _engine.Reset();
            _output = CreatePlatformOutput();
            _output.Initialize(SampleRate, Channels, BufferFrames);
            _output.Start(_engine.FillBuffer);
            _isPlaying = true;
        }

        public void Stop()
        {
            if (!_isPlaying) return;
            _output?.Stop();
            _output?.Dispose();
            _output = null;
            _isPlaying = false;
        }

        public void Toggle()
        {
            if (_isPlaying) Stop(); else Play();
        }

        public void Dispose()
        {
            Stop();
        }

        private static IPlatformAudioOutput CreatePlatformOutput()
        {
#if WINDOWS
            return new WhiteNoise.Platforms.Windows.WindowsAudioOutput();
#elif ANDROID
            return new WhiteNoise.Platforms.Android.AndroidAudioOutput();
#elif IOS || MACCATALYST
            return new WhiteNoise.Platforms.iOS.IosAudioOutput();
#else
            throw new PlatformNotSupportedException("No audio output for this platform.");
#endif
        }
    }
}