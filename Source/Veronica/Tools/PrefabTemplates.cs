using System.Collections.Immutable;
using Keen.VRage.ContentPipeline.Game.Prefabs;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.ContentPipeline.Project.Structure.AssetGenerators;
using Veronica.Editing;

namespace Veronica.Tools;

// The Editor's Create Prefab menu, read from the generator that builds it.
internal static class PrefabTemplates
{
    public static Dictionary<string, AssetGeneratorOption> List(LoadedProject project)
    {
        // Subclasses of the provider build other menus; the base one is Create Prefab.
        var provider = AssetGeneratorIndexer.Instance.GeneratorProviders
            .FirstOrDefault(p => p.GetType() == typeof(PrefabGeneratorProvider))
            ?? throw new EditError("The Editor's prefab generator is not available.");

        var nodes = provider.GetGenerators(new AssetGeneratorProviderArguments([], [], project, RequireExactAssetsMatch: false));
        var options = new Dictionary<string, AssetGeneratorOption>();
        foreach (var node in nodes)
        {
            // The menu's own top group is not part of a template's path.
            if (node is AssetGeneratorGroup group)
                foreach (var child in group.Nested)
                    Collect(child, "", options);
            else
                Collect(node, "", options);
        }
        return options;
    }

    private static void Collect(AssetGeneratorNode node, string parent, Dictionary<string, AssetGeneratorOption> options)
    {
        string path = parent.Length == 0 ? node.Name : $"{parent}/{node.Name}";
        switch (node)
        {
            case AssetGeneratorOption option:
                options.TryAdd(path, option);
                break;
            case AssetGeneratorGroup group:
                foreach (var child in group.Nested)
                    Collect(child, path, options);
                break;
        }
    }

    public static (string Path, AssetGeneratorOption Option) Resolve(LoadedProject project, string query)
    {
        var options = List(project);
        var matches = Templates.Resolve(options.Keys, query);
        return matches.Count switch
        {
            1 => (matches[0], options[matches[0]]),
            0 => throw new EditError($"No template matches '{query}'; list them with find_template."),
            _ => throw new EditError($"'{query}' matches several templates: {string.Join(", ", matches.Take(Templates.MaxListed))}."),
        };
    }
}

// Answers the generator's name dialog with the tool's name argument, or with the dialog's own suggestion when there is none.
internal sealed class NameAnswer(string? name) : IAssetGeneratorInteractivity
{
    // Some templates name their files themselves and never ask.
    public bool Asked { get; private set; }

    public Task<string?> QueryStringAsync(string title, string description, Func<string, string?>? validator = null, string? suggestedText = null, string? hintText = null)
    {
        Asked = true;
        string answer = name ?? suggestedText ?? throw new EditError("Give a name.");
        if (validator?.Invoke(answer) is { } problem)
            throw new EditError($"The name '{answer}' is not accepted: {problem}");
        return Task.FromResult<string?>(answer);
    }
}
