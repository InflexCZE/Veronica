using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Veronica.Setup.Ui;

// A tab's pages are rebuilt every time the tab opens; the page it opens on must not slide in.
public sealed class SlideCarousel : Carousel
{
    private IPageTransition? _transition;

    protected override Type StyleKeyOverride => typeof(Carousel);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _transition ??= PageTransition;
        PageTransition = null;
        Dispatcher.UIThread.Post(() => PageTransition = _transition, DispatcherPriority.Loaded);
    }
}
