namespace Veronica.Agent;

// What one conversation's tools work on: the skills and wiki pages loaded when it began, and its skill plan.
// Starting over means a new instance, never resetting the parts of this one.
public sealed class SessionState(IReadOnlyList<Skill> skills, IReadOnlyList<WikiPage> pages)
{
    public IReadOnlyList<Skill> Skills => skills;

    public IReadOnlyList<WikiPage> Pages => pages;

    public SkillPlan Plan { get; } = new();
}

// The session a tool call works on: the one its caller entered, else the chat's.
public sealed class SessionScope(Func<SessionState> create)
{
    private readonly AsyncLocal<SessionState?> _entered = new();
    private readonly object _gate = new();
    private SessionState? _chat;

    public SessionState Current => _entered.Value ?? Chat;

    public SessionState Chat
    {
        get
        {
            lock (_gate)
                return _chat ??= create();
        }
    }

    // The chat starting over, as on /clear.
    public SessionState Renew()
    {
        var fresh = create();
        lock (_gate)
            _chat = fresh;
        return fresh;
    }

    public SessionState Create() => create();

    // An async method's changes to an AsyncLocal stay inside its own call, so the caller's session is untouched.
    public async Task<T> RunInAsync<T>(SessionState session, Func<Task<T>> run)
    {
        _entered.Value = session;
        return await run().ConfigureAwait(false);
    }
}
