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

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        Changed(name);
    }

    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class Command(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}

public sealed class WelcomePage(string root, string warning, Action next) : ViewModel
{
    public string Root => root;

    public string Warning => warning;

    public bool HasWarning => warning.Length > 0;

    public ICommand Continue { get; } = new Command(next);
}

public sealed class ProgressPage : ViewModel
{
    private string _status = "";
    private double _percent;
    private bool _indeterminate = true;
    private string _error = "";

    public ProgressPage(string title, Action retry, Action back)
    {
        Title = title;
        Retry = new Command(retry);
        Back = new Command(back);
        Cancel = new Command(() => Cancellation.Cancel());
    }

    public CancellationTokenSource Cancellation { get; } = new();

    public ICommand Cancel { get; }

    public string Title { get; }

    public string Status { get => _status; set => Set(ref _status, value); }

    public double Percent { get => _percent; set => Set(ref _percent, value); }

    public bool Indeterminate { get => _indeterminate; set => Set(ref _indeterminate, value); }

    public string Error
    {
        get => _error;
        set
        {
            Set(ref _error, value);
            Changed(nameof(HasError));
        }
    }

    public bool HasError => _error.Length > 0;

    public ICommand Retry { get; }

    public ICommand Back { get; }

    public void Report(DownloadProgress progress)
    {
        Indeterminate = progress.Total <= 0;
        Percent = progress.Total > 0 ? 100.0 * progress.Done / progress.Total : 0;
        Status = string.Format(CultureInfo.InvariantCulture, "{0}: {1} / {2}", progress.File, Size(progress.Done), Size(progress.Total));
    }

    public static string Size(long bytes) =>
        bytes >= 1L << 30 ? string.Format(CultureInfo.InvariantCulture, "{0:0.0} GB", bytes / (double)(1L << 30))
        : string.Format(CultureInfo.InvariantCulture, "{0:0.0} MB", bytes / (double)(1L << 20));
}

public sealed class WaitEditorPage(string reason, Action cancel) : ViewModel
{
    public string Reason => reason;

    public ICommand Cancel { get; } = new Command(cancel);
}

public sealed class ProviderSelectPage(Action local, Action claude, Action openAi, Action? back) : ViewModel
{
    public ICommand Local { get; } = new Command(local);

    public ICommand Claude { get; } = new Command(claude);

    public ICommand OpenAi { get; } = new Command(openAi);

    public bool CanGoBack => back != null;

    public ICommand Back { get; } = new Command(back ?? (() => { }));
}

public sealed class ModelItem : ViewModel
{
    private bool _installed;
    private bool _active;

    public ModelItem(ModelRecord model, bool installed, bool active, Action<ModelItem> download, Action<ModelItem> delete, Action<ModelItem> use)
    {
        Model = model;
        _installed = installed;
        _active = active;
        Download = new Command(() => download(this));
        Delete = new Command(() => delete(this));
        Use = new Command(() => use(this));
    }

    public ModelRecord Model { get; }

    public string Name => Model.Id;

    public string Details => string.Format(CultureInfo.InvariantCulture, "{0} download, needs {1:0.0} GB VRAM", ProgressPage.Size(Model.DownloadBytes), Model.VramRequiredMb / 1024.0);

    public bool Installed
    {
        get => _installed;
        set
        {
            Set(ref _installed, value);
            Changed(nameof(NotInstalled));
            Changed(nameof(CanUse));
            Changed(nameof(State));
        }
    }

    public bool NotInstalled => !_installed;

    public bool Active
    {
        get => _active;
        set
        {
            Set(ref _active, value);
            Changed(nameof(CanUse));
            Changed(nameof(State));
        }
    }

    public bool CanUse => _installed && !_active;

    public string State => _active ? "Active" : _installed ? "Installed" : "";

    public ICommand Download { get; }

    public ICommand Delete { get; }

    public ICommand Use { get; }
}

public sealed class LocalModelsPage(IEnumerable<ModelItem> models, Action next, Action back) : ViewModel
{
    private string _message = "";

    public ObservableCollection<ModelItem> Models { get; } = new(models);

    public string Message { get => _message; set => Set(ref _message, value); }

    public ICommand Continue { get; } = new Command(next);

    public ICommand Back { get; } = new Command(back);
}

