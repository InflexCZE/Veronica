using Veronica.Agent;

namespace Veronica;

// Stands in for a provider that cannot start; every turn shows why.
internal sealed class UnavailableProvider(string reason) : IProvider
{
    public IProviderSession OpenSession(string name) => new Session(reason);

    public int ContextWindow => IProvider.AssumedContextWindow;

    public bool RunsSubagents => false;

    private sealed class Session(string reason) : IProviderSession
    {
        public Task RunTurnAsync(TurnRequest request, ITurnHost host, CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException(reason));

        public Task ResetAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }
}
