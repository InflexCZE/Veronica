using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;

namespace Veronica.Setup.Ui;

public sealed partial class MainWindow : Window
{
    private readonly NavigationView _nav;
    private readonly Control _toolbar;
    private AppShell? _shell;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _nav = this.FindControl<NavigationView>("NavView")!;
        _toolbar = this.FindControl<Control>("Toolbar")!;
        _nav.SelectionChanged += OnNavSelectionChanged;
        PointerPressed += OnWindowPointerPressed;
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel main)
                main.PropertyChanged += OnMainChanged;
        };
    }

    public void Bring()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    public async Task<string?> PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Install folder" });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickEditorAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Find VRage.Editor.exe",
            FileTypeFilter = [new FilePickerFileType("VRage.Editor.exe") { Patterns = ["VRage.Editor.exe"] }],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickGameAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Find SpaceEngineers2.exe",
            FileTypeFilter = [new FilePickerFileType("SpaceEngineers2.exe") { Patterns = ["SpaceEngineers2.exe"] }],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.App))
            return;
        if (_shell != null)
            _shell.PropertyChanged -= OnShellChanged;
        _shell = ((MainViewModel)sender!).App;
        if (_shell == null)
        {
            Select("setup");
            return;
        }
        _shell.PropertyChanged += OnShellChanged;
        SelectTab(_shell.Tab);
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppShell.Tab) && _shell != null)
            SelectTab(_shell.Tab);
    }

    private void SelectTab(AppTab tab) => Select(((int)tab).ToString());

    // First run shows a single Initial Setup tab in place of the app's tabs.
    private void Select(string tag)
    {
        var item = _nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => i.Tag is string t && t == tag);
        if (item != null && !ReferenceEquals(_nav.SelectedItem, item))
            _nav.SelectedItem = item;
        // The items get their template only once the bar is shown.
        Dispatcher.UIThread.Post(StyleItems, DispatcherPriority.Loaded);
    }

    private void OnNavSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is NavigationViewItem { Tag: string tag } && int.TryParse(tag, out int index) && _shell != null)
            _shell.Tab = (AppTab)index;
        StyleItems();
    }

    // Only the bar drags the window, not its tabs.
    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (e.Source is not Visual source || !_toolbar.IsVisualAncestorOf(source) || source.FindAncestorOfType<NavigationViewItem>() != null)
            return;
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            BeginMoveDrag(e);
    }

    // NavigationView's top mode has no brush for a selected item's text and icon; the selected tab takes the
    // accent and the indicator under it spans the whole tab.
    private void StyleItems()
    {
        var accent = (IBrush)this.FindResource("AppAccentBrush")!;
        var dimmed = (IBrush)this.FindResource("AppDimmedBrush")!;
        foreach (var item in _nav.MenuItems.OfType<NavigationViewItem>())
        {
            var brush = ReferenceEquals(item, _nav.SelectedItem) ? accent : dimmed;
            foreach (var descendant in item.GetVisualDescendants())
            {
                if (descendant is ContentPresenter presenter)
                    presenter.Foreground = brush;
                else if (descendant is TextBlock text)
                    text.Foreground = brush;
                else if (descendant is ContentControl control)
                    control.Foreground = brush;
                else if (descendant is Image { Source: DrawingImage { Drawing: GeometryDrawing { Pen: Pen pen } } })
                    pen.Brush = brush;

                if (descendant is Border { Name: "SelectionIndicator" } indicator)
                {
                    indicator.Background = accent;
                    indicator.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                    indicator.Width = double.NaN;
                    indicator.Margin = new Thickness(indicator.Margin.Left, 0, 0, 0);
                }
                if (descendant is Border { Name: "LayoutRoot" } root)
                    root.Padding = new Thickness(root.Padding.Left, root.Padding.Top, 0, root.Padding.Bottom);
                if (descendant is Panel { Name: "SelectionIndicatorGrid" } grid)
                {
                    grid.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                    grid.Width = double.NaN;
                    grid.Margin = new Thickness(grid.Margin.Left, 0, 0, 0);
                }
            }
        }
    }
}
