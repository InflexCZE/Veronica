using System.Diagnostics;
using System.Globalization;
using System.Text;
using WebQwen;

const string Usage = """
    usage: WebQwen generate --prompt <text> [options]
           WebQwen serve [options]
      --model <id>          model id from the manifest (default Ternary-Bonsai-2-27B-MLC)
      --models <dir>        folder holding <model id>\ (default: .deps\models above this exe)
      --context <n>         context window size (default: the model's)
      --verbose             print the engine log
    generate only:
      --prompt <text>       user message
      --system <text>       system message
      --max-tokens <n>      generation limit
      --temperature <x>     sampling temperature (default: the model's)
      --top-p <x>           nucleus sampling (default: the model's)
      --seed <n>            sampling seed
      --no-thinking         answer without a think block
    serve: commands on stdin, events on stdout, one JSON object per line (Protocol.cs);
    the engine log goes to stderr. Exits when stdin closes.
    """;

if (args.Length == 0 || args[0] is not ("generate" or "serve")) return Fail(Usage);
bool serve = args[0] == "serve";

string modelId = "Ternary-Bonsai-2-27B-MLC";
string? modelsDir = null, prompt = null, system = null;
int maxTokens = 0, context = 0;
double? temperature = null, topP = null;
long? seed = null;
bool verbose = false, thinking = true;
for (int i = 1; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
    switch (args[i])
    {
        case "--model": modelId = Next(); break;
        case "--models": modelsDir = Next(); break;
        case "--context": context = int.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--verbose": verbose = true; break;
        case "--prompt" when !serve: prompt = Next(); break;
        case "--system" when !serve: system = Next(); break;
        case "--max-tokens" when !serve: maxTokens = int.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--temperature" when !serve: temperature = double.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--top-p" when !serve: topP = double.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--seed" when !serve: seed = long.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--no-thinking" when !serve: thinking = false; break;
        default: return Fail($"unknown option {args[i]}\n{Usage}");
    }
}
if (!serve && prompt == null) return Fail(Usage);

modelsDir ??= FindModelsDir();
if (modelsDir == null) return Fail("no --models folder given and no .deps\\models found above the exe");
var model = Manifest.Load(Path.Combine(AppContext.BaseDirectory, "manifest.json")).Model(modelId, context);
string modelDir = Path.Combine(modelsDir, modelId);
if (!Directory.Exists(modelDir)) return Fail($"{modelDir} does not exist; run Build\\FetchModel.ps1 -ModelId {modelId}");

Action<LogLevel, string> engineLog = (level, text) =>
{
    if (verbose || level >= LogLevel.Warn) Console.Error.WriteLine($"[{level}] {text}");
};

if (serve) return await Serve();

var wall = Stopwatch.StartNew();
using var engine = Engine.Create(new ModelFiles(model, modelDir), engineLog);
Console.Error.WriteLine($"engine ready in {wall.Elapsed.TotalSeconds:F1} s");

string lastProgress = "";
await engine.LoadModelAsync(model, new SyncProgress(p =>
{
    if (p.Text != lastProgress) Console.Error.WriteLine($"  {p.Text}");
    lastProgress = p.Text;
}));
Console.Error.WriteLine($"model loaded in {wall.Elapsed.TotalSeconds:F1} s");

var messages = new List<ChatMessage>();
if (system != null) messages.Add(new ChatMessage("system", system));
messages.Add(new ChatMessage("user", prompt!));
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancel.Cancel();
};
var stream = engine.Chat(new ChatRequest(messages, maxTokens, temperature, topP, seed, thinking), cancel.Token);
ChatChannel? channel = null;
await foreach (var delta in stream)
{
    if (delta.Channel != channel)
    {
        if (channel != null) Console.WriteLine();
        Console.ForegroundColor = delta.Channel == ChatChannel.Reasoning ? ConsoleColor.DarkGray : ConsoleColor.Gray;
        channel = delta.Channel;
    }
    Console.Write(delta.Text);
}
Console.ResetColor();
Console.WriteLine();
var result = await stream.Completion;
Console.Error.WriteLine(
    $"{result.PromptTokens} prompt tokens at {result.PrefillTokensPerSecond:F1} tok/s, " +
    $"{result.CompletionTokens} generated at {result.DecodeTokensPerSecond:F1} tok/s, finish: {result.FinishReason}");
return 0;

async Task<int> Serve()
{
    var utf8 = new UTF8Encoding(false);
    var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
    var input = new StreamReader(Console.OpenStandardInput(), utf8);
    // stdout carries only protocol lines; anything else printing to Console.Out lands in the log.
    Console.SetOut(Console.Error);

    Engine? served = null;
    var server = new Server(input, output, text => Console.Error.WriteLine(text));
    int code = await server.RunAsync(async progress =>
    {
        served = await Task.Run(() => Engine.Create(new ModelFiles(model, modelDir), engineLog));
        await served.LoadModelAsync(model, progress);
        return served;
    });
    if (served != null)
    {
        var dispose = Task.Run(served.Dispose);
        await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromSeconds(5)));
    }
    Environment.Exit(code);
    return code;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 2;
}

static string? FindModelsDir()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
    {
        string candidate = Path.Combine(dir.FullName, ".deps", "models");
        if (Directory.Exists(candidate)) return candidate;
    }
    return null;
}
