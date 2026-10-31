using Avalonia;
using Avalonia.Controls;
using System.Reflection;
using Avalonia.Input;
using Avalonia.Threading;
using Keen.VRage.Library.Diagnostics;
using Veronica.Tools;

namespace Veronica.Ui;

// The dock rebuilds this view every time its tab is selected, so it subscribes only while it is shown.
public partial class BoardView : UserControl
{
    private const int MeasureAttempts = 20;
    private const double FitMargin = 40;

    private readonly BoardPanelViewModel? _viewModel;
    private int _attempts;

    public BoardView()
    {
        InitializeComponent();
        DataContext = _viewModel = BoardPanelViewModel.Current;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_viewModel == null)
            return;
        DataContext = _viewModel;
        _viewModel.GraphChanged += OnGraphChanged;
        if (!_viewModel.Arranged)
            OnGraphChanged();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel != null)
            _viewModel.GraphChanged -= OnGraphChanged;
        // The tiles subscribe to the Content Browser's long-lived item view models; letting go of them
        // keeps a view the dock threw away from staying alive.
        DataContext = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnGraphChanged()
    {
        _attempts = 0;
        Dispatcher.UIThread.Post(TryArrange, DispatcherPriority.Background);
    }

    // The layout needs every node's measured size, which the containers report after their first layout pass.
    private void TryArrange()
    {
        if (_viewModel is not { Arranged: false } board || VisualRoot == null)
            return;
        bool measured = board.Nodes.All(n => n.Size.Width > 0 && n.Size.Height > 0) && board.Labels.All(l => l.Size.Width > 0);
        if (!measured && ++_attempts < MeasureAttempts)
        {
            Dispatcher.UIThread.Post(TryArrange, DispatcherPriority.Background);
            return;
        }
        if (!measured)
            Log.Default.WriteLine("[Veronica] Board nodes did not report their sizes; laying out with what they have.");

        var extent = board.Arrange();
        Dispatcher.UIThread.Post(() => Fit(extent), DispatcherPriority.Background);
    }

    // Shows the whole graph, never magnified past its real size.
    private void Fit(Rect extent)
    {
        var view = Editor.Bounds.Size;
        if (_viewModel == null || view.Width <= 0 || view.Height <= 0 || extent.Width <= 0)
            return;
        var area = extent.Inflate(FitMargin);
        double zoom = Math.Clamp(Math.Min(view.Width / area.Width, view.Height / area.Height), Editor.MinViewportZoom, 1);
        _viewModel.ViewportZoom = zoom;
        _viewModel.ViewportLocation = new Point(area.Center.X - view.Width / zoom / 2, area.Center.Y - view.Height / zoom / 2);
    }

    private void OnNodeTapped(object? sender, TappedEventArgs e) => Act(sender, node => Explorer.Select(node.Asset));

    private void OnNodeDoubleTapped(object? sender, TappedEventArgs e) => Act(sender, node => Explorer.Show(node.Asset));

    private static void Act(object? sender, Action<BoardNode> action)
    {
        if ((sender as Control)?.DataContext is not BoardNode node)
            return;
        try
        {
            action(node);
        }
        catch (Exception error)
        {
            Log.Default.WriteLine($"[Veronica] Board: {(error as TargetInvocationException)?.InnerException?.Message ?? error.Message}");
        }
    }
}
