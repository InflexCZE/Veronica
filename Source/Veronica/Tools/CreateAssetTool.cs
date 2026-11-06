using System.Text.Json;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.ContentPipeline.Project.Structure.AssetGenerators;
using Keen.VRage.ContentPipeline.Textures.Cubemaps.Editor;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class CreateAssetTool : ITool
{
    // The Editor's Create menu entries the tool offers, by the menu's own name; more join as they are tested.
    private static readonly (string Kind, Type Provider)[] Kinds =
    [
        (SkyboxGeneratorProvider.GENERATOR_NAME, typeof(SkyboxGeneratorProvider)),
    ];

    public string Name => "create_asset";

    public string Description =>
        "Creates an asset in a mod folder from its input files, as the Editor's Create menu does. " +
        "skybox: a skybox cubemap from six face images named <name>_px, _nx, _py, _ny, _pz, _nz (each suffix picks its face), or an empty one without inputs.";

    public string Parameters => """
        {"type":"object","properties":{"kind":{"type":"string","enum":["skybox"]},"folder":{"type":"string","description":"Folder inside the mod, e.g. Assets/MySkybox"},"name":{"type":"string","description":"File name without extension; default the inputs' shared name"},"inputs":{"type":"array","items":{"type":"string"},"description":"Content items to make it from, by path or file name"}},"required":["kind","folder"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string kind = Args.Text(arguments, "kind");
        string folderPath = Args.Text(arguments, "folder");
        string? name = Args.OptionalText(arguments, "name");
        var inputs = arguments.TryGetProperty("inputs", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : throw new EditError("inputs holds item names as text.")).ToList()
            : [];
        var provider = Kinds.FirstOrDefault(k => string.Equals(k.Kind, kind, StringComparison.OrdinalIgnoreCase)).Provider
            ?? throw new EditError($"kind is '{kind}'; use {string.Join(" or ", Kinds.Select(k => k.Kind.ToLowerInvariant()))}.");
        return new ToolResult(await EditorProject.RunAsync(project => CreateAsync(project, provider, kind.ToLowerInvariant(), folderPath, name, inputs), cancellationToken));
    }

    private static async Task<string> CreateAsync(LoadedProject project, Type providerType, string kind, string folderPath, string? name, List<string> inputs)
    {
        if (name != null && name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new EditError($"'{name}' cannot be part of a file name.");

        var assets = inputs.Select(i => Content.Resolve(project, i)).ToArray();
        var provider = AssetGeneratorIndexer.Instance.GeneratorProviders.FirstOrDefault(p => p.GetType() == providerType)
            ?? throw new EditError($"The Editor's {kind} generator is not available.");
        var option = provider.GetGenerators(new AssetGeneratorProviderArguments([.. assets], [], project, RequireExactAssetsMatch: false))
            .OfType<AssetGeneratorOption>().FirstOrDefault()
            ?? throw new EditError($"The Editor offers no {kind} option.");
        if (option.DisabledReason is { } disabled)
            throw new EditError($"No {kind} from these inputs: {disabled.Replace('\n', ' ').Replace("selected", "given")}");

        var (folder, _) = Folders.Ensure(project, folderPath, assetsOnly: true);
        string fileName = await CreatePartialTool.CreateFileAsync(option, folder, name, kind);

        var item = folder.GetItemByName(fileName);
        return $"Created {(item != null ? Content.Describe(item) : fileName)}";
    }
}
