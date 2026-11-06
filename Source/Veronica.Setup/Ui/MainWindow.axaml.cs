using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Veronica.Setup.Ui;

public sealed partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);

    public void Bring()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }
}
