using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Unified.Common.Logging;

namespace Unified.Infrastructure.Pdf;

// Only allowlisted labels cross the browser/logging boundary. Browser strings can contain
// session tokens, personal data, and schedule content, even in exception messages.
internal static class SchedulePdfDiagnosticSanitizer
{
    private static readonly HashSet<string> SafeSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "api",
        "auth",
        "user",
        "users",
        "login",
        "logout",
        "signin-oidc",
        "signout-callback-oidc",
        "calendar",
        "config",
        "locations",
        "lookup",
        "scheduling",
        "training",
        "events",
        "assignments",
        "shifts",
        "leave",
        "entries",
        "series",
        "acting-positions",
        "dashboard",
        "unregistered",
        "assets",
        "realms",
        "protocol",
        "openid-connect",
        "token",
    };

    public static string Url(string? value, Uri frontendBaseUri)
    {
        if (value is null || value.Length > 8192 || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return "[invalid-url]";
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return "[non-http-url]";

        var sameOrigin =
            uri.Scheme == frontendBaseUri.Scheme
            && uri.IdnHost == frontendBaseUri.IdnHost
            && uri.Port == frontendBaseUri.Port;
        var origin = sameOrigin
            ? $"{uri.Scheme}://{uri.IdnHost}{(uri.IsDefaultPort ? string.Empty : $":{uri.Port}")}"
            : $"{uri.Scheme}://[external-origin]";
        var path = uri.AbsolutePath;
        var basePath = frontendBaseUri.AbsolutePath.TrimEnd('/');
        var prefix = string.Empty;
        if (basePath.Length > 0 && (path == basePath || path.StartsWith(basePath + "/", StringComparison.Ordinal)))
        {
            prefix = "/[base]";
            path = path[basePath.Length..];
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var safePath = string.Join(
            '/',
            segments
                .Take(8)
                .Select(segment => SafeSegments.Contains(segment) ? segment.ToLowerInvariant() : "[redacted]")
        );
        return origin
            + prefix
            + "/"
            + safePath
            + (segments.Length > 8 ? "/[truncated]" : string.Empty)
            + (uri.Query.Length > 0 ? "?[redacted]" : string.Empty);
    }

    public static string BrowserText(string? value)
    {
        // Bound work before inspecting text; never retain or return arbitrary browser text.
        var text = LogSanitizer.UserText(value is null ? null : value[..Math.Min(value.Length, 512)], 512) ?? "";
        foreach (var label in new[] { "TypeError", "ReferenceError", "SyntaxError", "RangeError", "ChunkLoadError" })
            if (text.Contains(label, StringComparison.Ordinal))
                return label + " [message-redacted]";
        foreach (
            var code in new[]
            {
                "ERR_CONNECTION_REFUSED",
                "ERR_CONNECTION_RESET",
                "ERR_NAME_NOT_RESOLVED",
                "ERR_TIMED_OUT",
                "ERR_ABORTED",
                "ERR_FAILED",
                "ERR_CERT_AUTHORITY_INVALID",
                "ERR_TOO_MANY_REDIRECTS",
                "ERR_BLOCKED_BY_CLIENT",
            }
        )
            if (text.Contains("net::" + code, StringComparison.Ordinal))
                return "net::" + code;
        return "[message-redacted]";
    }

    public static string Method(string value) =>
        value is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS" ? value : "[other-method]";

    public static string RenderState(string? value) =>
        value is "loading" or "complete" or "error" ? value
        : value is null ? "[absent]"
        : "[unknown]";

    public static string ReadyState(string? value) =>
        value is "loading" or "interactive" or "complete" ? value : "[unknown]";
}

// Playwright's evaluation converter requires a parameterless constructor and writable properties.
internal sealed class SchedulePdfPageSnapshot
{
    public bool RootExists { get; set; }
    public string? RenderState { get; set; }
    public string? ReadyState { get; set; }
}

internal sealed class SchedulePdfDiagnostics : IDisposable
{
    internal const int MaximumTrackedRequests = 32;
    internal const int MaximumRecentEvents = 12;
    internal const int MaximumVerboseEvents = 40;
    internal const int MaximumFailureRequests = 10;
    private static readonly TimeSpan FailureBudget = TimeSpan.FromSeconds(2);
    private readonly ILogger logger;
    private readonly Uri frontendBaseUri;
    private readonly bool verbose;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly object gate = new();
    private readonly Dictionary<IRequest, PendingRequest> requests = new(ReferenceEqualityComparer.Instance);
    private readonly Queue<BrowserEvent> recentEvents = new();
    private readonly List<string> stages = [];
    private IPage? page;
    private volatile bool detached;
    private int inFlight;
    private int eventCount;
    private int verboseEvents;
    private int omittedRequests;
    private int failedRequests;
    private int httpErrors;
    private int consoleErrors;
    private bool pageCrashed;
    private string stage = "preparation";
    private string requestedUrl = "[not-navigated]";
    private string finalUrl = "[unavailable]";
    private int? navigationStatus;

    public SchedulePdfDiagnostics(ILogger logger, Uri frontendBaseUri, bool verbose)
    {
        this.logger = logger;
        this.frontendBaseUri = frontendBaseUri;
        this.verbose = verbose;
    }

    public string RenderId { get; } = Guid.NewGuid().ToString("N");

    public void Stage(string nextStage) =>
        BestEffort(() =>
        {
            lock (gate)
            {
                stage = nextStage;
                if (stages.Count >= 16)
                    return;
                stages.Add($"{stage}@{clock.ElapsedMilliseconds}ms");
            }
            if (verbose)
                logger.LogDebug(
                    "Schedule PDF diagnostic stage RenderId={RenderId} Stage={Stage} ElapsedMs={ElapsedMs}",
                    RenderId,
                    nextStage,
                    clock.ElapsedMilliseconds
                );
        });

    public void Attach(IPage browserPage) =>
        BestEffort(() =>
        {
            page = browserPage;
            page.Console += OnConsole;
            page.PageError += OnPageError;
            page.Crash += OnPageCrash;
            page.Request += OnRequest;
            page.RequestFinished += OnRequestFinished;
            page.RequestFailed += OnRequestFailed;
            page.Response += OnResponse;
        });

    public void Navigation(Uri requested, IResponse? response) =>
        BestEffort(() =>
        {
            requestedUrl = SchedulePdfDiagnosticSanitizer.Url(requested.AbsoluteUri, frontendBaseUri);
            finalUrl = SchedulePdfDiagnosticSanitizer.Url(page?.Url, frontendBaseUri);
            navigationStatus = response?.Status;
            AddEvent(new("navigation", "GET", requestedUrl, response?.Status, "final=" + finalUrl));
        });

    // Called before GotoAsync so a navigation failure still has a sanitized requested URL.
    public void RequestedUrl(Uri requested) =>
        BestEffort(() => requestedUrl = SchedulePdfDiagnosticSanitizer.Url(requested.AbsoluteUri, frontendBaseUri));

    public async Task FailureAsync(string failureKind)
    {
        var failedAtMs = clock.ElapsedMilliseconds;
        // No dependency on the exhausted render token, and diagnostics never throw to the renderer.
        try
        {
            BestEffort(Detach);
            SchedulePdfPageSnapshot? snapshot = null;
            var snapshotStatus = "no-page";
            var snapshotFailure = "none";
            bool? pageClosed = null;
            if (page is not null)
            {
                snapshotStatus = "unavailable";
                BestEffort(() => finalUrl = SchedulePdfDiagnosticSanitizer.Url(page.Url, frontendBaseUri));
                BestEffort(() => pageClosed = page.IsClosed);
                try
                {
                    snapshot = await page.EvaluateAsync<SchedulePdfPageSnapshot>(
                            """
                            () => {
                                const root = document.querySelector('#calendar-print-root');
                                const state = root?.getAttribute('data-render-state');
                                return {
                                    RootExists: !!root,
                                    RenderState: state == null ? null
                                        : ['loading', 'complete', 'error'].includes(state) ? state : '[unknown]',
                                    ReadyState: document.readyState
                                };
                            }
                            """
                        )
                        .WaitAsync(FailureBudget, CancellationToken.None);
                    snapshotStatus = "captured";
                }
                catch (Exception exception)
                {
                    // Fixed categories only: exception messages can contain browser or user data.
                    snapshotFailure = exception switch
                    {
                        TimeoutException => "diagnostic-timeout",
                        PlaywrightException => "browser-evaluation-failed",
                        _ => "snapshot-failed",
                    };
                }
            }

            long managedBytes = -1;
            long workingSetBytes = -1;
            BestEffort(() => managedBytes = GC.GetTotalMemory(false));
            BestEffort(() => workingSetBytes = Environment.WorkingSet);
            lock (gate)
            {
                logger.LogWarning(
                    "Schedule PDF diagnostic failure RenderId={RenderId} FailureKind={FailureKind} Stage={Stage} ElapsedMs={ElapsedMs} RequestedUrl={RequestedUrl} NavigationStatus={NavigationStatus} CurrentUrl={CurrentUrl} Snapshot={Snapshot} SnapshotFailure={SnapshotFailure} SnapshotElapsedMs={SnapshotElapsedMs} PageClosed={PageClosed} PageCrashed={PageCrashed} RootExists={RootExists} RenderState={RenderState} ReadyState={ReadyState} InFlight={InFlight} Tracked={Tracked} OmittedRequests={OmittedRequests} FailedRequests={FailedRequests} HttpErrors={HttpErrors} ConsoleErrors={ConsoleErrors} BrowserEvents={BrowserEvents} StageTimeline={StageTimeline} ManagedMemoryBytes={ManagedMemoryBytes} ProcessWorkingSetBytes={ProcessWorkingSetBytes}",
                    RenderId,
                    failureKind,
                    stage,
                    failedAtMs,
                    requestedUrl,
                    navigationStatus,
                    finalUrl,
                    snapshotStatus,
                    snapshotFailure,
                    clock.ElapsedMilliseconds - failedAtMs,
                    pageClosed,
                    pageCrashed,
                    snapshot?.RootExists,
                    SchedulePdfDiagnosticSanitizer.RenderState(snapshot?.RenderState),
                    SchedulePdfDiagnosticSanitizer.ReadyState(snapshot?.ReadyState),
                    inFlight,
                    requests.Count,
                    omittedRequests,
                    failedRequests,
                    httpErrors,
                    consoleErrors,
                    eventCount,
                    string.Join(",", stages),
                    managedBytes,
                    workingSetBytes
                );
                foreach (var entry in recentEvents)
                    WriteEvent(LogLevel.Warning, entry);
                foreach (var request in requests.Values.Take(MaximumFailureRequests))
                    logger.LogWarning(
                        "Schedule PDF diagnostic pending RenderId={RenderId} Method={Method} Url={Url} PendingMs={PendingMs}",
                        RenderId,
                        request.Method,
                        request.Url,
                        failedAtMs - request.StartedMs
                    );
            }
        }
        catch
        {
            // Includes a failing log sink: preserve the original rendering failure.
        }
    }

    private void OnConsole(object? sender, IConsoleMessage message) =>
        BestEffort(() =>
        {
            if (message.Type is not ("warning" or "error"))
                return;
            lock (gate)
            {
                if (detached)
                    return;
                consoleErrors++;
                AddEvent(
                    new(
                        "console-" + message.Type,
                        "-",
                        "-",
                        null,
                        SchedulePdfDiagnosticSanitizer.BrowserText(message.Text)
                    )
                );
            }
        });

    private void OnPageCrash(object? sender, IPage crashedPage) =>
        BestEffort(() =>
        {
            lock (gate)
            {
                if (detached)
                    return;
                pageCrashed = true;
                AddEvent(new("page-crashed", "-", "-", null, "-"));
            }
        });

    private void OnPageError(object? sender, string error) =>
        BestEffort(() =>
        {
            lock (gate)
            {
                if (detached)
                    return;
                consoleErrors++;
                AddEvent(new("page-error", "-", "-", null, SchedulePdfDiagnosticSanitizer.BrowserText(error)));
            }
        });

    private void OnRequest(object? sender, IRequest request) =>
        BestEffort(() =>
        {
            lock (gate)
            {
                if (detached)
                    return;
                inFlight++;
                if (requests.Count < MaximumTrackedRequests)
                    requests[request] = new(
                        SchedulePdfDiagnosticSanitizer.Method(request.Method),
                        SchedulePdfDiagnosticSanitizer.Url(request.Url, frontendBaseUri),
                        clock.ElapsedMilliseconds
                    );
                else
                    omittedRequests++;
            }
        });

    private void OnRequestFinished(object? sender, IRequest request) =>
        BestEffort(() =>
        {
            lock (gate)
            {
                if (detached)
                    return;
                requests.Remove(request);
                inFlight = Math.Max(0, inFlight - 1);
            }
        });

    private void OnRequestFailed(object? sender, IRequest request) =>
        BestEffort(() =>
        {
            lock (gate)
            {
                if (detached)
                    return;
                failedRequests++;
                OnRequestFinished(sender, request);
                AddEvent(
                    new(
                        "request-failed",
                        SchedulePdfDiagnosticSanitizer.Method(request.Method),
                        SchedulePdfDiagnosticSanitizer.Url(request.Url, frontendBaseUri),
                        null,
                        SchedulePdfDiagnosticSanitizer.BrowserText(request.Failure)
                    )
                );
            }
        });

    private void OnResponse(object? sender, IResponse response) =>
        BestEffort(() =>
        {
            // Also retain redirects to distinguish an auth redirect from a missing print root.
            if (response.Status < 300)
                return;
            lock (gate)
            {
                if (detached)
                    return;
                if (response.Status >= 400)
                    httpErrors++;
                AddEvent(
                    new(
                        "http-response",
                        SchedulePdfDiagnosticSanitizer.Method(response.Request.Method),
                        SchedulePdfDiagnosticSanitizer.Url(response.Url, frontendBaseUri),
                        response.Status,
                        "-"
                    )
                );
            }
        });

    private void AddEvent(BrowserEvent entry)
    {
        lock (gate)
        {
            entry = entry with { ElapsedMs = clock.ElapsedMilliseconds };
            eventCount++;
            if (recentEvents.Count == MaximumRecentEvents)
                recentEvents.Dequeue();
            recentEvents.Enqueue(entry);
            if (verbose && verboseEvents++ < MaximumVerboseEvents)
                WriteEvent(LogLevel.Debug, entry);
        }
    }

    private void WriteEvent(LogLevel level, BrowserEvent entry) =>
        logger.Log(
            level,
            "Schedule PDF diagnostic browser RenderId={RenderId} ElapsedMs={ElapsedMs} Kind={Kind} Method={Method} Status={Status} Url={Url} Detail={Detail}",
            RenderId,
            entry.ElapsedMs,
            entry.Kind,
            entry.Method,
            entry.Status,
            entry.Url,
            entry.Detail
        );

    private void Detach()
    {
        if (page is null || detached)
            return;
        detached = true;
        BestEffort(() => page.Console -= OnConsole);
        BestEffort(() => page.PageError -= OnPageError);
        BestEffort(() => page.Crash -= OnPageCrash);
        BestEffort(() => page.Request -= OnRequest);
        BestEffort(() => page.RequestFinished -= OnRequestFinished);
        BestEffort(() => page.RequestFailed -= OnRequestFailed);
        BestEffort(() => page.Response -= OnResponse);
    }

    private static void BestEffort(Action action)
    {
        try
        {
            action();
        }
        catch
        { /* Diagnostics must not affect rendering. */
        }
    }

    public void Dispose()
    {
        BestEffort(Detach);
        lock (gate)
        {
            requests.Clear();
            recentEvents.Clear();
            stages.Clear();
            page = null;
        }
    }

    private sealed record PendingRequest(string Method, string Url, long StartedMs);

    private sealed record BrowserEvent(
        string Kind,
        string Method,
        string Url,
        int? Status,
        string Detail,
        long ElapsedMs = 0
    );
}
