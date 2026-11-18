using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Veronica.Setup.Core;

namespace Veronica.Setup.Ui;

public abstract class ViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Changed(name);
        return true;
    }

    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class Command(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}

public static class Format
{
    public static string Size(long bytes) =>
        bytes >= 1L << 30 ? string.Format(CultureInfo.InvariantCulture, "{0:0.0} GB", bytes / (double)(1L << 30))
        : string.Format(CultureInfo.InvariantCulture, "{0:0.0} MB", bytes / (double)(1L << 20));

    public static string Progress(DownloadProgress p) =>
        p.Total > 0 ? $"{Size(p.Done)} of {Size(p.Total)}" : Size(p.Done);
}

// Progress of one long operation shown in place: a bar, a status line, a wait for the Editor, an error.
public sealed class Activity : ViewModel
{
    private bool _running;
    private string _status = "";
    private double _percent;
    private bool _indeterminate = true;
    private string _waiting = "";
    private string _error = "";

    public bool Running
    {
        get => _running;
        set
        {
            if (Set(ref _running, value))
                Changed(nameof(Progressing));
        }
    }

    public bool Progressing => _running && _waiting.Length == 0;

    public string Status { get => _status; set => Set(ref _status, value); }

    public double Percent { get => _percent; set => Set(ref _percent, value); }

    public bool Indeterminate { get => _indeterminate; set => Set(ref _indeterminate, value); }

    public string Waiting
    {
        get => _waiting;
        set
        {
            if (!Set(ref _waiting, value))
                return;
            Changed(nameof(IsWaiting));
            Changed(nameof(Progressing));
        }
    }

    public bool IsWaiting => _waiting.Length > 0;

    public string Error
    {
        get => _error;
        set
        {
            if (Set(ref _error, value))
                Changed(nameof(HasError));
        }
    }

    public bool HasError => _error.Length > 0;

    public CancellationTokenSource Cancellation { get; private set; } = new();

    public ICommand Cancel => new Command(() => Cancellation.Cancel());

    public void Begin(string status)
    {
        Cancellation = new CancellationTokenSource();
        Error = "";
        Waiting = "";
        Status = status;
        Indeterminate = true;
        Percent = 0;
        Running = true;
    }

    public void End()
    {
        Running = false;
        Waiting = "";
    }

    public void Report(DownloadProgress progress, string lead)
    {
        Indeterminate = progress.Total <= 0;
        Percent = progress.Total > 0 ? 100.0 * progress.Done / progress.Total : 0;
        Status = $"{lead} {Format.Progress(progress)}";
    }
}

public sealed class Dot(Slides slides, int index) : ViewModel
{
    public int Index => index;

    public bool IsActive => slides.Index == index;

    public bool IsReachable => index <= slides.Reachable;

    public ICommand Go => new Command(() => slides.GoTo(index));

    public void Refresh()
    {
        Changed(nameof(IsActive));
        Changed(nameof(IsReachable));
    }
}

// Pages that slide sideways under a fixed header, with dots and arrows below. Going back is always free;
// going forward only as far as the pages done so far allow.
public sealed class Slides : ViewModel
{
    private int _index;
    private int _reachable;

    public Slides(params object[] pages)
    {
        Pages = new ObservableCollection<object>(pages);
        Dots = new ObservableCollection<Dot>(pages.Select((_, i) => new Dot(this, i)));
        Previous = new Command(() => GoTo(_index - 1));
        Next = new Command(() => GoTo(_index + 1));
    }

    public ObservableCollection<object> Pages { get; }

    public ObservableCollection<Dot> Dots { get; }

    public int Index
    {
        get => _index;
        set
        {
            if (value < 0 || value > _reachable || !Set(ref _index, value))
                return;
            Refresh();
        }
    }

    public int Reachable
    {
        get => _reachable;
        set
        {
            if (!Set(ref _reachable, Math.Clamp(value, 0, Pages.Count - 1)))
                return;
            if (_index > _reachable)
                Index = _reachable;
            Refresh();
        }
    }

