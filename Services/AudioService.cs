using System;
using WhiteNoise.Audio;

namespace WhiteNoise.Services
{
    /// <summary>
    /// Application-level audio service. Injected via DI.
    /// Wraps AudioEngine and resolves the correct platform output.
    /// </summary>
    public class AudioService : IDisposable
    {
        private readonly AudioEngine _engine = new AudioEngine();
        private IPlatformAudioOutput? _output;
        private bool _isPlaying = false;

        // ── forwarded properties ─────────────────────────────────────────────
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

        public TimeSpan Duration
        {
            get => _engine.Duration;
            set => _engine.Duration = value;
        }

        public bool FadeOut
        {
            get => _engine.FadeOut;
            set => _engine.FadeOut = value;
        }

        public TimeSpan FadeDuration
        {
            get => _engine.FadeDuration;
            set => _engine.FadeDuration = value;
        }

        public bool IsPlaying => _isPlaying;

        // ── playback control ─────────────────────────────────────────────────

        public void Play()
        {
            if (_isPlaying) return;
            _output = CreatePlatformOutput();
            _engine.Start(_output);
            _isPlaying = true;
        }

        public void Stop()
        {
            if (!_isPlaying) return;
            _engine.Stop();
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
            _engine.Dispose();
        }

        // ── platform resolution ──────────────────────────────────────────────

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
