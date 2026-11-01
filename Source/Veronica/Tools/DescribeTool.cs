using System.Text.Json;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.DCS.Components;
using Keen.VRage.DCS.Internal;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Serialization;
using Keen.VRage.UI.Shared.ObjectInspector;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class DescribeTool : ITool
{
    public string Name => "describe";

    public string Description =>
        "Explains a type from the game's own documentation: what it and its members are for, its base and derived types, the component or definition it pairs with, and units. " +
        "Without member: the type's summary and its member list. With member: that member in full, then the members of its type.";

    public string Parameters => """
        {"type":"object","properties":{"subject":{"type":"string","description":"A type name, e.g. ThrusterDefinition or ThrusterComponent, or a content item: its project path, or its file name if unique"},"member":{"type":"string","description":"Field path as read prints it, e.g. ThrustPower or Dummies[0].Transform; empty for the whole type"}},"required":["subject"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string subject = Args.Text(arguments, "subject");
        string member = Args.OptionalText(arguments, "member") ?? "";
        return new ToolResult(await EditorProject.RunAsync(project => Describe(project, subject, member), cancellationToken));
    }

    private static string Describe(LoadedProject project, string subject, string member)
    {
        var catalog = EditorTypeCatalog.Instance;
        if (catalog.Find(subject) is { } type)
            return Describer.OfType(type, member, catalog);

        ExplorerAssetFile asset;
        try
        {
            asset = Content.Resolve(project, subject);
        }
        catch (EditError notContent) when (notContent.Message.StartsWith("No content item", StringComparison.Ordinal))
        {
            throw new EditError($"{catalog.NotFound(subject)} {notContent.Message}");
        }

        if (Block.TryOpen(project, asset) is { } block)
            return block.Describe(member, catalog);
        var (_, fields) = Content.Definition(asset);
        return $"{Content.Describe(asset)}:\n{Describer.OfValue(fields.Root, member, catalog, new ProjectLinks(project, asset))}";
    }
}

internal sealed class EditorTypeCatalog : ITypeCatalog
{
    public static EditorTypeCatalog Instance { get; } = new();

    private const int MaxTypeCandidates = 20;

    // Built once: the type list, names to types, and components with their object builders. Derived types are
    // found in it too, so describe never registers type sets of its own in the Editor's TypeOptionManager.
    private sealed record Index(
        IReadOnlyList<Type> Types,
        Dictionary<string, List<Type>> ByName,
        IReadOnlyList<Type> Components,
        Dictionary<Type, Type> ComponentBuilders);

    private readonly Lazy<Index> _index = new(Load);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, IReadOnlyList<Type>> _concrete = new();

    // Every type the inspector can show, named as the tools name it, plus the component types. The attribute index
    // misses plain serializable types such as ModelDummy, so the engine's and game's assemblies are scanned.
    private static Index Load()
    {
        var found = new HashSet<Type>();
        var components = new List<Type>();
        var builders = new Dictionary<Type, Type>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(IsGameAssembly))
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException e)
            {
                types = e.Types.OfType<Type>().ToArray();
            }
            foreach (var type in types)
            {
                if (type.ContainsGenericParameters || type.IsNested && !type.IsNestedPublic)
                    continue;
                if (RuntimeComponentInfo.IsComponent(type))
                {
                    found.Add(type);
                    if (!type.IsAbstract)
                    {
                        components.Add(type);
                        if (Info(type)?.ObjectBuilder is { } builder)
                            builders.TryAdd(builder, type);
                    }
                }
                else if (typeof(IAccessible).IsAssignableFrom(type) && TypeCatalog.IsAccessible(type))
                {
                    found.Add(TypeCatalog.Shown(type));
                }
            }
        }

        var shown = found.Select(t => builders.GetValueOrDefault(t, t)).Distinct().ToList();
        var byName = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);
        void Name(string? name, Type type)
        {
            if (name == null)
                return;
            if (!byName.TryGetValue(name, out var list))
                byName[name] = list = [];
            if (!list.Contains(type))
                list.Add(type);
        }
        foreach (var type in shown)
        {
            Name(type.Name, type);
            Name(type.FullName, type);
            if (TypeCatalog.BuilderOf(type) is { } builder)
            {
                Name(builder.Name, type);
                Name(builder.FullName, type);
            }
        }
        foreach (var (builder, component) in builders)
        {
            Name(builder.Name, component);
            Name(builder.FullName, component);
        }
        return new Index(shown, byName, components, builders);
    }

    private static bool IsGameAssembly(System.Reflection.Assembly assembly) =>
        !assembly.IsDynamic && assembly.GetName().Name is { } name && (name.StartsWith("VRage", StringComparison.Ordinal) || name.StartsWith("Game2", StringComparison.Ordinal));

    // Exact names only: a name that is no type may still be a content item.
    public Type? Find(string name)
    {
        string wanted = name.Trim();
        if (!_index.Value.ByName.TryGetValue(wanted, out var exact))
            return null;
        return exact.Count == 1
            ? exact[0]
            : throw new EditError($"'{wanted}' names several types; use one of: {string.Join(", ", exact.Select(t => t.FullName).Order(StringComparer.OrdinalIgnoreCase))}.");
    }

    public string NotFound(string name)
    {
        // "BatteryDefinition" is the words Battery and Definition, so a near miss of a type name still finds it.
        string[] words = System.Text.RegularExpressions.Regex.Replace(name.Trim(), "(?<=[a-z0-9])(?=[A-Z])", " ")
            .Split([' ', '_', '-', '.', '/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var near = words.Length == 0 ? [] : _index.Value.Types.Where(t => words.All(w => t.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(t => t.Name).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (near.Count == 0)
            return $"No type is named '{name.Trim()}'.";
        string more = near.Count > MaxTypeCandidates ? $", ... ({near.Count - MaxTypeCandidates} more; add a word)" : "";
        return $"No type is named '{name.Trim()}'; types containing its words: {string.Join(", ", near.Take(MaxTypeCandidates))}{more}.";
    }

    public MemberReference? Fields(Type type)
    {
        if (RuntimeComponentInfo.IsComponent(type))
            return Info(type)?.ObjectBuilder is { } builder ? TypeCatalog.Instance(builder, inPrefab: true) : null;
        return TypeCatalog.Instance(TypeCatalog.BuilderOf(type) ?? type);
    }

    public IReadOnlyList<Type> ConcreteTypes(Type type)
    {
        if (type.IsSealed || type.IsValueType)
            return [];
        return _concrete.GetOrAdd(type, t => _index.Value.Types.Where(c => c != t && !c.IsAbstract && !c.IsInterface && t.IsAssignableFrom(c)).ToList());
    }

    // A component's object builder is not paired with it in the object builder index, so it is mapped here.
    public Type Shown(Type type) => _index.Value.ComponentBuilders.TryGetValue(type, out var component) ? component : TypeCatalog.Shown(type);

    // The baked-in summary first, then the doc files beside the assemblies, which the inspector's Documentation
    // section reads: a component's own class is not serializable, so only the files carry its doc.
    public string? Summary(Type type) =>
        TypeCatalog.Summary(type) ?? FileSummary(type)
        ?? (RuntimeComponentInfo.IsComponent(type) && Info(type)?.ObjectBuilder is { } builder ? TypeCatalog.Summary(builder) : null);

    private static string? FileSummary(Type type)
    {
        try
        {
            return DocsByReflection.DocsService.GetXmlFromType(type, false) is { } element ? DocText.Render(element.InnerXml) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public Type? DefinitionOf(Type component) => RuntimeComponentInfo.IsComponent(component) ? Info(component)?.Definition : null;

    // The nearest definition type a component takes: a derived definition is taken by its base's components.
    public IReadOnlyList<Type> ComponentsTaking(Type definition)
    {
        if (!typeof(Definition).IsAssignableFrom(definition))
            return [];
        for (Type? t = definition; t != null && t != typeof(Definition); t = t.BaseType)
        {
            var taking = _index.Value.Components.Where(c => Info(c)?.Definition == t).ToList();
            if (taking.Count > 0)
                return taking;
        }
        return [];
    }

    private static RuntimeComponentInfo? Info(Type component)
    {
        // A Debug Editor asserts, with a blocking dialog, on a type that is not a component.
        if (!RuntimeComponentInfo.IsComponent(component))
            return null;
        try
        {
            return RuntimeComponentInfo.For(component);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