    public bool CanPrevious => _index > 0;

    public bool CanNext => _index < _reachable;

    public ICommand Previous { get; }

    public ICommand Next { get; }

    public void GoTo(int index) => Index = index;

    public void Open(int index)
    {
        if (index > Reachable)
            Reachable = index;
        Index = index;
    }

    private void Refresh()
    {
        Changed(nameof(CanPrevious));
        Changed(nameof(CanNext));
        foreach (var dot in Dots)
            dot.Refresh();
    }
}

public enum ProviderKind
{
    Local,
    Claude,
    OpenAi,
    BareMcp,
}

public static class Providers
{
    public static string SettingsValue(ProviderKind kind) => kind switch
    {
        ProviderKind.Claude => "claude",
        ProviderKind.OpenAi => "openai",
        ProviderKind.BareMcp => "bare_mcp",
        _ => "webqwen",
    };

    public static ProviderKind FromSettings(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "claude" => ProviderKind.Claude,
        "openai" => ProviderKind.OpenAi,
        "bare_mcp" => ProviderKind.BareMcp,
        _ => ProviderKind.Local,
    };

    public static string Name(ProviderKind kind) => kind switch
    {
        ProviderKind.Claude => "Claude Code",
        ProviderKind.OpenAi => "OpenAI-compatible server",
        ProviderKind.BareMcp => "Your own AI agent",
        _ => "Local model",
    };

    public static string Pitch(ProviderKind kind) => kind switch
    {
        ProviderKind.Claude => "Frontier model through your Claude subscription. Runs through the Claude Code you have installed.",
        ProviderKind.OpenAi => "Your server, your model. llama.cpp, LM Studio, Ollama or any service with an OpenAI-style API.",
        ProviderKind.BareMcp => "No model in the Editor. The agent you already use works on your mod through Veronica's tools over MCP.",
        _ => "A small model on your graphics card. One large download, then it works offline and nothing leaves your machine.",
    };
}

public sealed class WelcomeSlide : ViewModel
{
    private readonly Func<string, string> _warningFor;
    private string _folder = "";
    private string _warning = "";
    private string _error = "";
    private bool _locked;

    public WelcomeSlide(string folder, bool locked, Func<string, string> warningFor, Func<Task<string?>> pickFolder, Action<WelcomeSlide> start)
    {
        _warningFor = warningFor;
        Folder = folder;
        Locked = locked;
        Change = new Command(async () =>
        {
            if (await pickFolder() is { Length: > 0 } picked)
                Folder = picked;
        });
        Start = new Command(() => start(this));
    }

    public string Folder
    {
        get => _folder;
        private set
        {
            if (!Set(ref _folder, value))
                return;
            Warning = _warningFor(value);
            Error = "";
        }
    }

    public string Warning
    {
        get => _warning;
        private set
        {
            if (Set(ref _warning, value))
                Changed(nameof(HasWarning));
        }
    }

    public bool HasWarning => _warning.Length > 0;

    public string Error
    {
        get => _error;
        set
        {
            if (Set(ref _error, value))
                Changed(nameof(HasError));
        }
    }

    public bool HasError => _error.Length > 0;

    // The install already belongs to this folder: it was copied here, started here, or the files are in.
    public bool Locked
    {
        get => _locked;
        set => Set(ref _locked, value);
    }

    public bool StartMenuShortcut { get; set; } = true;

    public bool DesktopShortcut { get; set; } = true;

    public ICommand Change { get; }

    public ICommand Start { get; }
}

public sealed class ProviderCard(ProviderKind kind, Action<ProviderKind> choose) : ViewModel
{
    private bool _selected;

    public ProviderKind Kind => kind;

    public string Name => Providers.Name(kind);

    public string Pitch => Providers.Pitch(kind);

    public bool Selected { get => _selected; set => Set(ref _selected, value); }

    public ICommand Choose => new Command(() => choose(kind));
}

