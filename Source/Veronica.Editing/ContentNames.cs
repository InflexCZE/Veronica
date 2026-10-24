namespace Veronica.Editing;

// How the model names content items and templates.
public static class ContentNames
{
    public static string Normalize(string path) => path.Trim().Replace('/', '\\').Trim('\\');

    public static string[] Segments(string folderPath) =>
        Normalize(folderPath).Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // A project-relative path, or a bare file name; the extension is optional either way.
    public static bool Matches(string projectRelativePath, string query)
    {
        string path = Normalize(projectRelativePath);
        string wanted = Normalize(query);
        if (wanted.Length == 0)
            return false;

        if (Same(path, wanted) || Same(WithoutExtension(path), wanted))
            return true;
        if (wanted.Contains('\\'))
            return false;

        string name = Path.GetFileName(path);
        return Same(name, wanted) || Same(Path.GetFileNameWithoutExtension(name), wanted);
    }

    private static string WithoutExtension(string path)
    {
        int dot = path.LastIndexOf('.');
        return dot > path.LastIndexOf('\\') ? path[..dot] : path;
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
