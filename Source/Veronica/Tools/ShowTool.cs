using System.Reflection;
using System.Text.Json;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.Editor.Core;
using Keen.VRage.Editor.Models.Project.Interactions;
using Keen.VRage.Editor.Windows.ProjectExplorer.ExplorerItems;
using Keen.VRage.Editor.Windows.ProjectExplorer.ViewModels;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

// What a click and double-click on the item do: select it in the Project Explorer and the Property Inspector,
// then open it in the Render View.
public sealed class ShowTool : ITool
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // These open their own editor or an external app instead of the Render View; the user opens them.
    private static readonly HashSet<string> OwnEditors = new(StringComparer.Ordinal)
    {
        "FmodInteractions", "BehaviorTreeInteractions", "AnimationControllerInteractions", "ParticleEffectInteractions",
    };

    public string Name => "show";

    public string Description => "Shows a content item to the user: selects it in the Project Explorer and Property Inspector, and opens it in the Render View when it can be previewed.";

    public string Parameters => """
        {"type":"object","properties":{"target":{"type":"string","description":"Project path of the item, or its file name if unique; for a block, any of its prefab files"}},"required":["target"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string target = Args.Text(arguments, "target");
        return new ToolResult(await EditorProject.RunAsync(project => Show(project, target), cancellationToken));
    }

    private static string Show(LoadedProject project, string target)
    {
        var asset = Content.Resolve(project, target);
        var explorer = MainViewModel.Instance.GetViewModel<ProjectExplorerViewModel>()
            ?? throw new EditError("The Editor's Project Explorer is not available.");

        if (Select == null)
            throw new EditError("This Editor version has no way to select items from a plugin.");
        if (Select.Invoke(explorer, [asset, true, true]) is not ProjectExplorerFileViewModel file)
            throw new EditError($"The Project Explorer could not find {asset.ProjectRelativePath}.");

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

    // "Go to Asset": internal in the Editor.
    private static readonly MethodInfo? Select =
        typeof(ProjectExplorerViewModel).GetMethod("TryFindAndSetSelectionToFile", AnyInstance, [typeof(ExplorerFile), typeof(bool), typeof(bool)]);

    // Double-click's own entry point, which also reopens the item after the Editor reloads it; DisplayOpenFile alone does not.
    private static readonly MethodInfo? Open =
        typeof(ProjectExplorerViewModel).GetMethod("DisplayOpenItem", AnyInstance, [typeof(ProjectExplorerItemViewModel)]);
}
