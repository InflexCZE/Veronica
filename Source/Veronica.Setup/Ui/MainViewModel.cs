using Avalonia.Threading;
using Veronica.Agent;
using Veronica.Setup.Core;

namespace Veronica.Setup.Ui;

// The window's flow (Docs/Tech/Distribution.md): first run is Welcome, provider selection, the provider's
// setup and its check; afterwards app mode manages the provider, the mods and the versions.
public sealed class MainViewModel(SetupHost host, Action shutdown) : ViewModel
{
    private object? _page;
    private AppPage? _app;
    private bool _selfUpdated;
    private bool _busy;

    public object? Page { get => _page; private set => Set(ref _page, value); }

    public string Title => host.Version is { } v ? $"Veronica {v}" : "Veronica";

    private bool FirstRun => !host.Files.FileExists(host.Layout.SettingsFile) || host.Record.Version == null;

    public void Start(bool update, bool selfUpdated)
    {
        _selfUpdated = selfUpdated;
        Safe(() =>
        {
            new SelfUpdate(host.Http, host.Files, host.Layout).CleanUp(host.RunningExe);
            host.Log($"Started {host.RunningExe} {host.Version}");
            if (FirstRun)
            {
                if (TakeOwnName())
                    return;
                ShowWelcome();
                return;
            }
            ShowApp();
            if (update)
                RunUpdate(firstRun: false);
        });
    }

    // Another start of Veronica.exe asked this one to update (the panel's button while this window was open).
    public void RequestUpdate()
    {
        if (!_busy && !FirstRun)
            RunUpdate(firstRun: false);
    }

