using Microsoft.Maui.Controls;

namespace NETGal.Player.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new NavigationPage(new MainPage());
    }
}

