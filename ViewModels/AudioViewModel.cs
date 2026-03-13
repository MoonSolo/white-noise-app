using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WhiteNoise.Audio;
using WhiteNoise.Services;

namespace WhiteNoise.ViewModels
{
    public class AudioViewModel : INotifyPropertyChanged
    {
        private readonly AudioService _audio;

        public AudioViewModel(AudioService audio)
        {
            _audio = audio;
            PlayPauseCommand = new RelayCommand(OnPlayPause);
            StopCommand      = new RelayCommand(OnStop);
            _selectedPattern = Patterns[1]; // Wave default
        }

        // ── commands ──────────────────────────────────────────────────────────
        public ICommand PlayPauseCommand { get; }
        public ICommand StopCommand      { get; }

        // ── play state ────────────────────────────────────────────────────────

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            private set
            {
                if (_isPlaying == value) return;
                _isPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlayPauseLabel));
            }
        }

        public string PlayPauseLabel => IsPlaying ? "⏸  Pause" : "▶  Play";

        // ── volume ────────────────────────────────────────────────────────────

        private double _volume = 50.0;
        public double Volume
        {
            get => _volume;
            set { _volume = value; _audio.Volume = (float)(value / 100.0); OnPropertyChanged(); }
        }

        // ── pattern ───────────────────────────────────────────────────────────

        private NoisePattern _pattern = NoisePattern.Wave;
        public NoisePattern Pattern
        {
            get => _pattern;
            set
            {
                _pattern = value;
                ApplyPatternToEngine();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCustomMode));
            }
        }

        public bool IsCustomMode => Pattern == NoisePattern.Custom;

        public ObservableCollection<PatternItem> Patterns { get; } = new()
        {
            new PatternItem(NoisePattern.Constant,  "Constant",   "Flat, steady noise"),
            new PatternItem(NoisePattern.Wave,      "Ocean Wave", "Slow rising & falling"),
            new PatternItem(NoisePattern.Rain,      "Rain",       "Irregular light flutter"),
            new PatternItem(NoisePattern.Pulse,     "Pulse",      "Rhythmic waveform"),
            new PatternItem(NoisePattern.Breathing, "Breathing",  "Deep inhale / exhale"),
            new PatternItem(NoisePattern.Custom,    "Custom",     "Set your own frequencies"),
        };

        private PatternItem? _selectedPattern;
        public PatternItem? SelectedPattern
        {
            get => _selectedPattern;
            set
            {
                _selectedPattern = value;
                if (value != null) Pattern = value.Value;
                OnPropertyChanged();
            }
        }

        // ── custom: wave enabled toggle ───────────────────────────────────────

        private bool _customWaveEnabled = true;
        public bool CustomWaveEnabled
        {
            get => _customWaveEnabled;
            set
            {
                _customWaveEnabled = value;
                ApplyPatternToEngine();
                OnPropertyChanged();
                OnPropertyChanged(nameof(WaveSliderVisible));
            }
        }

        // Wave speed slider only visible when wave is enabled in custom mode
        public bool WaveSliderVisible => _customWaveEnabled;

        // Sends the right pattern to the engine:
        // Custom + wave on  → Custom (uses WaveFrequency LFO)
        // Custom + wave off → Constant (flat gain, no modulation)
        private void ApplyPatternToEngine()
        {
            if (_pattern == NoisePattern.Custom)
                _audio.Pattern = _customWaveEnabled ? NoisePattern.Custom : NoisePattern.Constant;
            else
                _audio.Pattern = _pattern;
        }

        // ── noise frequency (Custom) ──────────────────────────────────────────

        private double _noiseFrequency = 20000.0;
        public double NoiseFrequency
        {
            get => _noiseFrequency;
            set
            {
                _noiseFrequency = value;
                _audio.NoiseFrequency = (float)value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NoiseFrequencyLabel));
            }
        }

        public string NoiseFrequencyLabel
        {
            get
            {
                double f = _noiseFrequency;
                return f >= 1000 ? $"{f / 1000:F1} kHz" : $"{(int)f} Hz";
            }
        }

        // ── wave frequency (Custom) ───────────────────────────────────────────

        private double _waveFrequency = 9.0;
        public double WaveFrequency
        {
            get => _waveFrequency;
            set
            {
                _waveFrequency = value;
                _audio.WaveFrequency = (float)value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WaveFrequencyLabel));
            }
        }

        public string WaveFrequencyLabel => $"{(int)_waveFrequency} cycles/min";

        // ── crackle ───────────────────────────────────────────────────────────

        private bool _crackleEnabled;
        public bool CrackleEnabled
        {
            get => _crackleEnabled;
            set { _crackleEnabled = value; _audio.CrackleEnabled = value; OnPropertyChanged(); }
        }

        private double _crackleIntensity = 30.0;
        public double CrackleIntensity
        {
            get => _crackleIntensity;
            set { _crackleIntensity = value; _audio.CrackleIntensity = (float)(value / 100.0); OnPropertyChanged(); }
        }

        // ── duration ──────────────────────────────────────────────────────────

        private double _durationMinutes = 0;
        public double DurationMinutes
        {
            get => _durationMinutes;
            set
            {
                _durationMinutes = value;
                _audio.Duration = value > 0 ? TimeSpan.FromMinutes(value) : TimeSpan.Zero;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DurationLabel));
            }
        }

        public string DurationLabel => _durationMinutes <= 0
            ? "∞  No limit"
            : $"{(int)_durationMinutes} min";

        // ── fade out ──────────────────────────────────────────────────────────

        private bool _fadeOut = true;
        public bool FadeOut
        {
            get => _fadeOut;
            set { _fadeOut = value; _audio.FadeOut = value; OnPropertyChanged(); }
        }

        private double _fadeDurationSeconds = 30.0;
        public double FadeDurationSeconds
        {
            get => _fadeDurationSeconds;
            set
            {
                _fadeDurationSeconds = value;
                _audio.FadeDuration = TimeSpan.FromSeconds(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(FadeLabel));
            }
        }

        public string FadeLabel => $"{(int)_fadeDurationSeconds}s fade";

        // ── command handlers ──────────────────────────────────────────────────

        private void OnPlayPause()
        {
            try { _audio.Toggle(); IsPlaying = _audio.IsPlaying; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Play] {ex}"); }
        }

        private void OnStop()
        {
            try { _audio.Stop(); IsPlaying = false; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Stop] {ex}"); }
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public record PatternItem(NoisePattern Value, string Name, string Description);

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _execute();
#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
    }
}