using Keen.VRage.ContentPipeline.Project.Structure;

namespace Veronica.Tools;

// File names the model gives for new definitions: the extension is the tool's to add.
internal static class FileStem
{
    // Null when blank: models send empty optional arguments.
    public static string? Of(string? name, string extension)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        name = name.Trim();
        return name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? name[..^extension.Length].TrimEnd() : name;
    }

    // As the Editor's own name check, which compares stems: Foo.def blocks Foo.partialdef.
    public static ExplorerFile? Taken(ExplorerFolder folder, string stem) =>
        folder.ChildItems.OfType<ExplorerFile>().FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f.Name), stem, StringComparison.OrdinalIgnoreCase));
}
