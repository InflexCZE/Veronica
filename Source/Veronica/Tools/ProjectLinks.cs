using Keen.VRage.ContentPipeline.Definitions.AssetInterfaces;
using Keen.VRage.ContentPipeline.Definitions.Utility;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.ContentPipeline.Project.Structure.References;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Serialization;
using Veronica.Editing;

namespace Veronica.Tools;

// References between content items, with the rules the inspector's reference picker applies.
internal sealed class ProjectLinks(LoadedProject project, ExplorerAssetFile owner) : IContentLinks
{
    public string? PathOf(Guid id) => project.GetItemByGuid(id)?.ProjectRelativePath;

    public ContentLink Find(string name, ReferenceSpec spec)
    {
        var named = Content.Pick(project.GetAvailableFiles().ToList(), name);
        if (named.Count == 0)
            throw new EditError(Content.NotFound(project, project.GetAvailableFiles(), name));

        var fitting = named.Where(f => Fits(f, spec)).ToList();
        return fitting.Count switch
        {
            1 => new ContentLink(fitting[0].AssetID, fitting[0].ProjectRelativePath),
            0 when named.Any(IsOverride) => throw new EditError(
                $"'{name}' is an override: it changes the original in place, so fields that point at the original already get its values. Point at the original, or at a copy for a new thing."),
            0 => throw new EditError($"'{name}' cannot go in this field, which takes {Expected(spec)}. Found: {string.Join(", ", named.Take(Content.MaxCandidates).Select(Content.Describe))}."),
            _ => throw new EditError(Content.Ambiguous(name, fitting)),
        };
    }

    public Definition CreateDefinitionReference(Guid id) =>
        project.GetItemByGuid(id) is ExplorerAssetFile { AssetInterface: IDefinitionAssetInterface definition }
            ? definition.CreateDefinitionPlaceHolder()
            : throw new EditError("The referenced item is not a definition.");

    private bool Fits(ExplorerFile candidate, ReferenceSpec spec)
    {
        if (candidate.AssetID == owner.AssetID)
            return false;

        bool wantsSource = !spec.Field.IsDefault && spec.Field.GetMemberAttribute<AssetReferenceTypeAttribute>() is { ReferenceType: ReferenceType.AssetSource };
        if (wantsSource ? candidate is not ExplorerAssetSourceFile : candidate is not ExplorerAssetFile)
            return false;

        if (spec.Extensions.Count > 0 && !spec.Extensions.Contains(Path.GetExtension(candidate.Name).ToLowerInvariant().TrimStart('.')))
            return false;

        if (spec.DefinitionTypes.Count == 0)
            return true;

        if (candidate is not ExplorerAssetFile { AssetInterface: IDefinitionAssetInterface definition })
            return false;
        if (definition is PartialDefinitionAssetInterface { PartialDefinitionKind: PartialDefinitionKind.None or PartialDefinitionKind.Override })
            return false;
        return definition.DefinitionType != typeof(object) && spec.DefinitionTypes.All(definition.DefinitionType.IsAssignableTo);
    }

    private static bool IsOverride(ExplorerFile file) =>
        file is ExplorerAssetFile { AssetInterface: PartialDefinitionAssetInterface { PartialDefinitionKind: PartialDefinitionKind.Override } };

    private static string Expected(ReferenceSpec spec)
    {
        if (spec.DefinitionTypes.Count > 0)
            return $"a {string.Join(" that is a ", spec.DefinitionTypes.Select(t => t.Name))}";
        if (spec.Extensions.Count > 0)
            return $"a .{string.Join(" or .", spec.Extensions)} file";
        return "a content item";
    }
}
