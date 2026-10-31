using System.Reflection;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.Editor.Core;
using Keen.VRage.Editor.Models.Project.Interactions;
using Keen.VRage.Editor.Windows.ProjectExplorer.ExplorerItems;
using Keen.VRage.Editor.Windows.ProjectExplorer.ViewModels;
using Veronica.Editing;

namespace Veronica.Tools;

// The Project Explorer's selection, opening and item view models. Their entry points are internal to the Editor.
internal static class Explorer
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // These open their own editor or an external app instead of the Render View; the user opens them.
    private static readonly HashSet<string> OwnEditors = new(StringComparer.Ordinal)
    {
        "FmodInteractions", "BehaviorTreeInteractions", "AnimationControllerInteractions", "ParticleEffectInteractions",
    };

    private static ProjectExplorerViewModel Instance =>
        MainViewModel.Instance.GetViewModel<ProjectExplorerViewModel>()
        ?? throw new EditError("The Editor's Project Explorer is not available.");

    // A click in the Content Browser: the item is selected there and in the Property Inspector.
    public static ProjectExplorerFileViewModel Select(ExplorerFile asset)
    {
        var explorer = Instance;
        if (SelectFile == null)
            throw new EditError("This Editor version has no way to select items from a plugin.");
        return SelectFile.Invoke(explorer, [asset, true, true]) as ProjectExplorerFileViewModel
            ?? throw new EditError($"The Project Explorer could not find {asset.ProjectRelativePath}.");
    }

    // The item's view model as the Content Browser's tile binds it, without touching the selection.
    public static ProjectExplorerFileViewModel? Tile(ExplorerFile asset) =>
        MainViewModel.Instance.GetViewModel<ProjectExplorerViewModel>() is { } explorer
            ? FileViewModel?.Invoke(explorer, [asset]) as ProjectExplorerFileViewModel
            : null;

    // A double-click: select, then open in the Render View when the item has a preview.
    public static string Show(ExplorerAssetFile asset)
    {
        var explorer = Instance;
        var file = Select(asset);

        string shown = Content.Describe(asset);
        if (Unwrapped(file.Interactions) is { } interactions && OwnEditors.Contains(interactions.GetType().Name))
            return $"Selected {shown}; it opens in its own editor, so the user double-clicks it to open it.";
        // A sub-asset, such as a prefab under its prefab binding, is previewed through the asset that owns it.
        var preview = !file.CanBeOpened && file.MainAsset is { CanBeOpened: true } main ? main : file;

        // The view model's cached flag is what opening checks; the interactions only explain a no.
        if (!preview.CanBeOpened)
        {
            string reason = "";
            file.Interactions?.CanBeOpened(out reason);
            return $"Selected {shown}; it has no Render View preview{(string.IsNullOrEmpty(reason) ? "" : $" ({reason})")}.";
        }

        if (Open != null)
            Open.Invoke(explorer, [preview]);
        else
            preview.DisplayOpenFile();
        return preview == file
            ? $"Selected {shown} and opened it in the Render View."
            : $"Selected {shown} and opened {Content.Shown(preview.FileModel)}, which it belongs to, in the Render View.";
    }

    // Partial definitions, proxies and stripped files forward to the interactions of what they stand for.
    private static FileInteractions? Unwrapped(FileInteractions? interactions)
    {
        for (int depth = 0; interactions != null && depth < 8; depth++)
        {
            var type = interactions.GetType();
            object? inner = type.GetField("DefinitionInteractions", AnyInstance)?.GetValue(interactions)
                ?? type.GetProperty("TargetInteractions", AnyInstance)?.GetValue(interactions)
                ?? type.GetField("_actualInteractions", AnyInstance)?.GetValue(interactions);
            if (inner is not FileInteractions next)
                return interactions;
            interactions = next;
        }
        return interactions;
    }

    // "Go to Asset".
    private static readonly MethodInfo? SelectFile =
        typeof(ProjectExplorerViewModel).GetMethod("TryFindAndSetSelectionToFile", AnyInstance, [typeof(ExplorerFile), typeof(bool), typeof(bool)]);

    // Finds or builds the item's view model; selection and the shown folder stay as they are.
    private static readonly MethodInfo? FileViewModel =
        typeof(ProjectExplorerViewModel).GetMethod("TryGetFileViewModel", AnyInstance, [typeof(ExplorerFile)]);

    // Double-click's own entry point, which also reopens the item after the Editor reloads it; DisplayOpenFile alone does not.
    private static readonly MethodInfo? Open =
        typeof(ProjectExplorerViewModel).GetMethod("DisplayOpenItem", AnyInstance, [typeof(ProjectExplorerItemViewModel)]);
}