public sealed class ChooseSlide : ViewModel
{
    public ChooseSlide(Action<ProviderKind> choose, ProviderKind selected)
    {
        Cards = new ObservableCollection<ProviderCard>(Enum.GetValues<ProviderKind>().Select(k => new ProviderCard(k, choose)));
        Select(selected);
    }

    public ObservableCollection<ProviderCard> Cards { get; }

    public void Select(ProviderKind kind)
    {
        foreach (var card in Cards)
            card.Selected = card.Kind == kind;
    }
}

public sealed class ConnectSlide : ViewModel
{
    private ConnectPage? _content;

    public ConnectPage? Content
    {
        get => _content;
        set
        {
            var old = _content;
            if (Set(ref _content, value))
                old?.Leave();
        }
    }
}

public enum ConnectState
{
    None,
    Connecting,
    Connected,
    Failed,
}

// One provider's setup: its fields and one button that checks the connection, shows it passed, then saves.
public abstract class ConnectPage : ViewModel
{
    private ConnectState _state;
    private string _error = "";
    private bool _filesReady;
    private string _saved = "";

    protected ConnectPage(ProviderKind kind, string continueLabel, Action<ConnectPage> connect)
    {
        Kind = kind;
        ContinueLabel = continueLabel;
        Continue = new Command(() =>
        {
            if (!_filesReady || !Valid)
                return;
            Pressed?.Invoke();
            if (_state is ConnectState.Connecting or ConnectState.Connected)
                return;
            connect(this);
        });
    }

    public ProviderKind Kind { get; }

    public string Name => Providers.Name(Kind);

    public string Pitch => Providers.Pitch(Kind);

    public string ContinueLabel { get; }

    public ICommand? Back { get; init; }

    // Runs on every press of the button, also while the check runs.
    public Action? Pressed { get; init; }

    public ConnectState State
    {
        get => _state;
        private set
        {
            if (!Set(ref _state, value))
                return;
            Changed(nameof(IsConnecting));
            Changed(nameof(IsConnected));
            Changed(nameof(IsFailed));
            Refresh();
        }
    }

    public bool IsConnecting => _state == ConnectState.Connecting;

    public bool IsConnected => _state == ConnectState.Connected;

    public bool IsFailed => _state == ConnectState.Failed;

    public string Error { get => _error; private set => Set(ref _error, value); }

    // Whether the fields are complete enough to try connecting.
    protected abstract bool Valid { get; }

    public bool FilesReady
    {
        get => _filesReady;
        set
        {
            if (Set(ref _filesReady, value))
                Refresh();
        }
    }

    // A working button stays enabled so it keeps its color; clicks are ignored meanwhile.
    public bool ButtonEnabled => _state is ConnectState.Connecting or ConnectState.Connected || (_filesReady && Valid);

    protected virtual string ConnectingText => "Connecting...";

    // The page is no longer shown.
    public virtual void Leave()
    {
    }

    public string ContinueText => _state switch
    {
        ConnectState.Connecting => ConnectingText,
        ConnectState.Connected => "Connected",
        _ => _filesReady ? ContinueLabel : "Finishing installation...",
    };

    public string Saved
    {
        get => _saved;
        set
        {
            if (Set(ref _saved, value))
                Changed(nameof(HasSaved));
        }
    }

    public bool HasSaved => _saved.Length > 0;

    public ICommand Continue { get; }

    public void Connecting()
    {
        Error = "";
        Saved = "";
        State = ConnectState.Connecting;
    }

    public void Show(CheckResult result)
    {
        Error = result.Ok ? "" : result.Message;
        State = result.Ok ? ConnectState.Connected : ConnectState.Failed;
    }

    // After the save the button returns to rest; Saved tells what happened.
    public void Idle() => State = ConnectState.None;

    // Any edit makes an earlier result stale.
    protected void Edited()
    {
        Error = "";
        Saved = "";
        State = ConnectState.None;
        Refresh();
    }

    public void Refresh()
    {
        Changed(nameof(ButtonEnabled));
        Changed(nameof(ContinueText));
    }
}

