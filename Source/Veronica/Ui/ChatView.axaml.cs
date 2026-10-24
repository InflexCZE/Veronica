using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Veronica.Ui;

// The dock rebuilds this view every time its tab is selected, so it subscribes only while it is shown.
public partial class ChatView : UserControl
{
    private const double BottomSlack = 8;

    private readonly ChatPanelViewModel? _viewModel;

    public ChatView()
    {
        InitializeComponent();
        DataContext = _viewModel = ChatPanelViewModel.Current;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_viewModel != null)
            _viewModel.Messages.CollectionChanged += OnMessagesChanged;
        MessagesScroll.ScrollChanged += OnScrollChanged;

        ScrollToEnd();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel != null)
            _viewModel.Messages.CollectionChanged -= OnMessagesChanged;
        MessagesScroll.ScrollChanged -= OnScrollChanged;

        base.OnDetachedFromVisualTree(e);
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToEnd();

    // A streamed reply grows the content; follow it unless the user has scrolled up to read.
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentDelta.Y <= 0)
            return;
        var s = MessagesScroll;
        if (s.Offset.Y + s.Viewport.Height >= s.Extent.Height - e.ExtentDelta.Y - BottomSlack)
            ScrollToEnd();
    }

    private void ScrollToEnd() =>
        Dispatcher.UIThread.Post(MessagesScroll.ScrollToEnd, DispatcherPriority.Background);
}