public sealed class ClaudeSetupPage(string? model, string? effort, Action<ClaudeSetupPage> next, Action back) : ViewModel
{
    public string Model { get; set; } = model ?? "";

    public string Effort { get; set; } = effort ?? "";

    public ICommand Continue => new Command(() => next(this));

    public ICommand Back { get; } = new Command(back);
}

public sealed class OpenAiSetupPage(string? url, string? model, string? apiKey, string? effort, Action<OpenAiSetupPage> next, Action back) : ViewModel
{
    public string Url { get; set; } = url ?? "";

    public string Model { get; set; } = model ?? "";

    public string ApiKey { get; set; } = apiKey ?? "";

    public string Effort { get; set; } = effort ?? "";

    public ICommand Continue => new Command(() => next(this));

    public ICommand Back { get; } = new Command(back);
}

public sealed class CheckPage : ViewModel
{
    private string _status = "Checking...";
    private bool _done;
    private bool _ok;

    public CheckPage(Action finish, Action retry, Action back)
    {
        Finish = new Command(finish);
        Retry = new Command(retry);
        Back = new Command(back);
    }

    public string Status { get => _status; set => Set(ref _status, value); }

    public bool Ok { get => _ok; private set => Set(ref _ok, value); }

    public bool Failed => _done && !_ok;

    public bool Checking => !_done;

    public void Show(CheckResult result)
    {
        _done = true;
        Ok = result.Ok;
        Status = result.Message;
        Changed(nameof(Failed));
        Changed(nameof(Checking));
    }

    public ICommand Finish { get; }

    public ICommand Retry { get; }

    public ICommand Back { get; }
}

public sealed class ModItem(string name, string folder, string? project, HookState state, Action<ModItem> toggle) : ViewModel
{
    public string Name => name;

    public string Folder => folder;

    public string? Project => project;

    public HookState State => state;

    public bool IsHooked => state is HookState.Hooked or HookState.Foreign;

    public bool CanToggle => project != null && state != HookState.Foreign;

    public string Note { get; init; } = project == null ? "no .vrgproj found"
        : state == HookState.Foreign ? "loads Veronica from another build or folder, left alone"
        : "";

    public ICommand Toggle => new Command(() => toggle(this));
}

public sealed class ConflictItem(MergeConflict conflict, string name, Action<ConflictItem> keepMine, Action<ConflictItem> useNew) : ViewModel
{
    public MergeConflict Conflict => conflict;

    public string Name => name;

    public string Note => conflict.Dropped ? "the new release removes this file; you changed it" : "you changed this file; the new release changes it too";

    public ICommand KeepMine => new Command(() => keepMine(this));

    public ICommand UseNew => new Command(() => useNew(this));

    public ICommand Open => new Command(() => SetupHost.Open(new[] { conflict.Mine, conflict.Incoming }.Where(File.Exists).ToArray()));
}

public sealed class AppPage : ViewModel
{
    private string _provider = "";
    private string _versionText = "";
    private string _latestText = "";
    private bool _canUpdate;
    private string _modsMessage = "";
    private string _conflictsMessage = "";
    private string _warning = "";

    public AppPage(Action changeProvider, Action update, Action checkUpdates)
    {
        ChangeProvider = new Command(changeProvider);
        Update = new Command(update);
        CheckUpdates = new Command(checkUpdates);
    }

    public string Warning
    {
        get => _warning;
        set
        {
            Set(ref _warning, value);
            Changed(nameof(HasWarning));
        }
    }

    public bool HasWarning => _warning.Length > 0;

    public string ConflictsMessage { get => _conflictsMessage; set => Set(ref _conflictsMessage, value); }

    public string Provider { get => _provider; set => Set(ref _provider, value); }

    public ObservableCollection<ModItem> Mods { get; } = new();

    public string ModsMessage { get => _modsMessage; set => Set(ref _modsMessage, value); }

    public string VersionText { get => _versionText; set => Set(ref _versionText, value); }

    public string LatestText { get => _latestText; set => Set(ref _latestText, value); }

    public bool CanUpdate { get => _canUpdate; set => Set(ref _canUpdate, value); }

    public ObservableCollection<ConflictItem> Conflicts { get; } = new();

    public ICommand ChangeProvider { get; }

    public ICommand Update { get; }

    public ICommand CheckUpdates { get; }
}
