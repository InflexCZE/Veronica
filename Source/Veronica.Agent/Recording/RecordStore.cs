using System.Text;
using System.Text.Json;

namespace Veronica.Agent.Recording;

// Where a recording keeps its sessions: one named file of JSON lines per session.
public interface IRecordStore
{
    void Append(string fileName, byte[] line);

    void ChatCleared();
}

// Opened per line, so a killed Editor loses nothing and no handle outlives its session.
public sealed class FileRecordStore(string directory) : IRecordStore
{
    public void Append(string fileName, byte[] line)
    {
        Directory.CreateDirectory(directory);
        using var file = new FileStream(Path.Combine(directory, fileName), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        file.Write(line);
    }

    public void ChatCleared()
    {
    }
}

// The chat since its last clear, for /copy.
public sealed class MemoryRecordStore : IRecordStore
{
    private readonly object _gate = new();
    private readonly List<(string Name, List<byte[]> Lines)> _files = [];

    public void Append(string fileName, byte[] line)
    {
        lock (_gate)
        {
            int index = _files.FindIndex(f => f.Name == fileName);
            if (index < 0)
                _files.Add((fileName, [line]));
            else
                _files[index].Lines.Add(line);
        }
    }

    public void ChatCleared()
    {
        lock (_gate)
            _files.Clear();
    }

    // Each session's lines after a header line naming its file, so splitting at the headers gives the
    // files an eval run writes.
    public string Copy()
    {
        var text = new StringBuilder();
        lock (_gate)
        {
            foreach (var (name, lines) in _files)
            {
                text.Append("{\"type\":\"session\",\"file\":").Append(JsonSerializer.Serialize(name)).Append("}\n");
                foreach (var line in lines)
                    text.Append(Encoding.UTF8.GetString(line));
            }
        }
        return text.ToString();
    }
}
