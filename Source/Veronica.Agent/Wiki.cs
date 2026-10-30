using System.Text;
using System.Text.Json;
using Veronica.Agent.Tools;

namespace Veronica.Agent;

// Source is provenance for whoever edits the page; the model never sees it.
public sealed record WikiPage(string Name, string Title, string Source, string Description, string Body);

public sealed record WikiLoad(IReadOnlyList<WikiPage> Pages, IReadOnlyList<string> Problems);

public static class WikiFiles
{
    public static WikiLoad Load(string? directory)
    {
        var (pages, problems) = Frontmatter.LoadAll(directory, Parse, p => p.Name, "page");
        return new WikiLoad(pages, problems);
    }

    public static WikiPage Parse(string text)
    {
        var (fields, body) = Frontmatter.Parse(text, "name", "title", "source", "description");
        return new WikiPage(fields["name"], fields["title"], fields["source"], fields["description"], body);
    }

    public static string Index(IReadOnlyList<WikiPage> pages)
    {
        var index = new StringBuilder();
        foreach (var page in pages)
            index.Append("\n- ").Append(page.Name).Append(": ").Append(page.Description);
        return index.ToString();
    }
}

public sealed class WikiPageTool(Func<IReadOnlyList<WikiPage>> pages) : ITool
{
    public const string ToolName = "wiki_page";

    public string Name => ToolName;

    public string Description => "Returns a wiki page from the system prompt's page list.";

    public string Parameters => """
        {"type":"object","properties":{"name":{"type":"string","description":"Page name, e.g. gscreen"}},"required":["name"]}
        """;

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var all = pages();
        var name = ToolArgs.Text(arguments, "name");
        var page = all.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (page == null)
        {
            var names = all.Count == 0 ? "there are none" : "the pages are: " + string.Join(", ", all.Select(p => p.Name));
            return Task.FromResult(new ToolResult($"There is no wiki page named '{name}'; {names}."));
        }
        return Task.FromResult(new ToolResult($"# {page.Title}\n\n{page.Body}"));
    }
}
