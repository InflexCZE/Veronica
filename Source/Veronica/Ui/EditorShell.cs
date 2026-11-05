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

    internal static Window? MainWindow =>
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

    private static readonly WikiTool Wiki = new(() => Skills.Pages, () => Skills.Current,
        [new FindContentTool(), new FindReferencesTool(), new ListFolderTool(), new FindTemplateTool(), new ReadTool(), new ContainsTool(), new DescribeTool()], Plan);

    private static readonly ITool[] Tools =
    [
        Wiki,
        new SkillTool(() => Skills.Current, Plan),
        new ActivateSkillTool(() => Skills.Current, Plan),
        new MarkCompletedTool(Plan),
        new MarkSkippedTool(Plan),
        new WaitForUserTool(),
        new FindContentTool(),
        new FindReferencesTool(),
        new ListFolderTool(),
        new ReadTool(),
        new ContainsTool(),
        new DescribeTool(),
        new PlanHintedTool(new WriteTool(), Plan),
        new PlanHintedTool(new ElementAddTool(), Plan),
        new PlanHintedTool(new ElementRemoveTool(), Plan),
        new PlanHintedTool(new ComponentAddTool(), Plan),
        new PlanHintedTool(new ComponentRemoveTool(), Plan),
        new PlanHintedTool(new CreateFolderTool(), Plan),
        new FindTemplateTool(),
        new PlanHintedTool(new CreatePrefabTool(), Plan),
        new PlanHintedTool(new CreatePartialTool(), Plan),
        new PlanHintedTool(new CreateDefinitionTool(), Plan),
        new ShowTool(),
        new PresentGraphTool(),
    ];

    private static ChatPanelViewModel CreatePanel()
    {
        ChatPanelViewModel? panel = null;
        var questions = new QuestionBoard();
        // The Claude provider's turn runs in its own loop, which only a blocking call keeps waiting for the answers.
        var tools = new ToolSet([.. Tools, new AskUserTool(questions, waitsForAnswers: ModelProvider.NeedsEndpoint)]);
        // The researcher reads the chat's transcript, so an MCP client has nothing to ask it about.
        var endpoint = ToolEndpoint.Create(tools.Without(WikiTool.ToolName).With(new AskUserTool(questions, waitsForAnswers: true)));
        var provider = EvalHook.Record(ModelProvider.Create(status => panel?.ShowStatus(status), endpoint));
        panel = new ChatPanelViewModel(provider, tools, Plan, Skills.SystemPrompt, Wiki, new Compaction(Plan, () => Skills.Current), questions);
        EvalHook.Attach(panel);
        ToolEndpoint.Start(endpoint, ModelProvider.NeedsEndpoint, panel.ShowStatus);
        return panel;
    }

    internal static string? PickDockGroup(DockManager dock)
    {
        if (dock.ConnectedGroups.Any(g => g.DockId == PreferredDockGroup))
            return PreferredDockGroup;

        var fallback = dock.ConnectedGroups.OfType<TabbedDockGroup>().FirstOrDefault()?.DockId;
        if (fallback != null)
            Log.Default.WriteLine($"[Veronica] Dock group {PreferredDockGroup} not in the current layout, using {fallback}.");
        return fallback;
    }
}