    // A browser may save the download as "Veronica (1).exe"; the panel and self-update look for Veronica.exe.
    // The running exe cannot be renamed safely, so a copy takes over and deletes the original.
    private bool TakeOwnName()
    {
        if (string.Equals(Path.GetFileName(host.RunningExe), Release.ExeAsset, StringComparison.OrdinalIgnoreCase) || host.Files.FileExists(host.Layout.Exe))
            return false;
        try
        {
            File.Copy(host.RunningExe, host.Layout.Exe, overwrite: false);
            host.Log($"Copied {host.RunningExe} to {host.Layout.Exe}, restarting from it");
            host.PrepareRestart(host.Layout.Exe, [Program.ReplacesArgument, host.RunningExe])();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            host.Log($"Taking the name {Release.ExeAsset} failed: {e.Message}");
            return false;
        }
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
            var page = new ProgressPage("Something went wrong", () => Start(update: false, _selfUpdated), shutdown) { Error = e.Message };
            Page = page;
        }
    }

    private void ShowWelcome()
    {
        var foreign = InstallLayout.ForeignEntries(host.Files.Entries(host.Layout.Root), Path.GetFileName(host.RunningExe));
        string warning = foreign.Count == 0 ? ""
            : $"This folder also holds {string.Join(", ", foreign.Take(5))}{(foreign.Count > 5 ? ", ..." : "")}. Veronica installs its folders right here, beside them. To keep it apart, close this window, move Veronica.exe into an empty folder and start it there.";
        Page = new WelcomePage(host.Layout.Root, warning, () => RunUpdate(firstRun: true));
    }

    // Install and update are one path: self-update first, then Runtime, Skills and Wiki, then models whose pin changed.
    private void RunUpdate(bool firstRun)
    {
        var progress = new ProgressPage(firstRun ? "Installing Veronica" : "Updating Veronica", () => RunUpdate(firstRun), () => { if (firstRun) ShowWelcome(); else ShowApp(); });
        Page = progress;
        _ = Guard(progress, async token =>
        {
            progress.Status = "Looking for the latest release...";
            var release = await ReleaseFeed.LatestAsync(host.Http, host.ReleasesUrl, token);
            host.Log($"Latest release {release.Tag} from {host.ReleasesUrl}");
            // A release whose exe carries a lower version than its tag would otherwise update forever.
            if (!_selfUpdated && SelfUpdate.IsNeeded(release, host.Version))
            {
                progress.Status = $"Updating Veronica.exe to {release.Version}...";
                var update = new SelfUpdate(host.Http, host.Files, host.Layout);
                string download = await update.DownloadAsync(release, token);
                var restart = host.PrepareRestart(host.Layout.Exe, firstRun ? [Program.SelfUpdatedArgument] : [Program.SelfUpdatedArgument, Program.UpdateArgument]);
                host.Log($"Replacing {host.RunningExe} with {release.Version}, restarting");
                update.Swap(host.RunningExe, download);
                restart();
                return;
            }

            var result = await new ReleaseInstaller(host.Http, host.Files, host.Layout).InstallAsync(release, Throttled.Progress(progress.Report), async () =>
            {
                await WaitForEditorAsync("Close the Editor so Veronica can replace its files.", progress);
                Page = progress;
                progress.Status = "Replacing Veronica's files...";
            }, token);
            host.Log($"Installed {result.Version}, {result.Conflicts.Count} conflicts");
            foreach (var model in result.ModelsToReplace)
            {
                progress.Status = $"Replacing {model.Id} with its new revision...";
                await DownloadModelAsync(model, progress, token);
            }
            if (firstRun)
                ShowProviderSelect(back: null);
            else
                ShowApp();
        });
    }

    private async Task DownloadModelAsync(ModelRecord model, ProgressPage progress, CancellationToken token)
    {
        await new ModelDownloader(host.Http, host.Files, host.Layout).DownloadAsync(model, Throttled.Progress(progress.Report), token);
        host.Record.WithModel(model.Id, model.Pin).Save(host.Files, host.Layout);
        host.Log($"Downloaded {model.Id}");
    }

    // Cancel goes back; a partial model download stays in Downloads and resumes next time.
    private async Task Guard(ProgressPage progress, Func<CancellationToken, Task> work)
    {
        _busy = true;
        try
        {
            await work(progress.Cancellation.Token);
        }
        catch (OperationCanceledException) when (progress.Cancellation.IsCancellationRequested)
        {
            host.Log($"Cancelled: {progress.Title}");
            progress.Back.Execute(null);
        }
        catch (Exception e)
        {
            host.Log($"Failed: {e}");
            progress.Error = e.Message;
            Page = progress;
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task WaitForEditorAsync(string reason, ProgressPage progress)
    {
        if (!host.Editor.IsRunning())
            return;
        Page = new WaitEditorPage(reason, () => progress.Cancellation.Cancel());
        while (host.Editor.IsRunning())
            await Task.Delay(1000, progress.Cancellation.Token);
    }

    private void ShowProviderSelect(Action? back) =>
        Page = new ProviderSelectPage(ShowLocalModels, ShowClaude, ShowOpenAi, back);

    private void ShowLocalModels()
    {
        var settings = host.Settings;
        string? active = settings.GetTrimmed(Settings.VERONICA_MODEL);
        var record = host.Record;
        LocalModelsPage? page = null;
        var items = host.Manifest.Select(m => new ModelItem(m,
            installed: record.Models.TryGetValue(m.Id, out var pin) && pin == m.Pin && host.Files.DirectoryExists(host.Layout.ModelFolder(m.Id)),
            active: m.Id == active,
            download: item => DownloadModel(item, page!),
            delete: item => DeleteModel(item, page!),
            use: item => UseModel(item, page!))).ToList();
        page = new LocalModelsPage(items, () =>
        {
            if (!page!.Models.Any(m => m.Active && m.Installed))
            {
                page.Message = "Download a model and choose Use first.";
                return;
            }
            host.SaveSettings(new Dictionary<string, string?> { [Settings.VERONICA_PROVIDER] = "webqwen" });
            RunCheck(() => Task.FromResult(ProviderCheck.WebQwen(host.Files, host.Layout, host.Record, host.Manifest, host.Settings.GetTrimmed(Settings.VERONICA_MODEL))), ShowLocalModels);
        }, () => ShowProviderSelect(FirstRun ? null : ShowApp));
        Page = page;
    }

    private void DownloadModel(ModelItem item, LocalModelsPage page)
    {
        var progress = new ProgressPage($"Downloading {item.Name}", () => DownloadModel(item, page), () => Page = page);
        Page = progress;
        _ = Guard(progress, async token =>
        {
            await DownloadModelAsync(item.Model, progress, token);
            item.Installed = true;
            if (!page.Models.Any(m => m.Active))
                UseModel(item, page);
            Page = page;
        });
    }

    // The Editor may be running this model; the record goes first so a half-deleted folder is not taken as installed.
    private void DeleteModel(ModelItem item, LocalModelsPage page)
    {
        var progress = new ProgressPage($"Deleting {item.Name}", () => DeleteModel(item, page), () => Page = page);
        Page = progress;
        _ = Guard(progress, async _ =>
        {
            await WaitForEditorAsync("Close the Editor; it may be running this model.", progress);
            host.Record.WithModel(item.Name, null).Save(host.Files, host.Layout);
            item.Installed = false;
            if (item.Active)
            {
                item.Active = false;
                host.SaveSettings(new Dictionary<string, string?> { [Settings.VERONICA_MODEL] = null });
            }
            host.Files.DeleteDirectory(host.Layout.ModelFolder(item.Name));
            host.Log($"Deleted {item.Name}");
            page.Message = "";
            Page = page;
        });
    }

    private void UseModel(ModelItem item, LocalModelsPage page)
    {
        host.SaveSettings(new Dictionary<string, string?> { [Settings.VERONICA_MODEL] = item.Name });
        foreach (var m in page.Models)
            m.Active = m == item;
    }

    private void ShowClaude()
    {
        var settings = host.Settings;
        Page = new ClaudeSetupPage(settings.Get(Settings.VERONICA_CLAUDE_MODEL), settings.Get(Settings.VERONICA_CLAUDE_EFFORT), page =>
        {
            host.SaveSettings(new Dictionary<string, string?>
            {
                [Settings.VERONICA_PROVIDER] = "claude",
                [Settings.VERONICA_CLAUDE_MODEL] = page.Model,
                [Settings.VERONICA_CLAUDE_EFFORT] = page.Effort,
            });
            RunCheck(() => ProviderCheck.ClaudeAsync(CancellationToken.None), ShowClaude);
        }, () => ShowProviderSelect(FirstRun ? null : ShowApp));
    }

    private void ShowOpenAi()
    {
        var settings = host.Settings;
        Page = new OpenAiSetupPage(settings.Get(Settings.VERONICA_OPENAI_URL), settings.Get(Settings.VERONICA_OPENAI_MODEL),
            settings.Get(Settings.VERONICA_OPENAI_API_KEY), settings.Get(Settings.VERONICA_OPENAI_EFFORT), page =>
            {
                host.SaveSettings(new Dictionary<string, string?>
                {
                    [Settings.VERONICA_PROVIDER] = "openai",
                    [Settings.VERONICA_OPENAI_URL] = page.Url,
                    [Settings.VERONICA_OPENAI_MODEL] = page.Model,
                    [Settings.VERONICA_OPENAI_API_KEY] = page.ApiKey,
                    [Settings.VERONICA_OPENAI_EFFORT] = page.Effort,
                });
                var saved = host.Settings;
                RunCheck(() => ProviderCheck.OpenAiAsync(host.Http, saved.Get(Settings.VERONICA_OPENAI_URL), saved.Get(Settings.VERONICA_OPENAI_MODEL), saved.Get(Settings.VERONICA_OPENAI_API_KEY), CancellationToken.None), ShowOpenAi);
            }, () => ShowProviderSelect(FirstRun ? null : ShowApp));
    }

    private void RunCheck(Func<Task<CheckResult>> check, Action back)
    {
        var page = new CheckPage(ShowApp, () => RunCheck(check, back), back);
        Page = page;
        _ = Task.Run(async () =>
        {
            CheckResult result;
            try
            {
                result = await check();
            }
            catch (Exception e)
            {
                result = new CheckResult(false, e.Message);
            }
            host.Log($"Provider check: {result.Ok} {result.Message}");
            Dispatcher.UIThread.Post(() => page.Show(result));
        });
    }

    private void ShowApp() => Safe(() =>
    {
        _app ??= new AppPage(() => ShowProviderSelect(ShowApp), () => RunUpdate(firstRun: false), CheckLatest);
        RefreshApp();
        Page = _app;
        CheckLatest();
    });

    private void RefreshApp()
    {
        var app = _app!;
        var settings = host.Settings;
        var record = host.Record;
        app.Warning = host.RecordWarning ?? "";
        string provider = settings.GetTrimmed(Settings.VERONICA_PROVIDER) ?? "webqwen";
        app.Provider = provider.ToLowerInvariant() switch
        {
            "claude" => $"Claude Code, model {settings.GetTrimmed(Settings.VERONICA_CLAUDE_MODEL) ?? "sonnet"}",
            "openai" => $"OpenAI-compatible server {settings.GetTrimmed(Settings.VERONICA_OPENAI_URL)}, model {settings.GetTrimmed(Settings.VERONICA_OPENAI_MODEL) ?? "(first listed)"}",
            _ => $"Local model {settings.GetTrimmed(Settings.VERONICA_MODEL) ?? "(none chosen)"}",
        };
        app.VersionText = record.Version is { } v ? $"Installed: {v}" : "Not installed";

        app.Mods.Clear();
        foreach (var mod in ReadMods(app))
            app.Mods.Add(mod);

        app.Conflicts.Clear();
        app.ConflictsMessage = "";
        foreach (var conflict in ContentMerge.Conflicts(host.Files, host.Layout, record))
            app.Conflicts.Add(new ConflictItem(conflict, ContentMerge.Relative(host.Layout, conflict.Mine), KeepMine, UseNew));
    }

    private void CheckLatest()
    {
        var app = _app!;
        app.LatestText = "Looking for the latest release...";
        _ = Task.Run(async () =>
        {
            string text;
            bool newer = false;
            try
            {
                var release = await ReleaseFeed.LatestAsync(host.Http, host.ReleasesUrl, CancellationToken.None);
                var installed = host.Record.Version;
                newer = installed == null || release.Version > installed.Value;
                text = newer ? $"Veronica {release.Version} is available." : $"Latest: {release.Version}, up to date.";
            }
            catch (Exception e)
            {
                text = $"Could not reach the releases: {e.Message}";
            }
            Dispatcher.UIThread.Post(() =>
            {
                app.LatestText = text;
                app.CanUpdate = newer;
            });
        });
    }

    private List<ModItem> ReadMods(AppPage app)
    {
        var mods = new List<ModItem>();
        IReadOnlyList<ModIndexEntry> entries;
        try
        {
            if (!host.Files.FileExists(ModIndex.DefaultPath))
            {
                app.ModsMessage = "No local mods found: the mod HUB's index does not exist yet.";
                return mods;
            }
            entries = ModIndex.Parse(host.Files.ReadAllText(ModIndex.DefaultPath));
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or FormatException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            host.Log($"Reading {ModIndex.DefaultPath} failed: {e}");
            app.ModsMessage = $"Mods unavailable: the mod HUB's index could not be read ({e.Message}).";
            return mods;
        }
        app.ModsMessage = "";
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
                mods.Add(new ModItem(name, entry.Folder, project, state, ToggleMod));
            }
            catch (Exception e) when (e is System.Text.Json.JsonException or FormatException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                host.Log($"Reading mod {entry.Folder} failed: {e}");
                mods.Add(new ModItem(name, entry.Folder, null, HookState.NotHooked, ToggleMod) { Note = $"cannot be read: {e.Message}" });
            }
        }
        return mods;
    }

    private void ToggleMod(ModItem mod)
    {
        bool hook = !mod.IsHooked;
        var progress = new ProgressPage("Updating the mod", () => ToggleMod(mod), ShowApp);
        Page = progress;
        _ = Guard(progress, async _ =>
        {
            await WaitForEditorAsync("Close the Editor so it does not write the mod's project back over Veronica's change.", progress);
            string text = host.Files.ReadAllText(mod.Project!);
            host.Files.WriteAllText(mod.Project!, hook ? VrgProj.Hook(text, host.Layout.Runtime) : VrgProj.Unhook(text, host.Layout.Runtime));
            host.Log($"{(hook ? "Hooked" : "Unhooked")} {mod.Project}");
            ShowApp();
        });
    }

    private void KeepMine(ConflictItem item) => Resolve(item, ContentMerge.KeepMine);

    private void UseNew(ConflictItem item) => Resolve(item, ContentMerge.UseNew);

    private void Resolve(ConflictItem item, Action<IFiles, InstallLayout, MergeConflict> resolve)
    {
        try
        {
            resolve(host.Files, host.Layout, item.Conflict);
            _app!.Conflicts.Remove(item);
            _app.ConflictsMessage = "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            host.Log($"Resolving {item.Name} failed: {e}");
            _app!.ConflictsMessage = $"{item.Name}: {e.Message} Close it where it is open and try again.";
        }
    }
}
