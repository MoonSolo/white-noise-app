using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WhiteNoise.Audio;
using WhiteNoise.Services;

namespace WhiteNoise.ViewModels
{
    public class PatternItem
    {
        public string       Name        { get; set; } = "";
        public string       Description { get; set; } = "";
        public NoisePattern Pattern     { get; set; }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        { _execute = execute; _canExecute = canExecute; }
        public bool CanExecute(object? p) => _canExecute?.Invoke() ?? true;
        public void Execute(object? p)    => _execute();
        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class AudioViewModel : INotifyPropertyChanged
    {
        private readonly AudioService _audio;

        public ObservableCollection<PatternItem> Patterns { get; } = new()
        {
            new PatternItem { Name = "Constant",  Description = "Steady flat noise",                    Pattern = NoisePattern.Constant  },
            new PatternItem { Name = "Wave",       Description = "Slow gentle rise and fall",            Pattern = NoisePattern.Wave      },
            new PatternItem { Name = "Rain",       Description = "Fast shimmering like rainfall",        Pattern = NoisePattern.Rain      },
            new PatternItem { Name = "Pulse",      Description = "Rhythmic soft thumps",                 Pattern = NoisePattern.Pulse     },
            new PatternItem { Name = "Breathing",  Description = "Slow inhale / exhale rhythm",          Pattern = NoisePattern.Breathing },
            new PatternItem { Name = "Ocean",      Description = "Random wave bursts, 250–450 Hz band",  Pattern = NoisePattern.Ocean     },
            new PatternItem { Name = "Custom",     Description = "Set your own frequency and wave speed",Pattern = NoisePattern.Custom    },
        };

        public AudioViewModel(AudioService audio)
        {
            _audio = audio;
            _selectedPattern = Patterns[0]; // default: Constant
            ApplyPatternToEngine();

            PlayPauseCommand = new RelayCommand(OnPlayPause);
            StopCommand      = new RelayCommand(OnStop);
        }

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand PlayPauseCommand { get; }
        public ICommand StopCommand      { get; }

        private void OnPlayPause() { _audio.Toggle(); Refresh(); }
        private void OnStop()      { _audio.Stop();   Refresh(); }

        private void Refresh()
        {
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(PlayPauseLabel));
            OnPropertyChanged(nameof(PlayButtonColor));
            OnPropertyChanged(nameof(PlayButtonTextColor));
        }

        public bool   IsPlaying        => _audio.IsPlaying;
        public string PlayPauseLabel   => _audio.IsPlaying ? "⏸  Pause" : "▶  Play";
        public string PlayButtonColor  => _audio.IsPlaying ? "#4A9F6E" : "#7EB8D4";
        public string PlayButtonTextColor => "#FFFFFF";

        // ── Pattern ────────────────────────────────────────────────────────
        private PatternItem _selectedPattern;
        public PatternItem SelectedPattern
        {
            get => _selectedPattern;
            set
            {
                if (_selectedPattern == value) return;
                _selectedPattern = value;
                ApplyPatternToEngine();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCustomMode));
            }
        }

        public bool IsCustomMode => _selectedPattern?.Pattern == NoisePattern.Custom;

        // ── Volume (0–100 in UI, 0–1 in engine) ────────────────────────────
        private float _volume = 70f;
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                _audio.Volume = value / 100f;
                OnPropertyChanged();
            }
        }

        // ── Crackle ────────────────────────────────────────────────────────
        private bool _crackleEnabled;
        public bool CrackleEnabled
        {
            get => _crackleEnabled;
            set { _crackleEnabled = value; _audio.CrackleEnabled = value; OnPropertyChanged(); }
        }

        // 0–100 in UI, 0–1 in engine
        private float _crackleIntensity = 50f;
        public float CrackleIntensity
        {
            get => _crackleIntensity;
            set
            {
                _crackleIntensity = value;
                _audio.CrackleIntensity = value / 100f;
                OnPropertyChanged();
            }
        }

        // ── Duration ───────────────────────────────────────────────────────
        private float _durationMinutes = 0f;
        public float DurationMinutes
        {
            get => _durationMinutes;
            set
            {
                _durationMinutes = value;
                _audio.Duration = value * 60f;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DurationLabel));
            }
        }

        public string DurationLabel => _durationMinutes <= 0
            ? "∞  Infinite"
            : $"{(int)_durationMinutes} min";

        // ── Fade Out ───────────────────────────────────────────────────────
        private bool _fadeOut;
        public bool FadeOut
        {
            get => _fadeOut;
            set { _fadeOut = value; _audio.FadeOut = value; OnPropertyChanged(); }
        }

        private float _fadeDurationSeconds = 10f;
        public float FadeDurationSeconds
        {
            get => _fadeDurationSeconds;
            set
            {
                _fadeDurationSeconds = value;
                _audio.FadeDuration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FadeLabel));
            }
        }

        public string FadeLabel => $"{(int)_fadeDurationSeconds} s";

        // ── Custom mode ────────────────────────────────────────────────────
        private float _noiseFrequency = 1000f;
        public float NoiseFrequency
        {
            get => _noiseFrequency;
            set
            {
                _noiseFrequency = value;
                _audio.NoiseFrequency = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NoiseFrequencyLabel));
            }
        }

        public string NoiseFrequencyLabel => _noiseFrequency >= 1000
            ? $"{_noiseFrequency / 1000f:F1} kHz"
            : $"{(int)_noiseFrequency} Hz";

        private float _waveFrequency = 10f;
        public float WaveFrequency
        {
            get => _waveFrequency;
            set
            {
                _waveFrequency = value;
                _audio.WaveFrequency = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WaveFrequencyLabel));
            }
        }

        public string WaveFrequencyLabel => $"{(int)_waveFrequency} /min";

        // ── Helpers ────────────────────────────────────────────────────────
        private void ApplyPatternToEngine()
        {
            if (_selectedPattern != null)
                _audio.Pattern = _selectedPattern.Pattern;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}