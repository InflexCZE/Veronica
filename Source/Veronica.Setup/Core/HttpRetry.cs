using System.Globalization;
using System.Net;

namespace Veronica.Setup.Core;

public static class Backoff
{
    public static readonly TimeSpan Max = TimeSpan.FromSeconds(30);

    public static TimeSpan Step(int retry) => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, retry - 1), Max.TotalSeconds));
}

// Hugging Face answers 429 "maximum queue size reached" when its backend is overloaded, whatever the caller's
// quota; such answers and dropped connections clear within seconds, so every Launcher request rides them out
// (Docs/Tech/Distribution.md).
public sealed class RetryHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    public const int Retries = 8;

    public static readonly TimeSpan Budget = TimeSpan.FromTicks(Enumerable.Range(1, Retries).Sum(r => Backoff.Step(r).Ticks));

    public Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;

    public Action<string>? Log { get; init; }

    public static bool Transient(HttpStatusCode status) => status is
        HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError
        or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // A followed redirect rewrites RequestUri to the target, e.g. a signed CDN URL that must not reach the log
        // and may expire; a retry starts over from the original.
        var uri = request.RequestUri;
        var spent = TimeSpan.Zero;
        for (int retry = 1; ; retry++)
        {
            // A request body may already be consumed, so only body-less requests are sent again.
            bool last = retry > Retries || request.Content != null;
            request.RequestUri = uri;
            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException e) when (!last && !cancellationToken.IsCancellationRequested)
            {
                spent += await WaitAsync(uri, e.Message, Backoff.Step(retry), cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (last || !Transient(response.StatusCode))
                return response;
            var wait = Asked(response) ?? Backoff.Step(retry);
            // Waiting past the budget cannot help, so the answer is the failure now rather than after the budget.
            if (wait > Budget - spent)
                return response;
            response.Dispose();
            spent += await WaitAsync(uri, $"answered {(int)response.StatusCode}", wait, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<TimeSpan> WaitAsync(Uri? uri, string why, TimeSpan wait, CancellationToken cancellationToken)
    {
        Log?.Invoke(string.Create(CultureInfo.InvariantCulture, $"{uri} {why}, trying again in {wait.TotalSeconds:0} s"));
        await Delay(wait, cancellationToken).ConfigureAwait(false);
        return wait;
    }

    private static TimeSpan? Asked(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var asked = retryAfter?.Delta ?? (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        return asked < TimeSpan.Zero ? TimeSpan.Zero : asked;
    }
}
