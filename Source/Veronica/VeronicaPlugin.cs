using System.Diagnostics.CodeAnalysis;
using Keen.VRage.Core.Game.Systems;
using Keen.VRage.DCS.Components;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Filesystem.ContentCache;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Serialization.ObjectBuilders;
using Keen.VRage.Plugin.Editor;
using Keen.VRage.Plugin.Editor.Preview;
using Veronica.Residency;
using Veronica.Ui;

namespace Veronica;

[ProjectPlugin]
public sealed class VeronicaPlugin : IProjectPlugin
{
    // The Editor creates a new plugin instance on every project load; the UI it adds outlives them.
    private static readonly EditorShell Shell = new();

    // Runs before Render12 creates its device and pools, which is when pool tracking must be on.
    static VeronicaPlugin()
    {
        EngineAllocations.TrackBorrows();
    }

    public void ObjectBuilderLoadingContextConfigurator(ref ObjectBuilderLoadingContext.Builder contextBuilder, Entity engineEntity)
    {
    }

    public bool TryConfigureSessionAndScene(Entity engine, SessionBuilder sessionBuilder, [NotNullWhen(false)] out SessionConfigurationErrors? configurationErrors)
    {
        configurationErrors = null;
        return true;
    }

    public bool TryGetPreviewForAsset(AssetDescriptor asset, [NotNullWhen(true)] out PreviewEntityDataBase? previewEntityData, out PreviewErrors? previewErrors, IAccessible? previewSettings = null)
    {
        previewEntityData = null;
        previewErrors = null;
        return false;
    }

    public void OnProjectLoaded()
    {
        Log.Default.WriteLine($"[Veronica] Plugin loaded from {typeof(VeronicaPlugin).Assembly.Location}");
        Shell.Attach();
    }
}
