# Native writes to WebQwen.exe's stdout

**Exists:** in `serve` mode, stdout carries only protocol lines. `Program.cs` writes them through its own writer and points managed `Console.Out` at stderr. Our native code writes nothing to stdout.

**Missing:** native code that writes to stdout (Dawn, V8, a driver) would put a line on the protocol stream, and the plugin would drop a healthy process as "unreadable event". The fix is to duplicate the stdout handle for the protocol writer, then point the process's stdout (`SetStdHandle` and the CRT's descriptor 1) at stderr before the engine starts.
