using Keen.VRage.ContentPipeline.Definitions.AssetInterfaces;
using Keen.VRage.ContentPipeline.Game.Prefabs;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.ContentPipeline.Project.Utils;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Serialization;
using Veronica.Editing;

namespace Veronica.Tools;

// A prefab binding pairs a block's client and server prefabs and shows no fields; the block's values live on the
// definitions its prefabs and compositions point at.
internal static class Prefabs
{
    public static bool IsBinding(ExplorerAssetFile asset) => asset.IsPrefabBindingDefinition();

    public static void RequireNotBinding(ExplorerAssetFile asset)
    {
        if (IsBinding(asset))
            throw new EditError($"{Content.Shown(asset)} is a prefab binding and has no fields; {Definitions(asset)}");
    }

    public static string Definitions(ExplorerAssetFile binding)
    {
        var items = Members(binding);
        return items.Count == 0
            ? "its prefabs list no definitions."
            : "the block's fields are on its definitions, read and write them there:\n" + string.Join("\n", items.Select(Content.Describe));
    }

    // Only the binding's own project: a component shared with other blocks is not part of this one.
    private static List<ExplorerAssetFile> Members(ExplorerAssetFile binding)
    {
        var project = binding.Project;
        var found = new List<ExplorerAssetFile>();
        var seen = new HashSet<Guid> { binding.AssetID };

        void Add(ExplorerAssetFile? asset)
        {
            if (asset != null && asset.Project == project && asset.AssetInterface is IDefinitionAssetInterface && seen.Add(asset.AssetID))
                found.Add(asset);
        }

        foreach (var asset in binding.GetPrefabAndCompositeAssets())
        {
            Add(asset);
            if (asset.AssetInterface is not IDefinitionAssetInterface definition || definition.GetDefinitionManipulator() is not { } manipulator)
                continue;
            foreach (var member in manipulator.GetMembersOfType<Definition>(depth: 0))
            {
                if (member.GetValue<Definition>() is not null && member[nameof(Definition.Guid)].GetValue<Guid>() is { } guid && guid != Guid.Empty)
                    Add(project.GetAssetFileByGuidInProject(guid));
            }
        }
        return found.OrderBy(a => a.ProjectRelativePath, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
