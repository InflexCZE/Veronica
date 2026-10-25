using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Media;
using Avalonia.VisualTree;
using Keen.VRage.Editor.Controls.RenderControl;
using Keen.VRage.Library.Diagnostics;

namespace Veronica.Ui;

// Tells the user why the viewports stopped updating while the local model runs. A viewport is a
// native child window that Avalonia cannot draw over; a popup is a top-level window, so it can,
// as the Editor's own flyouts do.
internal static class FrozenViewBadge
{
    private const string Text = "Veronica is working...";
    private const double Inset = 8;

    private static readonly List<Popup> Shown = [];

    // UI thread. Never throws: a missing badge must not fail the freeze.
    public static void Show()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;
            foreach (var window in desktop.Windows)
                foreach (var view in window.GetVisualDescendants().OfType<VRageNativeControl>())
                    if (view.IsEffectivelyVisible && view.Bounds.Width > 0 && view.Bounds.Height > 0)
                        Shown.Add(ShowOver(view));
        }
        catch (Exception e)
        {
            Log.Default.WriteLine($"[Veronica] Could not show the viewport badge: {e.Message}");
        }
    }

    // UI thread.
    public static void Hide()
    {
        foreach (var popup in Shown)
        {
            popup.IsOpen = false;
            ((ISetLogicalParent)popup).SetParent(null);
        }
        Shown.Clear();
    }

    private static Popup ShowOver(Control view)
    {
        var text = new TextBlock
        {
            Text = Text,
            FontSize = Resource("MediumFontSize", 14.0),
            Foreground = Resource<IBrush>("PrimaryTextColor", Brushes.White),
        };
        if (Resource<FontFamily?>("VeronicaUiFont", null) is { } font)
            text.FontFamily = font;

        var popup = new Popup
        {
            PlacementTarget = view,
            Placement = PlacementMode.AnchorAndGravity,
            PlacementAnchor = PopupAnchor.BottomRight,
            PlacementGravity = PopupGravity.TopLeft,
            HorizontalOffset = -Inset,
            VerticalOffset = -Inset,
            IsLightDismissEnabled = false,
            Child = new Border
            {
                Background = Resource<IBrush>("DarkGrayColor", Brushes.Black),
                BorderBrush = Resource<IBrush>("SemiLightGrayColor", Brushes.Gray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 5),
                IsHitTestVisible = false,
                Child = text,
            },
        };
        // Without a logical parent the popup's window finds no theme and stays empty and zero-sized.
        // The parent pointer alone is enough, as ContextMenu does: the viewport gains no child.
        ((ISetLogicalParent)popup).SetParent(view);
        popup.IsOpen = true;
        return popup;
    }

    private static T Resource<T>(string key, T fallback) =>
        Application.Current!.TryFindResource(key, out var value) && value is T found ? found : fallback;
}
