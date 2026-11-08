using System.Reflection;
using System.Text.Json;
using Keen.VRage.ContentPipeline.Common;
using Keen.VRage.ContentPipeline.Definitions;
using Keen.VRage.ContentPipeline.Definitions.Utility;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.Core.Scripting;
using Keen.VRage.Editor.Windows.ProjectExplorer.ViewModels;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Extensions;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Utils;
using Keen.VRage.UI.Shared.Helpers;
using Keen.VRage.UI.Shared.ObjectInspector;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class CreateDefinitionTool : ITool
{
    public string Name => "create_definition";

    public string Description =>
        "Creates a new definition of a type, with the type's default values, in a mod folder. To use it from a reference field, write the field to its path.";

    public string Parameters => """
        {"type":"object","properties":{"type":{"type":"string","description":"Definition type, e.g. BlockKindDefinition"},"name":{"type":"string","description":"File name without extension"},"folder":{"type":"string","description":"Folder inside the mod, e.g. Assets/MyMod"}},"required":["type","name","folder"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string type = Args.Text(arguments, "type");
        string name = FileStem.Of(Args.Text(arguments, "name"), FileExtensions.DEFINITION_DOT) ?? throw new EditError("name is missing.");
        string folderPath = Args.Text(arguments, "folder");
        return new ToolResult(await EditorProject.RunAsync(project => Create(project, type, name, folderPath), cancellationToken))
        {
            Summary = $"Created definition {Gist.Clip(name, 40)} in {Gist.Clip(folderPath, 40)}",
        };
    }

    public string Attempt(JsonElement arguments) => $"create definition {Gist.Clip(Args.Text(arguments, "name"), 40)}";

    private static string Create(LoadedProject project, string typeQuery, string name, string folderPath)
    {
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new EditError($"'{name}' cannot be part of a file name.");
        var type = DefinitionTypes.Pick(CreatableTypes(), typeQuery);
        var (folder, _) = Folders.Ensure(project, folderPath, assetsOnly: true);
        string fileName = CreateFile(type, folder, name);
        return $"Created {(folder.GetItemByName(fileName) is { } item ? Content.Describe(item) : fileName)}";
    }

    public static string CreateFile(string typeName, ExplorerFolder folder, string name) =>
        CreateFile(DefinitionTypes.Pick(CreatableTypes(), typeName), folder, name);

    private static string CreateFile(Type type, ExplorerFolder folder, string name)
    {
        Content.RequireWritable(folder);
        string fileName = name + FileExtensions.DEFINITION_DOT;
        if (FileStem.Taken(folder, name) is { } taken)
            throw new EditError($"{taken.ProjectRelativePath} already exists; pick another name or folder.");

        var builder = DefaultBuilder(type);
        builder.Guid = Guid.NewGuid();
        var handle = folder.CreateOrOverwriteFile(fileName, (stream, debugName) =>
            SerializationHelper.SerializeAbstract(new SerializationContext(stream, debugName, new ProjectDefinitionSerializationContext(folder.Project)), builder),
            builder.Guid);
        if (!handle.IsValid())
            throw new EditError($"The Editor could not write {fileName} (see its log).");
        return fileName;
    }

    // As the Editor's Create Definition does: some types fill in useful defaults themselves.
    private static DefinitionObjectBuilder DefaultBuilder(Type type)
    {
        if (type.IsAssignableTo(typeof(IDefaultDefinitionCreator)))
        {
            var create = type.GetInterfaceMap(typeof(IDefaultDefinitionCreator)).TargetMethods[0];
            if (create.Invoke(null, null) is DefinitionObjectBuilder custom)
                return custom;
        }
        return Activator.CreateInstance(DefinitionAccessHelper.GetObjectBuilderType(type)) as DefinitionObjectBuilder
            ?? throw new EditError($"The Editor cannot create a {type.Name}.");
    }

    // The types of the Editor's Create Definition menu: concrete definitions the game can load.
    private static IReadOnlyCollection<Type> CreatableTypes() =>
        TypeOptionManager.FindOptions(typeof(Definition),
            static (_, candidate) => !candidate.IsAbstract && !candidate.ContainsGenericParameters,
            static (_, candidate) => GameVisible(candidate),
            "Veronica_DefinitionCreation");

    internal static bool GameVisible(Type type)
    {
        if (type.Assembly.GetCustomAttribute<ScriptingAssemblyAttribute>() != null)
            return true;
        if (PluginService.Value is not { } plugin)
            return true;
        object[] name = [type.Assembly.GetName().Name ?? ""];
        return !(bool)plugin.IsEditor.Invoke(plugin.Service, name)! && !(bool)plugin.IsPlugin.Invoke(plugin.Service, name)!;
    }

    // The menu's filter needs the Editor's internal plugin service; without it nothing is filtered out.
    private static readonly Lazy<(object Service, MethodInfo IsEditor, MethodInfo IsPlugin)?> PluginService = new(() =>
    {
        var serviceType = typeof(ProjectExplorerViewModel).Assembly.GetType("Keen.VRage.Editor.Services.Project.IPluginService");
        var isEditor = serviceType?.GetMethod("IsEditorAssembly", [typeof(string)]);
        var isPlugin = serviceType?.GetMethod("IsPluginAssembly", [typeof(string)]);
        if (serviceType == null || isEditor == null || isPlugin == null || VRageAvaloniaLocator.Current.GetService(serviceType) is not { } service)
            return null;
        return (service, isEditor, isPlugin);
    });
}
