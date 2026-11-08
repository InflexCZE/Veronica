using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Veronica.Agent.Chat;

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
        // Tunnelling: the TextBox would otherwise take Enter as a new line.
        Input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_viewModel != null)
        {
            _viewModel.Messages.CollectionChanged += OnMessagesChanged;
            _viewModel.PropertyChanged += OnPanelChanged;
        }
        MessagesScroll.ScrollChanged += OnScrollChanged;

        ScrollToEnd();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Messages.CollectionChanged -= OnMessagesChanged;
            _viewModel.PropertyChanged -= OnPanelChanged;
        }
        MessagesScroll.ScrollChanged -= OnScrollChanged;

        base.OnDetachedFromVisualTree(e);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || (e.KeyModifiers & KeyModifiers.Shift) != 0)
            return;
        e.Handled = true;
        _viewModel?.SubmitCommand.Execute(null);
    }

    private void OnThinkingTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ThinkingPart thinking)
            thinking.Toggle();
    }

    private void OnToolTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ToolPart tool)
            tool.Toggle();
    }

    private void OnSubAgentTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is SubAgentPart card)
            card.Toggle();
    }

    private void OnSuggestionClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is string text)
            _viewModel?.SuggestCommand.Execute(text);
    }

    private void OnUpdateTapped(object? sender, TappedEventArgs e) => _viewModel?.UpdateCommand?.Execute(null);

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToEnd();

    private void OnPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatPanelViewModel.Card))
            ScrollToEnd();
    }

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
