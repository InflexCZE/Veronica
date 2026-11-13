using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Veronica.Setup.Ui;

namespace Veronica.Setup;

public sealed class App : Application
{
    public static bool StartUpdate { get; set; }

    public static bool SelfUpdated { get; set; }

    public static bool StartInstall { get; set; }

    public static SingleInstance? Instance { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var main = new MainViewModel(SetupHost.ForThisProcess(), () => desktop.Shutdown());
            var window = new MainWindow { DataContext = main };
            main.PickFolder = window.PickFolderAsync;
            main.PickEditor = window.PickEditorAsync;
            main.PickGame = window.PickGameAsync;
            main.CopyText = text => window.Clipboard?.SetTextAsync(text) ?? Task.CompletedTask;
            desktop.MainWindow = window;
            Instance?.Listen(
                () => Dispatcher.UIThread.Post(() => window.Bring()),
                () => Dispatcher.UIThread.Post(() =>
                {
                    window.Bring();
                    main.RequestUpdate();
                }));
            main.Start(StartUpdate, SelfUpdated, StartInstall);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
