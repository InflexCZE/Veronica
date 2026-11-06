using System.Security.Cryptography;
using System.Text;

namespace Veronica.Setup;

// One Veronica.exe per install folder: two would race on the same files. A second start brings the first
// to the front, and passes on an update request from the panel's button.
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private readonly EventWaitHandle _update;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex, string root)
    {
        _mutex = mutex;
        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, Name(root, "activate"));
        _update = new EventWaitHandle(false, EventResetMode.AutoReset, Name(root, "update"));
    }

    private static string Name(string root, string what) =>
        $"Local\\Veronica.Setup.{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root).TrimEnd('\\').ToUpperInvariant())))[..16]}.{what}";

    public static SingleInstance? TryAcquire(string root, TimeSpan wait)
    {
        var mutex = new Mutex(false, Name(root, "lock"));
        try
        {
            if (mutex.WaitOne(wait))
                return new SingleInstance(mutex, root);
        }
        catch (AbandonedMutexException)
        {
            return new SingleInstance(mutex, root);
        }
        mutex.Dispose();
        return null;
    }

    public static void Notify(string root, bool update)
    {
        try
        {
            using var handle = EventWaitHandle.OpenExisting(Name(root, update ? "update" : "activate"));
            handle.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }

    public void Listen(Action activate, Action update) => new Thread(() =>
    {
        var handles = new WaitHandle[] { _activate, _update, _stop.Token.WaitHandle };
        while (true)
        {
            int signalled = WaitHandle.WaitAny(handles);
            if (signalled == 2)
                return;
            (signalled == 0 ? activate : update)();
        }
    }) { IsBackground = true, Name = "Veronica single instance" }.Start();

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }
        _mutex.Dispose();
        _activate.Dispose();
        _update.Dispose();
    }
}