public sealed class ClaudeConnectPage(string? model, string? effort, string continueLabel, Action<ConnectPage> connect)
    : ConnectPage(ProviderKind.Claude, continueLabel, connect)
{
    private string _model = model ?? "";
    private string _effort = effort ?? "";

    protected override bool Valid => true;

    public string Model
    {
        get => _model;
        set
        {
            if (Set(ref _model, value))
                Edited();
        }
    }

    public string Effort
    {
        get => _effort;
        set
        {
            if (Set(ref _effort, value))
                Edited();
        }
    }
}

public sealed class OpenAiConnectPage(string? url, string? apiKey, string? model, string? effort, string continueLabel, Action<ConnectPage> connect)
    : ConnectPage(ProviderKind.OpenAi, continueLabel, connect)
{
    private string _url = url ?? "";
    private string _apiKey = apiKey ?? "";
    private string _model = model ?? "";
    private string _effort = effort ?? "";

    // An empty address looks for a server on this machine.
    protected override bool Valid => true;

    public string Url
    {
        get => _url;
        set
        {
            if (Set(ref _url, value))
                Edited();
        }
    }

    public string ApiKey
    {
        get => _apiKey;
        set
        {
            if (Set(ref _apiKey, value))
                Edited();
        }
    }

    public string Model
    {
        get => _model;
        set
        {
            if (Set(ref _model, value))
                Edited();
        }
    }

    public string Effort
    {
        get => _effort;
        set
        {
            if (Set(ref _effort, value))
                Edited();
        }
    }
}

// Waits for the user's agent to connect to a check server on Veronica's MCP port; the check may be skipped.
// The button copies the prompt and starts waiting; pressing it again copies again.
public sealed class BareMcpConnectPage(string prompt, Action<ConnectPage> connect)
    : ConnectPage(ProviderKind.BareMcp, "Copy prompt", connect)
{
    private IDisposable? _listener;
    private CancellationTokenSource? _left;
    private int _epoch;

    protected override bool Valid => true;

    protected override string ConnectingText => "Copied. Waiting for your agent...";

    public string Prompt => prompt;

    public ICommand? Skip { get; init; }

    // Counts the times the page was left, so a check started before one does not attach to the page after it.
    public int Epoch => _epoch;

    // Kept until the page is left, so the agent's requests after its first one are answered too.
    // The token is cancelled when the page is left, and at once when it was left since epoch.
    public CancellationToken Hold(IDisposable listener, int epoch)
    {
        if (epoch != _epoch)
        {
            listener.Dispose();
            return new CancellationToken(canceled: true);
        }
        _listener?.Dispose();
        _left?.Cancel();
        _listener = listener;
        _left = new CancellationTokenSource();
        return _left.Token;
    }

    public override void Leave()
    {
        _epoch++;
        _listener?.Dispose();
        _listener = null;
        _left?.Cancel();
        _left = null;
    }
}

public sealed class LocalConnectPage(bool understood, string continueLabel, Action<ConnectPage> connect)
    : ConnectPage(ProviderKind.Local, continueLabel, connect)
{
    private bool _understood = understood;
    private bool _busy;

    protected override bool Valid => Chosen is { Installed: true } && !_busy;

    public ObservableCollection<ModelCard> Models { get; } = new();

    public bool HasModels => Models.Count > 0;

    public bool Understood
    {
        get => _understood;
        set
        {
            if (Set(ref _understood, value))
                Changed(nameof(CardsEnabled));
        }
    }

    // One download or delete at a time.
    public bool Busy
    {
        get => _busy;
        set
        {
            if (!Set(ref _busy, value))
                return;
            Changed(nameof(CardsEnabled));
            Refresh();
        }
    }

    public bool CardsEnabled => _understood && !_busy;

    public ModelCard? Chosen => Models.FirstOrDefault(m => m.Chosen);

    public void SetModels(IEnumerable<ModelCard> models)
    {
        Models.Clear();
        foreach (var model in models)
            Models.Add(model);
        Changed(nameof(HasModels));
    }

    public void Choose(ModelCard? card)
    {
        foreach (var model in Models)
            model.Chosen = model == card;
        Edited();
    }
}

