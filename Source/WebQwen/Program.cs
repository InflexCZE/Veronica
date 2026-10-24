using System.Diagnostics;
using System.Globalization;
using WebQwen;

const string Usage = """
    usage: WebQwen generate --prompt <text> [options]
      --model <id>          model id from the manifest (default Ternary-Bonsai-2-27B-MLC)
      --models <dir>        folder holding <model id>\ (default: .deps\models above this exe)
      --system <text>       system message
      --max-tokens <n>      generation limit
      --temperature <x>     sampling temperature (default: the model's)
      --top-p <x>           nucleus sampling (default: the model's)
      --seed <n>            sampling seed
      --context <n>         context window size (default: the model's)
      --verbose             print the engine log
    """;

if (args.Length == 0 || args[0] != "generate") return Fail(Usage);

string modelId = "Ternary-Bonsai-2-27B-MLC";
string? modelsDir = null, prompt = null, system = null;
int maxTokens = 0, context = 0;
double? temperature = null, topP = null;
long? seed = null;
bool verbose = false;
for (int i = 1; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
    switch (args[i])
    {
        case "--model": modelId = Next(); break;
        case "--models": modelsDir = Next(); break;
        case "--prompt": prompt = Next(); break;
        case "--system": system = Next(); break;
        case "--max-tokens": maxTokens = int.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--temperature": temperature = double.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--top-p": topP = double.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--seed": seed = long.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--context": context = int.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--verbose": verbose = true; break;
        default: return Fail($"unknown option {args[i]}\n{Usage}");
    }
}
if (prompt == null) return Fail(Usage);

modelsDir ??= FindModelsDir();
if (modelsDir == null) return Fail("no --models folder given and no .deps\\models found above the exe");
var model = Manifest.Load(Path.Combine(AppContext.BaseDirectory, "manifest.json")).Model(modelId, context);
string modelDir = Path.Combine(modelsDir, modelId);
if (!Directory.Exists(modelDir)) return Fail($"{modelDir} does not exist; run Build\\FetchModel.ps1 -ModelId {modelId}");

var wall = Stopwatch.StartNew();
using var engine = Engine.Create(new ModelFiles(model, modelDir), (level, text) =>
{
    if (verbose || level >= LogLevel.Warn) Console.Error.WriteLine($"[{level}] {text}");
});
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
messages.Add(new ChatMessage("user", prompt));
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancel.Cancel();
};
var stream = engine.Chat(new ChatRequest(messages, maxTokens, temperature, topP, seed), cancel.Token);
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

// Progress<T> posts to the thread pool, out of order; reports arrive in order here.
sealed class SyncProgress(Action<LoadProgress> report) : IProgress<LoadProgress>
{
    public void Report(LoadProgress value) => report(value);
}
