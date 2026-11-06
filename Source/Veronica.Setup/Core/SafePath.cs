namespace Veronica.Setup.Core;

// Names from a download (zip entries, Hugging Face listings, the manifest's model ids) must not reach
// outside the folder they are meant for.
public static class SafePath
{
    public static string Under(string root, string relative)
    {
        var segments = relative.Replace('\\', '/').Split('/');
        if (relative.Length == 0 || Path.IsPathRooted(relative) || relative.Contains(':')
            || segments.Any(s => s.Length == 0 || s == "." || s == ".." || s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new FormatException($"'{relative}' is not a plain relative path.");
        string full = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"'{relative}' leaves its folder.");
        return full;
    }

    public static string Segment(string name)
    {
        if (name.Contains('/') || name.Contains('\\'))
            throw new FormatException($"'{name}' is not a single folder name.");
        Under(@"C:\x", name);
        return name;
    }
}
