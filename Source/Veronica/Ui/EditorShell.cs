using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.UI.UniDock.Docking;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Tools;

namespace Veronica.Ui;

public sealed class EditorShell
{
    private const string PreferredDockGroup = "MainRightDockGroup";

    private ChatPanelViewModel? _panel;
    private bool _templatesRegistered;
    private bool _menuAdded;

    public void Attach()
    {
        if (MainWindow is not { } window)
        {
            Log.Default.WriteLine("[Veronica] Editor main window not found, panel not shown.");
            return;
        }

        RegisterTemplates();
        AddMenu(window);
        ShowPanel();
    }

    private static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private void RegisterTemplates()
    {
        if (_templatesRegistered || Application.Current is not { } app)
            return;

        var templates = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri("avares://Veronica/Ui/Templates.axaml"));
        app.Resources.MergedDictionaries.Add(templates);
        _templatesRegistered = true;
    }

    private void AddMenu(Window window)
    {
        if (_menuAdded)
            return;

        if (window.FindControl<Menu>("PART_MainMenu") is not { } menu)
        {
            Log.Default.WriteLine("[Veronica] Editor main menu not found, no Veronica menu.");
            return;
        }

        var show = new MenuItem { Header = "Show Veronica" };
        show.Click += (_, _) => ShowPanel();
        menu.Items.Add(new MenuItem { Header = "Veronica", Items = { show } });
        _menuAdded = true;
    }

    private void ShowPanel()
    {
        if (MainWindow?.FindResource("TheDockManager") is not DockManager dock)
        {
            Log.Default.WriteLine("[Veronica] Editor dock manager not found, panel not shown.");
            return;
        }

        _panel ??= ChatPanelViewModel.Current = CreatePanel();

        if (!dock.ContainsViewModel(_panel))
        {
            if (PickDockGroup(dock) is not { } group)
            {
                Log.Default.WriteLine("[Veronica] No dock group in the current layout, panel not shown.");
                return;
            }

            _panel.DefaultDockGroupId = group;
            if (!dock.AddViewModel(_panel))
            {
                Log.Default.WriteLine("[Veronica] Editor dock manager has no panel list, panel not shown.");
                return;
            }
        }

        _panel.Select();
    }

    private static readonly SkillShelf Skills = new();

    private static readonly SkillPlan Plan = new();

    private static readonly ITool[] Tools =
    [
        new SkillTool(() => Skills.Current, Plan),
        new ActivateSkillTool(() => Skills.Current, Plan),
        new CompletePlanItemTool(Plan),
        new SkipPlanItemTool(Plan),
        new FindContentTool(),
        new ListFolderTool(),
        new ReadTool(),
        new WriteTool(),
        new ElementAddTool(),
        new ElementRemoveTool(),
        new CreateFolderTool(),
        new FindTemplateTool(),
        new CreatePrefabTool(),
        new CreatePartialTool(),
        new CreateDefinitionTool(),
        new ShowTool(),
    ];

    private static ChatPanelViewModel CreatePanel()
    {
        ChatPanelViewModel? panel = null;
        var tools = new ToolSet(Tools);
        var provider = ModelProvider.Create(status => panel?.ShowStatus(status), tools);
        panel = new ChatPanelViewModel(provider.OpenSession(), tools, Plan, Skills.SystemPrompt);
        EvalHook.Attach(panel, Skills.SystemPrompt(), tools.Definitions);
        return panel;
    }

    private static string? PickDockGroup(DockManager dock)
    {
        if (dock.ConnectedGroups.Any(g => g.DockId == PreferredDockGroup))
            return PreferredDockGroup;

        var fallback = dock.ConnectedGroups.OfType<TabbedDockGroup>().FirstOrDefault()?.DockId;
        if (fallback != null)
            Log.Default.WriteLine($"[Veronica] Dock group {PreferredDockGroup} not in the current layout, using {fallback}.");
        return fallback;
    }
}
