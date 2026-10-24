using Keen.VRage.ContentPipeline.Project.Structure;
using Veronica.Editing;

namespace Veronica.Tools;

// Folders of the loaded mod, created like mkdir -p.
internal static class Folders
{
    public static (ExplorerFolder Folder, bool Created) Ensure(LoadedProject project, string folderPath)
    {
        string[] segments = ContentNames.Segments(folderPath);
        if (segments.Length == 0)
            throw new EditError("Give a folder path inside the mod, e.g. Assets/MyThruster.");

        ExplorerFolder folder = project.GetRootFolder();
        bool created = false;
        foreach (string segment in segments)
        {
            switch (folder.GetItemByName(segment))
            {
                case ExplorerFolder existing:
                    folder = existing;
                    continue;
                case { } file:
                    throw new EditError($"'{file.ProjectRelativePath}' is a file, not a folder.");
            }

            Content.RequireWritable(folder);
            if (folder.CreateSubfolder(segment) == null || folder.GetItemByName(segment) is not ExplorerFolder made)
                throw new EditError($"The Editor could not create '{segment}' in '{Content.Shown(folder)}'.");
            folder = made;
            created = true;
        }
        return (folder, created);
    }
}
