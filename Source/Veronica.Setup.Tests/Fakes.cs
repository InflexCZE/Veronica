using System.Net;
using System.Net.Http.Headers;
using Veronica.Setup.Core;

namespace Veronica.Setup.Tests;

// An install folder in memory: paths are case-insensitive, folders exist while they hold anything.
public sealed class MemoryFiles : IFiles
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, byte[]> All => _files;

    // A move to a matching destination throws, the way a locked file or folder does.
    public Func<string, bool>? FailMoveTo { get; set; }

    private void CheckMove(string destination)
    {
        if (FailMoveTo?.Invoke(destination) == true)
            throw new IOException($"{destination} is in use.");
    }

    private static string Norm(string path) => Path.GetFullPath(path).TrimEnd('\\');

    public bool FileExists(string path) => _files.ContainsKey(Norm(path));

    public bool DirectoryExists(string path)
    {
        string dir = Norm(path);
        return _directories.Contains(dir) || _files.Keys.Any(f => f.StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase));
    }

    public long Length(string path) => _files[Norm(path)].Length;

    public byte[] ReadAllBytes(string path) => _files.TryGetValue(Norm(path), out var bytes) ? bytes.ToArray() : throw new FileNotFoundException(path);

    public void WriteAllBytes(string path, byte[] bytes) => _files[Norm(path)] = bytes.ToArray();

    public Stream OpenRead(string path) => new MemoryStream(ReadAllBytes(path), writable: false);

    public Stream OpenAppend(string path)
    {
        string key = Norm(path);
        var existing = _files.TryGetValue(key, out var bytes) ? bytes : [];
        return new AppendStream(existing, written => _files[key] = written).Init();
    }

    public void Move(string source, string destination, bool overwrite)
    {
        string from = Norm(source), to = Norm(destination);
        CheckMove(to);
        if (!_files.TryGetValue(from, out var bytes))
            throw new FileNotFoundException(source);
        if (_files.ContainsKey(to) && !overwrite)
            throw new IOException($"{destination} exists.");
        _files.Remove(from);
        _files[to] = bytes;
    }

    public void MoveDirectory(string source, string destination)
    {
        string from = Norm(source), to = Norm(destination);
        CheckMove(to);
        if (DirectoryExists(to))
            throw new IOException($"{destination} exists.");
        foreach (var key in _files.Keys.Where(k => k.StartsWith(from + "\\", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _files[to + key[from.Length..]] = _files[key];
            _files.Remove(key);
        }
        _directories.RemoveWhere(d => d.Equals(from, StringComparison.OrdinalIgnoreCase) || d.StartsWith(from + "\\", StringComparison.OrdinalIgnoreCase));
    }

    public void Delete(string path) => _files.Remove(Norm(path));

    public void DeleteDirectory(string path)
    {
        string dir = Norm(path);
        foreach (var key in _files.Keys.Where(k => k.StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase)).ToList())
            _files.Remove(key);
        _directories.RemoveWhere(d => d.Equals(dir, StringComparison.OrdinalIgnoreCase) || d.StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase));
    }

    public void CreateDirectory(string path) => _directories.Add(Norm(path));

    public IEnumerable<string> Files(string directory, bool recursive)
    {
        string dir = Norm(directory) + "\\";
        return _files.Keys.Where(k => k.StartsWith(dir, StringComparison.OrdinalIgnoreCase) && (recursive || !k[dir.Length..].Contains('\\'))).Order().ToList();
    }

    public IEnumerable<string> Entries(string directory)
    {
        string dir = Norm(directory) + "\\";
        return _files.Keys.Concat(_directories).Where(k => k.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
            .Select(k => k[dir.Length..].Split('\\')[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private sealed class AppendStream(byte[] existing, Action<byte[]> commit) : MemoryStream
    {
        private bool _committed;

        public AppendStream Init()
        {
            Write(existing);
            return this;
        }

        // Every write lands at once, so a download cut mid-stream leaves its partial file behind.
        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            commit(ToArray());
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            base.Write(buffer);
            commit(ToArray());
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_committed)
            {
                _committed = true;
                commit(ToArray());
            }
            base.Dispose(disposing);
        }
    }
}

// Serves URLs from memory. A route can honour Range requests and cut its first answers short.
public sealed class FakeHttp : HttpMessageHandler
{
    private readonly Dictionary<string, Route> _routes = new(StringComparer.Ordinal);

    public List<HttpRequestMessage> Requests { get; } = new();

    public sealed class Route(byte[] body)
    {
        public byte[] Body { get; } = body;

        public bool HonoursRange { get; init; } = true;

        // How many answers end early, and after how many bytes.
        public int CutsLeft { get; set; }

        public int CutAfter { get; init; }

        public int DropsLeft { get; set; }

        public int FailuresLeft { get; set; }

        public HttpStatusCode FailWith { get; set; } = HttpStatusCode.TooManyRequests;

        public TimeSpan? RetryAfter { get; set; }
    }

    public Route Serve(string url, byte[] body, bool honoursRange = true, int cuts = 0, int cutAfter = 0)
    {
        var route = new Route(body) { HonoursRange = honoursRange, CutsLeft = cuts, CutAfter = cutAfter };
        _routes[url] = route;
        return route;
    }

    public Route Serve(string url, string body) => Serve(url, System.Text.Encoding.UTF8.GetBytes(body));

    public HttpClient Client() => new(this);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (!_routes.TryGetValue(request.RequestUri!.ToString(), out var route))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        if (route.DropsLeft > 0)
        {
            route.DropsLeft--;
            throw new HttpRequestException("The connection was refused.");
        }
        if (route.FailuresLeft > 0)
        {
            route.FailuresLeft--;
            var failure = new HttpResponseMessage(route.FailWith) { RequestMessage = request };
            if (route.RetryAfter is { } after)
                failure.Headers.RetryAfter = new RetryConditionHeaderValue(after);
            return Task.FromResult(failure);
        }

        long from = 0;
        var status = HttpStatusCode.OK;
        if (route.HonoursRange && request.Headers.Range?.Ranges.FirstOrDefault() is { From: { } start })
        {
            from = start;
            status = HttpStatusCode.PartialContent;
        }
        var body = route.Body.AsSpan((int)from).ToArray();
        Stream stream = new MemoryStream(body);
        if (route.CutsLeft > 0)
        {
            route.CutsLeft--;
            stream = new CutStream(body, route.CutAfter);
        }
        var response = new HttpResponseMessage(status) { Content = new StreamContent(stream), RequestMessage = request };
        response.Content.Headers.ContentLength = body.Length;
        if (status == HttpStatusCode.PartialContent)
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, route.Body.Length - 1, route.Body.Length);
        return Task.FromResult(response);
    }

    // Delivers some bytes, then fails the way a dropped connection does.
    private sealed class CutStream(byte[] body, int cutAfter) : Stream
    {
        private int _position;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= cutAfter)
                throw new IOException("The connection was cut.");
            int n = Math.Min(count, Math.Min(cutAfter, body.Length) - _position);
            Array.Copy(body, _position, buffer, offset, n);
            _position += n;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => body.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
