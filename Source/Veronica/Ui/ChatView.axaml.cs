using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Veronica.Ui;

// The dock rebuilds this view every time its tab is selected, so it subscribes only while it is shown.
public partial class ChatView : UserControl
{
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

        ScrollToEnd();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel != null)
            _viewModel.Messages.CollectionChanged -= OnMessagesChanged;

        base.OnDetachedFromVisualTree(e);
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToEnd();

    private void ScrollToEnd() =>
        Dispatcher.UIThread.Post(MessagesScroll.ScrollToEnd, DispatcherPriority.Background);
}
