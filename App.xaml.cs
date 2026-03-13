using WhiteNoise.Views;

namespace WhiteNoise;

public partial class App : Application
{
    public App(MainPage mainPage)
    {
        InitializeComponent();
        MainPage = new NavigationPage(mainPage)
        {
            BarBackgroundColor = Color.FromArgb("#0D0E12"),
            BarTextColor       = Color.FromArgb("#7EB8D4")
        };
    }
}