public sealed class ModelCard : ViewModel
{
    private bool _installed;
    private bool _chosen;

    public ModelCard(ModelRecord model, bool installed, Action<ModelCard> choose, Action<ModelCard> delete)
    {
        Model = model;
        _installed = installed;
        Choose = new Command(() => choose(this));
        Delete = new Command(() => delete(this));
    }

    public ModelRecord Model { get; }

    public string Id => Model.Id;

    public string Name => Model.DisplayName ?? Model.Id;

    public string Description => Model.Description ?? "";

    public bool HasDescription => Description.Length > 0;

    public string Requirements => Model.GraphicsCardGb is { } card
        ? $"{card} GB graphics card, {Format.Size(Model.DownloadBytes)} download"
        : string.Format(CultureInfo.InvariantCulture, "{0:0} GB graphics memory, {1} download", Math.Ceiling(Model.VramRequiredMb / 1024.0), Format.Size(Model.DownloadBytes));

    public Activity Activity { get; } = new();

    public bool Installed
    {
        get => _installed;
        set
        {
            Set(ref _installed, value);
        }
    }

    public bool Chosen
    {
        get => _chosen;
        set
        {
            Set(ref _chosen, value);
        }
    }

    public ICommand Choose { get; }

    public ICommand Delete { get; }
}

// The footer strip of first run: Veronica's own files arriving while the modder sets up a brain.
public sealed class FilesStrip : ViewModel
{
    private bool _done;

    public FilesStrip(Action retry) => Retry = new Command(retry);

    public Activity Activity { get; } = new();

    public bool Done
    {
        get => _done;
        set
        {
            if (Set(ref _done, value))
                Changed(nameof(Visible));
        }
    }

    public bool Visible => _done || Activity.Running || Activity.HasError;

    public ICommand Retry { get; }

    public void Touch() => Changed(nameof(Visible));
}

public sealed class SetupFlow(Slides slides, FilesStrip files) : ViewModel
{
    public Slides Slides => slides;

    public FilesStrip Files => files;
}

public sealed class ModCard : ViewModel
{
    // remove is set on a project the modder added by hand; key tells cards apart when two share a folder.
    public ModCard(string name, string folder, string? project, HookState state, Action<ModCard> start, Action<ModCard, bool> change,
        string? key = null, Action<ModCard>? remove = null)
    {
        Name = name;
        Folder = folder;
        Key = key ?? folder;
        Project = project;
        State = state;
        Note = project == null ? "No .vrgproj file found."
            : state == HookState.Foreign ? "Loads Veronica from another build or folder."
            : "";
        Start = new Command(() => start(this));
        Install = new Command(() => change(this, true));
        Uninstall = new Command(() => change(this, false));
        Added = remove != null;
        Remove = new Command(() => remove?.Invoke(this));
    }

    public string Name { get; }

    public string Folder { get; }

    public string Key { get; }

    public bool Added { get; }

    public bool CanRemove => Added && !Activity.Running;

    public ICommand Remove { get; }

    public string? Project { get; }

    public HookState State { get; }

    public bool CanAct => Project != null && !Activity.Running;

    public bool Installed => State != HookState.NotHooked;

    public string Note { get; init; }

    public bool HasNote => Note.Length > 0;

    public Activity Activity { get; } = new();

    public ICommand Start { get; }

    public ICommand Install { get; }

    public ICommand Uninstall { get; }

    public void Touch()
    {
        Changed(nameof(CanAct));
        Changed(nameof(CanRemove));
    }
}

public sealed class ModsPane(Action changeEditor, Action changeGame, Action addProject) : ViewModel
{
    private string _message = "";
    private string _editor = "";
    private string _game = "";

    public ObservableCollection<ModCard> Mods { get; } = new();

    public ICommand AddProject { get; } = new Command(addProject);

    // Empty when neither the settings nor Steam know one; the first Start Editor then asks for it.
    public string Editor
    {
        get => _editor;
        set
        {
            if (Set(ref _editor, value))
                Changed(nameof(EditorText));
        }
    }

