namespace Veronica.Setup.Core;

// Every disk access of Veronica.exe goes through here, so tests run against memory.
public interface IFiles
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    long Length(string path);

    byte[] ReadAllBytes(string path);

    // Creates missing parent folders.
    void WriteAllBytes(string path, byte[] bytes);

    Stream OpenRead(string path);

    // Creates the file and its parent folders when missing.
    Stream OpenAppend(string path);

    void Move(string source, string destination, bool overwrite);

    void MoveDirectory(string source, string destination);

    void Delete(string path);

    void DeleteDirectory(string path);

    void CreateDirectory(string path);

    IEnumerable<string> Files(string directory, bool recursive);

    // The names of the files and folders directly in a folder.
    IEnumerable<string> Entries(string directory);
}

public static class FilesExtensions
{
    public static string ReadAllText(this IFiles files, string path) => System.Text.Encoding.UTF8.GetString(files.ReadAllBytes(path));

    public static void WriteAllText(this IFiles files, string path, string text) => files.WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(text));
}

public sealed class DiskFiles : IFiles
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public long Length(string path) => new FileInfo(path).Length;

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    // A crash mid-write must not leave a half .vrgproj or settings file: write beside it, then replace.
    public void WriteAllBytes(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, overwrite: true);
    }

    public Stream OpenRead(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);

    public Stream OpenAppend(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None, 1 << 16);
    }

    public void Move(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(source, destination, overwrite);
    }

    public void MoveDirectory(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        Directory.Move(source, destination);
    }

    public void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    public void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public IEnumerable<string> Files(string directory, bool recursive) =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList()
            : [];

    public IEnumerable<string> Entries(string directory) =>
        Directory.Exists(directory) ? Directory.EnumerateFileSystemEntries(directory).Select(p => Path.GetFileName(p)).ToList() : [];
}
