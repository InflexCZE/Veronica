using Avalonia.Threading;
using Veronica.Agent;
using Veronica.Setup.Core;

namespace Veronica.Setup.Ui;

// The window's flow (Docs/Tech/Distribution.md): first run slides through Welcome, the brain choice and its
// setup while Veronica's files arrive in the background; afterwards app mode has the Mods, Brain and Updates tabs.
public sealed class MainViewModel(SetupHost host, Action shutdown) : ViewModel
{
    private const string EditorWaitFiles = "Close the Editor to continue. Veronica's files are replaced as soon as it is closed.";
    private const string EditorWaitModel = "Close the Editor to continue, it may be running this model. Deleting starts as soon as it is closed.";
    private const string EditorWaitMod = "Close the Editor to continue, it would overwrite this change. The change is made as soon as it is closed.";
    private const string SavedText = "Saved. Takes effect at the next Editor start.";

    private object? _page;
    private AppShell? _app;
    private bool _selfUpdated;
    private bool _updating;
    private bool _filesReady;
    private bool _installing;
    private FilesStrip? _strip;
    private ConnectSlide? _setupConnect;
    private ConnectSlide? _brainConnect;
    private int _modelOps;

    public object? Page
    {
        get => _page;
        private set
        {
            if (!Set(ref _page, value))
                return;
            Changed(nameof(App));
            Changed(nameof(IsApp));
        }
    }

    public AppShell? App => _page as AppShell;

    public bool IsApp => _page is AppShell;

    public string Title => host.Version is { } v ? $"Veronica {v}" : "Veronica";

    private bool FirstRun => !host.Files.FileExists(host.Layout.SettingsFile) || host.Record.Version == null;

    public Func<Task<string?>> PickFolder { get; set; } = () => Task.FromResult<string?>(null);

    public Func<Task<string?>> PickEditor { get; set; } = () => Task.FromResult<string?>(null);

    public Func<Task<string?>> PickGame { get; set; } = () => Task.FromResult<string?>(null);

    public void Start(bool update, bool selfUpdated, bool install)
    {
        _selfUpdated = selfUpdated;
        Safe(() =>
        {
            new SelfUpdate(host.Http, host.Files, host.Layout).CleanUp(host.RunningExe);
            host.Log($"Started {host.RunningExe} {host.Version}");
            if (FirstRun)
            {
                ShowSetup(install);
                return;
            }
            ShowApp(update ? AppTab.Updates : AppTab.Mods);
            if (update)
                RunUpdate();
        });
    }

    // Another start of Veronica.exe asked this one to update (the panel's button while this window was open).
    public void RequestUpdate()
    {
        if (_app == null || _updating || !IsApp)
            return;
        _app.Tab = AppTab.Updates;
        RunUpdate();
    }

    // Startup and app-mode refreshes read files the user may have broken; show the error instead of dying.
    private void Safe(Action work)
    {
        try
        {
            work();
        }
        catch (Exception e)
        {
            host.Log($"Failed: {e}");
            Page = new ErrorPage(e.Message, () => Start(update: false, _selfUpdated, install: false), shutdown);
        }
    }

    // install: this exe was just copied into the folder Welcome chose, so it goes on where Welcome left off.
    private void ShowSetup(bool install)
    {
        // A previous run that installed and was closed before the brain was set up needs no second install.
        _filesReady = host.Record.Version != null;
        _strip = new FilesStrip(StartInstall) { Done = _filesReady };
        _setupConnect = new ConnectSlide();
        Slides slides = null!;
        ChooseSlide choose = null!;
        // The settings file marks a folder Welcome already chose: the copy writes it there before restarting.
        bool locked = install || _filesReady || host.Files.FileExists(host.Layout.SettingsFile);
        var welcome = new WelcomeSlide(locked || !SingleFile ? host.Layout.Root : DefaultFolder, locked, ForeignWarning, () => PickFolder(), w =>
        {
            if (InstallInto(w))
                slides.Open(1);
        });
        choose = new ChooseSlide(kind =>
        {
            choose.Select(kind);
            if (_setupConnect.Content?.Kind != kind)
                _setupConnect.Content = Connect(kind, "Continue", () => slides.GoTo(1), _ => ShowApp(AppTab.Mods));
            slides.Open(2);
        }, ProviderKind.Local);
        slides = new Slides(welcome, choose, _setupConnect);
        Page = new SetupFlow(slides, _strip);
        if (install)
        {
            StartInstall();
            slides.Open(1);
        }
    }

