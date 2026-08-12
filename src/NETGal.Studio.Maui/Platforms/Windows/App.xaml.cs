using Microsoft.Maui;
using Microsoft.UI.Xaml;

namespace NETGal.Studio.Maui.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

