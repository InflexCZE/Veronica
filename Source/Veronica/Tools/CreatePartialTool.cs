using System.Text.Json;
using Keen.VRage.ContentPipeline.Common;
using Keen.VRage.ContentPipeline.Game.Editor;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.ContentPipeline.Project.Structure.AssetGenerators;
using Keen.VRage.Library.Extensions;
using Keen.VRage.Library.Utils;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class CreatePartialTool : ITool
{
    private const string Override = "override";
    private const string Copy = "copy";

    public string Name => "create_partial";

    public string Description =>
        "Creates a partial definition of an existing definition in a mod folder. override changes the original wherever it is used " +
        "(only for read-only definitions such as Vanilla's, one per definition); copy makes a new definition that starts as the original. " +
        "Unchanged values follow the original.";

    public string Parameters => """
        {"type":"object","properties":{"source":{"type":"string","description":"The definition to override or copy, e.g. IonThruster100_ThrusterEffectsComponentDefinition"},"kind":{"type":"string","enum":["override","copy"]},"folder":{"type":"string","description":"Folder inside the mod, e.g. Assets/MyMod"},"name":{"type":"string","description":"File name without extension; default 'Override of <source>' or 'Copy of <source>'"}},"required":["source","kind","folder"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string source = Args.Text(arguments, "source");
        string kind = Args.Text(arguments, "kind").ToLowerInvariant();
        string folderPath = Args.Text(arguments, "folder");
        string? name = FileStem.Of(Args.OptionalText(arguments, "name"), FileExtensions.PARTIAL_DEFINITION_DOT);
        if (kind is not (Override or Copy))
            throw new EditError($"kind is '{kind}'; use override or copy.");
        return new ToolResult(await EditorProject.RunAsync(project => CreateAsync(project, source, kind, folderPath, name), cancellationToken));
    }

    private static async Task<string> CreateAsync(LoadedProject project, string source, string kind, string folderPath, string? name)
    {
        if (name != null && name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new EditError($"'{name}' cannot be part of a file name.");

        var asset = Content.Resolve(project, source);
        var option = Option(project, asset, kind);

        var (folder, _) = Folders.Ensure(project, folderPath, assetsOnly: true);
        Content.RequireWritable(folder);

        var output = await option.GetGeneratorOutputAsync(new AssetCreationArguments(folder, new NameAnswer(name)));
        if (output.FailureReason is { } reason)
            throw new EditError($"The partial definition could not be created: {reason}");
        if (output.FilesToGenerate is not [var file])
            throw new EditError("The Editor generated no partial definition.");

        // The generator checks only loaded assets; a file that failed to load would be overwritten.
        if (FileStem.Taken(folder, Path.GetFileNameWithoutExtension(file.FileName)) is { } taken)
            throw new EditError($"{taken.ProjectRelativePath} already exists; pick another name or folder.");

        var handle = folder.CreateOrOverwriteFile(file.FileName, file.FileWriteAction, file.Guid);
        if (!handle.IsValid())
            throw new EditError($"The Editor could not write {file.FileName} (see its log).");
        output.AfterFilesCreated?.Invoke();

        var item = folder.GetItemByName(file.FileName);
        return $"Created {(kind == Override ? "an override" : "a copy")} of {asset.ProjectRelativePath}: {(item != null ? Content.Describe(item) : file.FileName)}";
    }

    // The Editor's own menu option, so its rules (override only of read-only definitions, one override each,
    // no copies of configurations) come back as its own reasons.
    private static AssetGeneratorOption Option(LoadedProject project, ExplorerAssetFile asset, string kind)
    {
        var provider = AssetGeneratorIndexer.Instance.GeneratorProviders.OfType<PartialDefinitionGeneratorProvider>().FirstOrDefault()
            ?? throw new EditError("The Editor's partial definition generator is not available.");

        string optionName = kind == Override ? PartialDefinitionGeneratorProvider.OVERRIDE_DEFINITION_NAME : PartialDefinitionGeneratorProvider.COPY_DEFINITION_NAME;
        var option = provider.GetGenerators(new AssetGeneratorProviderArguments([asset], [], project, RequireExactAssetsMatch: false))
            .OfType<AssetGeneratorGroup>().SelectMany(g => g.Nested).OfType<AssetGeneratorOption>()
            .FirstOrDefault(o => o.Name == optionName)
            ?? throw new EditError($"The Editor offers no partial {kind} option.");

        if (option.DisabledReason is { } disabled)
            throw new EditError(disabled.Contains("Partial prefabs", StringComparison.Ordinal)
                ? $"{asset.ProjectRelativePath} is a prefab; create_partial cannot make partial prefabs."
                : $"No partial {kind} of {asset.ProjectRelativePath}: {disabled.Replace('\n', ' ')}");
        return option;
    }
}