    // A development build's exe is only a launcher for the DLLs beside it, so a copy of it alone cannot start.
    // The empty Location that IL3000 warns about is exactly what marks the single-file build.
#pragma warning disable IL3000
    private static bool SingleFile => string.IsNullOrEmpty(typeof(MainViewModel).Assembly.Location);
#pragma warning restore IL3000

    private static string DefaultFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Veronica");

    private string ForeignWarning(string folder)
    {
        try
        {
            return InstallLayout.ForeignEntries(host.Files.Entries(folder), Path.GetFileName(host.RunningExe)).Count == 0 ? ""
                : "This folder holds other files. Veronica's folders go beside them.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"This folder cannot be read: {e.Message}";
        }
    }

    // Installs here when this exe already is the chosen folder's Veronica.exe; otherwise copies itself there and
    // restarts from the copy, leaving the original where it is. Returns whether the flow goes on in this process.
    private bool InstallInto(WelcomeSlide welcome)
    {
        if (_installing || _filesReady)
            return true;
        if (welcome.Locked)
        {
            StartInstall();
            return true;
        }
        string folder = Path.GetFullPath(welcome.Folder);
        string exe = Path.Combine(folder, Release.ExeAsset);
        bool here = string.Equals(Path.GetFullPath(host.RunningExe), exe, StringComparison.OrdinalIgnoreCase);
        if (!here && !SingleFile)
        {
            welcome.Error = "A development build installs only into its own folder.";
            return false;
        }
        try
        {
            if (!here)
            {
                host.Files.CreateDirectory(folder);
                File.Copy(host.RunningExe, exe, overwrite: true);
                string settings = Path.Combine(folder, Settings.FileName);
                if (!host.Files.FileExists(settings))
                    host.Files.WriteAllText(settings, SettingsFile.Empty.Serialize());
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            host.Log($"Copying {host.RunningExe} to {exe} failed: {e}");
            welcome.Error = $"Veronica.exe could not be copied to {folder}: {e.Message}";
            return false;
        }
        CreateShortcuts(welcome, exe);
        welcome.Locked = true;
        if (here)
        {
            StartInstall();
            return true;
        }
        host.Log($"Copied {host.RunningExe} to {exe}, restarting from it");
        try
        {
            host.PrepareRestart(exe, [Program.InstallArgument])();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException)
        {
            host.Log($"Starting {exe} failed: {e}");
            welcome.Error = $"Veronica.exe was copied to {folder} but could not be started there: {e.Message}";
        }
        return false;
    }

    // A missing shortcut does not stop the install.
    private void CreateShortcuts(WelcomeSlide welcome, string exe)
    {
        var links = new List<string>();
        if (welcome.StartMenuShortcut)
            links.Add(Shortcut.StartMenu);
        if (welcome.DesktopShortcut)
            links.Add(Shortcut.Desktop);
        foreach (var link in links)
        {
            try
            {
                Shortcut.Create(link, exe);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
            {
                host.Log($"Creating the shortcut {link} failed: {e.Message}");
            }
        }
    }

    // First run fetches Veronica's files while the modder sets up a brain. Veronica.exe does not update itself
    // here; the next update does that.
    private void StartInstall()
    {
        if (_filesReady || _installing || _strip == null)
            return;
        _installing = true;
        var strip = _strip;
        var activity = strip.Activity;
        activity.Begin("Installing...");
        strip.Touch();
        _ = Run(async () =>
        {
            try
            {
                var token = activity.Cancellation.Token;
                var release = await ReleaseFeed.LatestAsync(host.Http, host.ReleasesUrl, token);
                host.Log($"Latest release {release.Tag} from {host.ReleasesUrl}");
                var result = await new ReleaseInstaller(host.Http, host.Files, host.Layout).InstallAsync(release,
                    Throttled.Progress(p => activity.Report(p, "Installing...")), OnUi(async () =>
                    {
                        await WaitForEditorAsync(activity, EditorWaitFiles, token);
                        activity.Status = "Putting files in place...";
                    }), token);
                host.Log($"Installed {result.Version}, {result.Conflicts.Count} conflicts");
                foreach (var model in result.ModelsToReplace)
                    await DownloadModelAsync(model, Throttled.Progress(p => activity.Report(p, $"Updating {model.Id}...")), token);
                _filesReady = true;
                activity.End();
                strip.Done = true;
                if (_setupConnect?.Content is { } page)
                {
                    page.FilesReady = true;
                    if (page is LocalConnectPage local)
                        LoadModels(local);
                }
            }
            catch (OperationCanceledException) when (activity.Cancellation.IsCancellationRequested)
            {
                host.Log("Install stopped");
                activity.End();
                activity.Error = "stopped. Close the Editor and try again.";
            }
            catch (Exception e)
            {
                host.Log($"Install failed: {e}");
                activity.End();
                activity.Error = e.Message;
            }
            finally
            {
                _installing = false;
                strip.Touch();
            }
        });
    }

    private ConnectPage Connect(ProviderKind kind, string continueLabel, Action back, Action<ConnectPage> saved)
    {
        var settings = host.Settings;
        var backCommand = new Command(back);
        ConnectPage page = kind switch
        {
            ProviderKind.Claude => new ClaudeConnectPage(settings.Get(Settings.VERONICA_CLAUDE_MODEL), settings.Get(Settings.VERONICA_CLAUDE_EFFORT),
                continueLabel, p => ConnectAndSave(p, saved)) { Back = backCommand },
            ProviderKind.OpenAi => new OpenAiConnectPage(settings.Get(Settings.VERONICA_OPENAI_URL), settings.Get(Settings.VERONICA_OPENAI_API_KEY),
                settings.Get(Settings.VERONICA_OPENAI_MODEL), settings.Get(Settings.VERONICA_OPENAI_EFFORT), continueLabel, p => ConnectAndSave(p, saved)) { Back = backCommand },
            _ => new LocalConnectPage(understood: false, continueLabel, p => ConnectAndSave(p, saved)) { Back = backCommand },
        };
        page.FilesReady = _filesReady;
        if (page is LocalConnectPage local)
            LoadModels(local);
        return page;
    }

    private static string? Blank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // The button spins while the check runs; a pass shows for a moment before the flow moves on.
    private void ConnectAndSave(ConnectPage page, Action<ConnectPage> saved)
    {
        page.Connecting();
        Func<Task<CheckResult>> check = page switch
        {
            ClaudeConnectPage => () => ProviderCheck.ClaudeAsync(CancellationToken.None),
            OpenAiConnectPage openAi => OpenAiCheck(openAi),
            LocalConnectPage local => LocalCheck(local),
            _ => throw new InvalidOperationException(page.GetType().Name),
        };
        _ = Run(async () =>
        {
            CheckResult result;
            try
            {
                result = await Task.Run(check);
            }
            catch (Exception e)
            {
                result = new CheckResult(false, e.Message);
            }
            host.Log($"Provider check: {result.Ok} {result.Message} {result.Url}");
            if (result.Url != null && page is OpenAiConnectPage openAi)
                openAi.Url = result.Url;
            page.Show(result);
            if (!result.Ok)
                return;
            await Task.Delay(1000);
            try
            {
                Save(page, saved);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                host.Log($"Saving the provider failed: {e}");
                page.Show(new CheckResult(false, $"The settings could not be saved: {e.Message}"));
                return;
            }
            page.Idle();
        });
    }

    private Func<Task<CheckResult>> OpenAiCheck(OpenAiConnectPage page)
    {
        string? url = Blank(page.Url), model = Blank(page.Model), key = Blank(page.ApiKey);
        return () => ProviderCheck.OpenAiAsync(host.OpenAiHttp, url, model, key, CancellationToken.None);
    }

    private Func<Task<CheckResult>> LocalCheck(LocalConnectPage page)
    {
        string? id = page.Chosen?.Id;
        return () => Task.FromResult(ProviderCheck.WebQwen(host.Files, host.Layout, host.Record, host.Manifest, id));
    }

    private void Save(ConnectPage page, Action<ConnectPage> saved)
    {
        var changes = new Dictionary<string, string?> { [Settings.VERONICA_PROVIDER] = Providers.SettingsValue(page.Kind) };
        switch (page)
        {
            case ClaudeConnectPage claude:
                changes[Settings.VERONICA_CLAUDE_MODEL] = Blank(claude.Model);
                changes[Settings.VERONICA_CLAUDE_EFFORT] = Blank(claude.Effort);
                break;
            case OpenAiConnectPage openAi:
                changes[Settings.VERONICA_OPENAI_URL] = Blank(openAi.Url);
                changes[Settings.VERONICA_OPENAI_API_KEY] = Blank(openAi.ApiKey);
                changes[Settings.VERONICA_OPENAI_MODEL] = Blank(openAi.Model);
                changes[Settings.VERONICA_OPENAI_EFFORT] = Blank(openAi.Effort);
                break;
            case LocalConnectPage local:
                changes[Settings.VERONICA_MODEL] = local.Chosen?.Id;
                break;
        }
        host.SaveSettings(changes);
        host.Log($"Saved provider {changes[Settings.VERONICA_PROVIDER]}");
        saved(page);
    }

    private void LoadModels(LocalConnectPage page)
    {
        if (!_filesReady)
            return;
        string? active = host.Settings.GetTrimmed(Settings.VERONICA_MODEL);
        var record = host.Record;
        var cards = host.Manifest.Select(m => new ModelCard(m,
            installed: record.Models.TryGetValue(m.Id, out var pin) && pin == m.Pin && host.Files.DirectoryExists(host.Layout.ModelFolder(m.Id)),
            choose: card => ChooseModel(page, card),
            delete: card => DeleteModel(page, card))).ToList();
        page.SetModels(cards);
        // Someone who already downloaded a model has read the disclaimer.
        if (cards.Any(c => c.Installed))
            page.Understood = true;
        page.Choose(cards.FirstOrDefault(c => c.Installed && c.Id == active));
    }

    private void ChooseModel(LocalConnectPage page, ModelCard card)
    {
        if (!page.CardsEnabled)
            return;
        if (!card.Installed && _updating)
        {
            card.Activity.Error = "Wait until the update finishes.";
            return;
        }
        page.Choose(card);
        if (!card.Installed)
            DownloadModel(page, card);
    }

    private void DownloadModel(LocalConnectPage page, ModelCard card)
    {
        page.Busy = true;
        _modelOps++;
        var activity = card.Activity;
        activity.Begin("Starting the download...");
        _ = Run(async () =>
        {
            try
            {
                await DownloadModelAsync(card.Model, Throttled.Progress(p => activity.Report(p, "Downloading...")), activity.Cancellation.Token);
                card.Installed = true;
                activity.End();
            }
            catch (OperationCanceledException) when (activity.Cancellation.IsCancellationRequested)
            {
                host.Log($"Cancelled the download of {card.Id}");
                activity.End();
            }
            catch (Exception e)
            {
                host.Log($"Downloading {card.Id} failed: {e}");
                activity.End();
                activity.Error = e.Message;
            }
            finally
            {
                page.Busy = false;
                _modelOps--;
            }
        });
    }

    private async Task DownloadModelAsync(ModelRecord model, IProgress<DownloadProgress> progress, CancellationToken token)
    {
        await new ModelDownloader(host.Http, host.Files, host.Layout).DownloadAsync(model, progress, token);
        host.Record.WithModel(model.Id, model.Pin).Save(host.Files, host.Layout);
        host.Log($"Downloaded {model.Id}");
    }

    // The Editor may be running this model; the record goes first so a half-deleted folder is not taken as installed.
    private void DeleteModel(LocalConnectPage page, ModelCard card)
    {
        if (page.Busy)
            return;
        if (_updating)
        {
            card.Activity.Error = "Wait until the update finishes.";
            return;
        }
        page.Busy = true;
        _modelOps++;
        var activity = card.Activity;
        activity.Begin("Deleting...");
        _ = Run(async () =>
        {
            try
            {
                await WaitForEditorAsync(activity, EditorWaitModel, activity.Cancellation.Token);
                activity.Status = "Deleting...";
                host.Record.WithModel(card.Id, null).Save(host.Files, host.Layout);
                card.Installed = false;
                if (host.Settings.GetTrimmed(Settings.VERONICA_MODEL) == card.Id)
                    host.SaveSettings(new Dictionary<string, string?> { [Settings.VERONICA_MODEL] = null });
                if (card.Chosen)
                    page.Choose(null);
                host.Files.DeleteDirectory(host.Layout.ModelFolder(card.Id));
                host.Log($"Deleted {card.Id}");
                activity.End();
            }
            catch (OperationCanceledException) when (activity.Cancellation.IsCancellationRequested)
            {
                activity.End();
            }
            catch (Exception e)
            {
                host.Log($"Deleting {card.Id} failed: {e}");
                activity.End();
                activity.Error = e.Message;
            }
            finally
            {
                page.Busy = false;
                _modelOps--;
            }
        });
    }

    private async Task WaitForEditorAsync(Activity activity, string reason, CancellationToken token)
    {
        if (!host.Editor.IsRunning())
            return;
        activity.Waiting = reason;
        try
        {
            while (host.Editor.IsRunning())
                await Task.Delay(1000, token);
        }
        finally
        {
            activity.Waiting = "";
        }
    }

    // The installer awaits its callback with ConfigureAwait(false); what the callback shows must run on the UI thread.
    private static Func<Task> OnUi(Func<Task> work) => () => Dispatcher.UIThread.InvokeAsync(work);

    // Work started from the UI thread; its continuations come back to it.
    private static async Task Run(Func<Task> work) => await work();

    private void ShowApp(AppTab tab) => Safe(() =>
    {
        _filesReady = true;
        if (_app == null)
            _app = new AppShell(new ModsPane(() => _ = PickEditorExeAsync(), () => _ = PickGameExeAsync()), BrainSlides(), new UpdatesPane(RunUpdate, CheckLatest));
        RefreshMods();
        RefreshUpdates();
        _app.Tab = tab;
        Page = _app;
        CheckLatest();
    });

    private Slides BrainSlides()
    {
        var current = Providers.FromSettings(host.Settings.GetTrimmed(Settings.VERONICA_PROVIDER));
        var connect = new ConnectSlide();
        _brainConnect = connect;
        Slides slides = null!;
        ChooseSlide choose = null!;
        Action<ConnectPage> saved = page =>
        {
            choose.Select(page.Kind);
            page.Saved = SavedText;
        };
        choose = new ChooseSlide(kind =>
        {
            if (connect.Content?.Kind != kind)
                connect.Content = Connect(kind, "Use this brain", () => slides.GoTo(0), saved);
            slides.Open(1);
        }, current);
        connect.Content = Connect(current, "Use this brain", () => slides.GoTo(0), saved);
        slides = new Slides(choose, connect);
        slides.Open(1);
        return slides;
    }

    private void RefreshMods()
    {
        var pane = _app!.Mods;
        // A card still waiting for the Editor keeps its own state, Cancel included.
        var running = pane.Mods.Where(m => m.Activity.Running).ToDictionary(m => m.Folder, StringComparer.OrdinalIgnoreCase);
        string? editor = FindEditorExe();
        pane.Editor = editor ?? "";
        pane.Game = FindGameExe(editor) ?? "";
        var mods = ReadMods(pane).Select(m => running.GetValueOrDefault(m.Folder) ?? m).ToList();
        pane.Mods.Clear();
        foreach (var mod in mods)
            pane.Mods.Add(mod);
    }

    private List<ModCard> ReadMods(ModsPane pane)
    {
        var mods = new List<ModCard>();
        IReadOnlyList<ModIndexEntry> entries;
        try
        {
            if (!host.Files.FileExists(ModIndex.DefaultPath))
            {
                pane.Message = "No local mods found. Create or open a mod in the Editor and it shows up here.";
                return mods;
            }
            entries = ModIndex.Parse(host.Files.ReadAllText(ModIndex.DefaultPath));
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or FormatException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            host.Log($"Reading {ModIndex.DefaultPath} failed: {e}");
            pane.Message = $"The Editor's list of local mods cannot be read ({e.Message}).";
            return mods;
        }
        pane.Message = entries.Count == 0 ? "No local mods yet. Create one in the Editor and it shows up here." : "";
        foreach (var entry in entries)
        {
            string name = Path.GetFileName(entry.Folder.TrimEnd('\\', '/'));
            try
            {
                string? project = host.Files.Files(entry.Folder, recursive: false)
                    .Where(f => f.EndsWith(".vrgproj", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                var state = HookState.NotHooked;
                if (project != null)
                {
                    string text = host.Files.ReadAllText(project);
                    name = VrgProj.Name(text) ?? name;
                    state = VrgProj.State(text, host.Layout.Runtime);
                }
                mods.Add(new ModCard(name, entry.Folder, project, state, mod => _ = StartEditorAsync(mod), (mod, hook) => _ = ChangeModAsync(mod, hook)));
            }
            catch (Exception e) when (e is System.Text.Json.JsonException or FormatException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                host.Log($"Reading mod {entry.Folder} failed: {e}");
                mods.Add(new ModCard(name, entry.Folder, null, HookState.NotHooked, _ => { }, (_, _) => { }) { Note = $"Cannot be read: {e.Message}" });
            }
        }
        return mods;
    }

    // Returns whether the .vrgproj was changed; the card is replaced by a fresh one either way.
    private async Task<bool> ChangeModAsync(ModCard mod, bool hook)
    {
        if (mod.Project is not { } project || mod.Activity.Running)
            return false;
        var activity = mod.Activity;
        activity.Begin(hook ? "Adding Veronica to this mod..." : "Removing Veronica from this mod...");
        mod.Touch();
        string? error = null;
        bool changed = false;
        try
        {
            await WaitForEditorAsync(activity, EditorWaitMod, activity.Cancellation.Token);
            string text = host.Files.ReadAllText(project);
            host.Files.WriteAllText(project, hook ? VrgProj.Hook(text, host.Layout.Runtime)
                : VrgProj.UnhookAny(text, host.Layout.Runtime, path => HoldsVeronica(project, path)));
            host.Log($"{(hook ? "Hooked" : "Unhooked")} {project}");
            changed = true;
        }
        catch (OperationCanceledException) when (activity.Cancellation.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            host.Log($"{(hook ? "Hooking" : "Unhooking")} {project} failed: {e}");
            error = e.Message;
        }
        activity.End();
        Safe(RefreshMods);
        if (error != null)
            ShowModError(mod, error);
        return changed;
    }

    private bool HoldsVeronica(string project, string searchPath)
    {
        try
        {
            string folder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, searchPath));
            return host.Files.FileExists(Path.Combine(folder, VrgProj.Assembly));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private void ShowModError(ModCard mod, string error)
    {
        var card = _app!.Mods.Mods.FirstOrDefault(m => string.Equals(m.Folder, mod.Folder, StringComparison.OrdinalIgnoreCase)) ?? mod;
        card.Activity.Error = error;
    }

    // A mod without Veronica gets it first, so the Editor opens with the chat.
    private async Task StartEditorAsync(ModCard mod)
    {
        if (mod.Project is not { } project || mod.Activity.Running)
            return;
        mod.Activity.Error = "";
        if (await EditorExeAsync() is not { } exe)
            return;
        if (EditorLaunch.Find(exe, host.Files.DirectoryExists, host.Files.FileExists) is not { } layout)
        {
            ShowModError(mod, $"{exe} is neither in the Space Engineers 2 Mod SDK nor in a development checkout, so the Editor would not find the game's projects.");
            return;
        }
        string? game = FindGameExe(exe);
        if (_app != null)
            _app.Mods.Game = game ?? "";
        if (mod.State == HookState.NotHooked && !await ChangeModAsync(mod, hook: true))
            return;
        var info = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = EditorLaunch.WorkingDirectory(exe, game) };
        foreach (var argument in EditorLaunch.Arguments(project, exe, layout, game))
            info.ArgumentList.Add(argument);
        try
        {
            System.Diagnostics.Process.Start(info)?.Dispose();
            host.Log($"Started {exe} on {project}{(game == null ? " without the game" : $" with {game}")}");
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException)
        {
            host.Log($"Starting {exe} failed: {e}");
            ShowModError(mod, $"The Editor could not be started: {e.Message}");
        }
    }

    private async Task<string?> EditorExeAsync() => FindEditorExe() ?? await PickEditorExeAsync();

    // The saved Editor; when none is saved or the saved one is gone, the Mod SDK from Steam, saved once found.
    private string? FindEditorExe()
    {
        if (host.Settings.GetTrimmed(Settings.VERONICA_EDITOR_EXE) is { } saved && host.Files.FileExists(saved))
            return saved;
        if (Steam.EditorExe(host.Files, host.SteamRoot()) is not { } found)
            return null;
        SaveFound(Settings.VERONICA_EDITOR_EXE, found, "Editor");
        return found;
    }

    // The saved game; otherwise the one that belongs to the Editor, saved once found: the Steam game beside the
    // Mod SDK's Editor, the checkout's own build beside a development Editor.
    private string? FindGameExe(string? editorExe)
    {
        if (host.Settings.GetTrimmed(Settings.VERONICA_GAME_EXE) is { } saved && host.Files.FileExists(saved))
            return saved;
        if (editorExe == null || EditorLaunch.Find(editorExe, host.Files.DirectoryExists, host.Files.FileExists) is not { } layout)
            return null;
        if ((layout.FromSdk ? Steam.GameExe(host.Files, host.SteamRoot()) : layout.DevGameExe) is not { } found)
            return null;
        SaveFound(Settings.VERONICA_GAME_EXE, found, "game");
        return found;
    }

    private void SaveFound(string key, string? path, string what)
    {
        try
        {
            host.SaveSettings(new Dictionary<string, string?> { [key] = path });
            host.Log(path == null ? $"The {what} path is cleared" : $"The {what} is {path}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            host.Log($"Saving the {what} path failed: {e}");
        }
    }

    // The saved game was found for the previous Editor, so it is looked up again for the new one.
    private async Task<string?> PickEditorExeAsync()
    {
        if (await PickEditor() is not { Length: > 0 } exe)
            return null;
        SaveFound(Settings.VERONICA_EDITOR_EXE, exe, "Editor");
        SaveFound(Settings.VERONICA_GAME_EXE, null, "game");
        if (_app != null)
        {
            _app.Mods.Editor = exe;
            _app.Mods.Game = FindGameExe(exe) ?? "";
        }
        return exe;
    }

    private async Task PickGameExeAsync()
    {
        if (await PickGame() is not { Length: > 0 } exe)
            return;
        SaveFound(Settings.VERONICA_GAME_EXE, exe, "game");
        if (_app != null)
            _app.Mods.Game = exe;
    }

    private void RefreshUpdates()
    {
        var pane = _app!.Updates;
        var record = host.Record;
        pane.Warning = host.RecordWarning ?? "";
        pane.Installed = record.Version is { } v ? $"Installed: Veronica {v}" : "Not installed yet.";
        pane.Conflicts.Clear();
        pane.ConflictsMessage = "";
        foreach (var conflict in ContentMerge.Conflicts(host.Files, host.Layout, record))
            pane.Conflicts.Add(new ConflictCard(conflict, ContentMerge.Relative(host.Layout, conflict.Mine), KeepMine, UseNew));
    }

    private void CheckLatest()
    {
        var pane = _app!.Updates;
        pane.Latest = "Looking for a newer version...";
        pane.UpToDate = false;
        pane.CanUpdate = false;
        _ = Task.Run(async () =>
        {
            string text;
            bool newer = false;
            try
            {
                var release = await ReleaseFeed.LatestAsync(host.Http, host.ReleasesUrl, CancellationToken.None);
                var installed = host.Record.Version;
                newer = installed == null || release.Version > installed.Value;
                text = newer ? $"Veronica {release.Version} is ready to install." : "You're up to date.";
            }
            catch (Exception e)
            {
                text = $"Cannot reach the releases: {e.Message}";
            }
            Dispatcher.UIThread.Post(() =>
            {
                pane.Latest = text;
                pane.CanUpdate = newer;
                pane.UpToDate = !newer && !text.StartsWith("Cannot", StringComparison.Ordinal);
            });
        });
    }

    // Self-update first, then Runtime, Skills and Wiki, then models whose pin changed.
    private void RunUpdate()
    {
        if (_updating || _app == null)
            return;
        if (_modelOps > 0)
        {
            _app.Updates.Activity.Error = "Wait until the model download or delete on the Brain tab finishes.";
            return;
        }
        _updating = true;
        var pane = _app.Updates;
        var activity = pane.Activity;
        activity.Begin("Looking for the latest release...");
        _ = Run(async () =>
        {
            try
            {
                var token = activity.Cancellation.Token;
                var release = await ReleaseFeed.LatestAsync(host.Http, host.ReleasesUrl, token);
                host.Log($"Latest release {release.Tag} from {host.ReleasesUrl}");
                // A release whose exe carries a lower version than its tag would otherwise update forever.
                if (!_selfUpdated && SelfUpdate.IsNeeded(release, host.Version))
                {
                    activity.Status = $"Updating Veronica.exe to {release.Version}...";
                    var update = new SelfUpdate(host.Http, host.Files, host.Layout);
                    string download = await update.DownloadAsync(release, token);
                    var restart = host.PrepareRestart(host.Layout.Exe, [Program.SelfUpdatedArgument, Program.UpdateArgument]);
                    host.Log($"Replacing {host.RunningExe} with {release.Version}, restarting");
                    update.Swap(host.RunningExe, download);
                    restart();
                    return;
                }
                var result = await new ReleaseInstaller(host.Http, host.Files, host.Layout).InstallAsync(release,
                    Throttled.Progress(p => activity.Report(p, "Downloading...")), OnUi(async () =>
                    {
                        await WaitForEditorAsync(activity, EditorWaitFiles, token);
                        activity.Status = "Replacing files...";
                    }), token);
                host.Log($"Installed {result.Version}, {result.Conflicts.Count} conflicts");
                foreach (var model in result.ModelsToReplace)
                    await DownloadModelAsync(model, Throttled.Progress(p => activity.Report(p, $"Updating {model.Id}...")), token);
                activity.End();
                Safe(() =>
                {
                    RefreshUpdates();
                    RefreshMods();
                    if (_brainConnect?.Content is LocalConnectPage { Busy: false } local)
                        LoadModels(local);
                });
                CheckLatest();
            }
            catch (OperationCanceledException) when (activity.Cancellation.IsCancellationRequested)
            {
                host.Log("Update cancelled");
                activity.End();
            }
            catch (Exception e)
            {
                host.Log($"Update failed: {e}");
                activity.End();
                activity.Error = e.Message;
            }
            finally
            {
                _updating = false;
            }
        });
    }

    private void KeepMine(ConflictCard card) => Resolve(card, ContentMerge.KeepMine);

    private void UseNew(ConflictCard card) => Resolve(card, ContentMerge.UseNew);

    private void Resolve(ConflictCard card, Action<IFiles, InstallLayout, MergeConflict> resolve)
    {
        var pane = _app!.Updates;
        try
        {
            resolve(host.Files, host.Layout, card.Conflict);
            pane.Conflicts.Remove(card);
            pane.ConflictsMessage = "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            host.Log($"Resolving {card.Name} failed: {e}");
            pane.ConflictsMessage = $"{card.Name}: {e.Message} Close it where it is open and try again.";
        }
    }
}
