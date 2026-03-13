using WhiteNoise.ViewModels;

namespace WhiteNoise.Views;

public partial class MainPage : ContentPage
{
    public MainPage(AudioViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // Select first pattern by default
        if (vm.Patterns.Count > 0)
            vm.SelectedPattern = vm.Patterns[1]; // "Wave" is index 1
    }
}
