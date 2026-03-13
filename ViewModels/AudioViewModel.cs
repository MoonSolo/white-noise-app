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
            PlayPauseCommand  = new RelayCommand(OnPlayPause);
            StopCommand       = new RelayCommand(OnStop);
        }

        // ── commands ──────────────────────────────────────────────────────────
        public ICommand PlayPauseCommand { get; }
        public ICommand StopCommand      { get; }

        // ── play state ────────────────────────────────────────────────────────

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            private set { _isPlaying = value; OnPropertyChanged(); OnPropertyChanged(nameof(PlayPauseLabel)); }
        }

        public string PlayPauseLabel => IsPlaying ? "Pause" : "Play";

        // ── volume (0..100 for slider) ────────────────────────────────────────

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
            set { _pattern = value; _audio.Pattern = value; OnPropertyChanged(); }
        }

        // All patterns for the picker
        public ObservableCollection<PatternItem> Patterns { get; } = new ObservableCollection<PatternItem>
        {
            new PatternItem(NoisePattern.Constant,  "Constant",  "Flat, steady noise"),
            new PatternItem(NoisePattern.Wave,      "Ocean Wave","Slow rising & falling"),
            new PatternItem(NoisePattern.Rain,      "Rain",      "Irregular light flutter"),
            new PatternItem(NoisePattern.Pulse,     "Pulse",     "Rhythmic waveform"),
            new PatternItem(NoisePattern.Breathing, "Breathing", "Deep inhale / exhale"),
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

        // 0 = infinite; stored as minutes on the slider
        private double _durationMinutes = 0;
        public double DurationMinutes
        {
            get => _durationMinutes;
            set
            {
                _durationMinutes = value;
                _audio.Duration  = value > 0
                    ? TimeSpan.FromMinutes(value)
                    : TimeSpan.Zero;
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
                _audio.FadeDuration  = TimeSpan.FromSeconds(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(FadeLabel));
            }
        }

        public string FadeLabel => $"{(int)_fadeDurationSeconds}s fade";

        // ── command handlers ──────────────────────────────────────────────────

        private void OnPlayPause()
        {
            _audio.Toggle();
            IsPlaying = _audio.IsPlaying;
        }

        private void OnStop()
        {
            _audio.Stop();
            IsPlaying = false;
        }

        // ── INotifyPropertyChanged ─────────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    public record PatternItem(NoisePattern Value, string Name, string Description);

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _execute();
        public event EventHandler? CanExecuteChanged;
    }
}
