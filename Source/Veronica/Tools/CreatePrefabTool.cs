using System.Text.Json;
using System.Text.RegularExpressions;
using Keen.VRage.ContentPipeline.Project.Features.AssetProcessing;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.ContentPipeline.Project.Structure.AssetGenerators;
using Keen.VRage.Library.Extensions;
using Keen.VRage.Library.Utils;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class CreatePrefabTool : ITool
{
    // Generators dodge an existing file by appending " (2)"; the tool refuses the collision instead.
    private static readonly Regex FreeVariant = new(@" \(\d+\)$");

    public string Name => "create_prefab";

    public string Description => "Builds a new prefab from a template into a mod folder, creating the folder if needed, and lists every generated file with its definition type.";

    public string Parameters => """
        {"type":"object","properties":{"template":{"type":"string","description":"Template path from find_template, or its last part, e.g. Ion Thrusters"},"name":{"type":"string","description":"Name of the new prefab; the generated files are named after it"},"folder":{"type":"string","description":"Folder inside the mod, e.g. Assets/MyThruster"}},"required":["template","name","folder"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string template = Args.Text(arguments, "template");
        string name = Args.Text(arguments, "name");
        string folderPath = Args.Text(arguments, "folder");
        var prefab = await EditorProject.RunAsync(project => CreateAsync(project, template, name, folderPath), cancellationToken);
        await TemplateRepairs.ApplyAsync(prefab, cancellationToken);
        return new ToolResult(await EditorProject.RunAsync(_ => Reply(prefab), cancellationToken))
        {
            Summary = $"Created prefab {Gist.Clip(name, 40)} from {Gist.Item(prefab.Template)} ({Gist.Count(prefab.Files.Count, "file", "files")})",
        };
    }

    public string Attempt(JsonElement arguments) => $"create prefab {Gist.Clip(Args.Text(arguments, "name"), 40)}";

    private static string Reply(GeneratedPrefab prefab)
    {
        var lines = new List<string> { $"Created from {prefab.Template}:" };
        lines.AddRange(prefab.Files.Select(n => prefab.Folder.GetItemByName(n) is { } item ? Content.Describe(item) : n));
        return string.Join("\n", lines);
    }

    private static async Task<GeneratedPrefab> CreateAsync(LoadedProject project, string template, string name, string folderPath)
    {
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new EditError($"'{name}' cannot be part of a file name.");

        var (templatePath, option) = PrefabTemplates.Resolve(project, template);
        var (folder, _) = Folders.Ensure(project, folderPath, assetsOnly: true);
        Content.RequireWritable(folder);

        var answer = new NameAnswer(name);
        var output = await option.GetGeneratorOutputAsync(new AssetCreationArguments(folder, answer));
        if (output.FailureReason is { } reason)
            throw new EditError($"The template could not generate: {reason}");
        if (output.FilesToGenerate.IsDefaultOrEmpty)
            throw new EditError("The template generated no files.");

        var files = output.FilesToGenerate;
        var names = FileNames(files.Select(f => f.FileName).ToList(), answer.Asked ? null : name);
        var taken = names.Select(n => FileStem.Taken(folder, Path.GetFileNameWithoutExtension(n))?.Name).OfType<string>().Distinct().ToList();
        if (taken.Count > 0)
            throw new EditError($"{string.Join(", ", taken)} already {(taken.Count == 1 ? "exists" : "exist")} in {Content.Shown(folder)} under the names this prefab's files would get; reuse that prefab, or pick another name or folder.");

        var written = new List<ResourceHandle>();
        for (int i = 0; i < files.Length; i++)
        {
            var handle = folder.CreateOrOverwriteFile(names[i], files[i].FileWriteAction, files[i].Guid);
            if (!handle.IsValid())
                throw new EditError($"The Editor could not write {names[i]} (see its log)"
                    + (written.Count == 0 ? "." : $"; already written: {string.Join(", ", names.Take(i))}."));
            written.Add(handle);
        }

        ReprocessFailed(folder.Project, written);
        output.AfterFilesCreated?.Invoke();
        return new GeneratedPrefab(templatePath, folder, name, names);
    }

    // Templates that never ask for a name get it anyway: their shared file name prefix becomes the name.
    private static List<string> FileNames(List<string> generated, string? rename)
    {
        var stems = generated.Select(n => FreeVariant.Replace(Path.GetFileNameWithoutExtension(n), "")).ToList();
        if (rename != null)
        {
            string prefix = stems.Aggregate((a, b) => a[..a.Zip(b).TakeWhile(p => p.First == p.Second).Count()]).TrimEnd('_', ' ', '-', '.');
            stems = stems.Select(s => rename + s[prefix.Length..]).ToList();
        }
        return stems.Select((s, i) => s + Path.GetExtension(generated[i])).ToList();
    }

    // As the Editor's own create does: an asset written before the files it needs failed processing, so retry it.
    private static void ReprocessFailed(LoadedProject project, List<ResourceHandle> written)
    {
        if (project.ProjectFeatures.OfType<AssetsProcessingFeature>().FirstOrDefault() is not { } feature
            || !feature.TryGetProcessingManager(out var manager))
            return;

        foreach (var handle in written)
        {
            if (manager.GetAssetController(handle.TryGetGuid() ?? Guid.Empty) is AssetProcessingController { ProcessingState: AssetProcessingState.Error } controller)
                controller.ForceReprocess();
        }
    }
}