    public string EditorText => _editor.Length > 0 ? _editor : "Not found in Steam. The first Start Editor asks for VRage.Editor.exe.";

    public ICommand ChangeEditor { get; } = new Command(changeEditor);

    public string Game
    {
        get => _game;
        set
        {
            if (Set(ref _game, value))
                Changed(nameof(GameText));
        }
    }

    public string GameText => _game.Length > 0 ? _game : "Not found. The Editor starts without the game.";

    public ICommand ChangeGame { get; } = new Command(changeGame);

    public string Message
    {
        get => _message;
        set
        {
            if (Set(ref _message, value))
                Changed(nameof(HasMessage));
        }
    }

    public bool HasMessage => _message.Length > 0;
}

public sealed class ConflictCard(MergeConflict conflict, string name, Action<ConflictCard> keepMine, Action<ConflictCard> useNew) : ViewModel
{
    public MergeConflict Conflict => conflict;

    public string Name => name;

    public string Note => conflict.Dropped
        ? "You changed this file; the new version removes it."
        : "You changed this file; the new version changes it too.";

    public ICommand KeepMine => new Command(() => keepMine(this));

    public ICommand UseNew => new Command(() => useNew(this));

    public ICommand Open => new Command(() => SetupHost.Open(new[] { conflict.Mine, conflict.Incoming }.Where(File.Exists).ToArray()));
}

public sealed class UpdatesPane : ViewModel
{
    private string _installed = "";
    private string _latest = "";
    private bool _canUpdate;
    private bool _upToDate;
    private string _warning = "";
    private string _conflictsMessage = "";

    public UpdatesPane(Action update, Action check)
    {
        Update = new Command(update);
        Check = new Command(check);
        Conflicts.CollectionChanged += (_, _) =>
        {
            Changed(nameof(HasConflicts));
            Changed(nameof(NeedsAttention));
        };
    }

    public string Installed { get => _installed; set => Set(ref _installed, value); }

    public string Latest { get => _latest; set => Set(ref _latest, value); }

    public bool CanUpdate
    {
        get => _canUpdate;
        set
        {
            if (Set(ref _canUpdate, value))
                Changed(nameof(NeedsAttention));
        }
    }

    public bool UpToDate { get => _upToDate; set => Set(ref _upToDate, value); }

    public string Warning
    {
        get => _warning;
        set
        {
            if (Set(ref _warning, value))
                Changed(nameof(HasWarning));
        }
    }

    public bool HasWarning => _warning.Length > 0;

    public Activity Activity { get; } = new();

    public ObservableCollection<ConflictCard> Conflicts { get; } = new();

    public bool HasConflicts => Conflicts.Count > 0;

    public string ConflictsMessage
    {
        get => _conflictsMessage;
        set
        {
            if (Set(ref _conflictsMessage, value))
                Changed(nameof(HasConflictsMessage));
        }
    }

    public bool HasConflictsMessage => _conflictsMessage.Length > 0;

    public bool NeedsAttention => _canUpdate || Conflicts.Count > 0;

    public ICommand Update { get; }

    public ICommand Check { get; }
}

public enum AppTab
{
    Mods,
    Brain,
    Updates,
}

public sealed class AppShell(ModsPane mods, Slides brain, UpdatesPane updates) : ViewModel
{
    private AppTab _tab;

    public ModsPane Mods => mods;

    public Slides Brain => brain;

    public UpdatesPane Updates => updates;

    public AppTab Tab
    {
        get => _tab;
        set
        {
            if (!Set(ref _tab, value))
                return;
            Changed(nameof(Content));
        }
    }

    public object Content => _tab switch
    {
        AppTab.Brain => brain,
        AppTab.Updates => updates,
        _ => mods,
    };
}

public sealed class ErrorPage(string message, Action retry, Action close) : ViewModel
{
    public string Message => message;

    public ICommand Retry { get; } = new Command(retry);

    public ICommand Close { get; } = new Command(close);
}
