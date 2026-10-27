using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Unified.Core.Pdf;
using Unified.Infrastructure.Options;
using Unified.Infrastructure.Pdf;

namespace Unified.Tests.Infrastructure.Pdf;

public sealed class SchedulePdfDiagnosticsTests
{
    private static readonly Uri Frontend = new("http://web:8080/unified-scheduling/");

    [Theory]
    [InlineData(
        "http://username:password@web:8080/unified-scheduling/calendar?token=secret&locationId=123#private",
        "http://web:8080/[base]/calendar?[redacted]"
    )]
    [InlineData(
        "http://web:8080/api/users/Alice%40example.test/entries/123?secret=value",
        "http://web:8080/api/users/[redacted]/entries/[redacted]?[redacted]"
    )]
    [InlineData(
        "https://secret@identity.example.test/realms/private/protocol/openid-connect/auth?code=token#private",
        "https://[external-origin]/realms/[redacted]/protocol/openid-connect/auth?[redacted]"
    )]
    [InlineData("data:text/html,secret", "[non-http-url]")]
    [InlineData("not-a-url secret", "[invalid-url]")]
    public void Url_WithSensitiveComponents_OnlyRetainsSafeRouteLabels(string input, string expected)
    {
        Assert.Equal(expected, SchedulePdfDiagnosticSanitizer.Url(input, Frontend));
    }

    [Theory]
    [InlineData("TypeError: Alice token=secret\nAuthorization: Bearer abc", "TypeError [message-redacted]")]
    [InlineData("net::ERR_CONNECTION_REFUSED http://user:password@web?token=secret", "net::ERR_CONNECTION_REFUSED")]
    [InlineData("Patient name, schedule title, password=secret", "[message-redacted]")]
    public void BrowserText_WithSensitiveContent_OnlyReturnsAllowlistedCategories(string input, string expected)
    {
        Assert.Equal(expected, SchedulePdfDiagnosticSanitizer.BrowserText(input));
        Assert.True(SchedulePdfDiagnosticSanitizer.BrowserText(new string('x', 100_000)).Length < 64);
    }

    [Fact]
    public void Url_WithUnboundedPathAndQuery_HasBoundedOutput()
    {
        var result = SchedulePdfDiagnosticSanitizer.Url(
            "http://web:8080/" + string.Join('/', Enumerable.Repeat("secret", 100)) + "?secret=value",
            Frontend
        );
        Assert.True(result.Length < 256);
        Assert.DoesNotContain("secret", result);
        Assert.Contains("[truncated]", result);
        Assert.Equal("[invalid-url]", SchedulePdfDiagnosticSanitizer.Url(new string('x', 9000), Frontend));
    }

    [Fact]
    public async Task Failure_WithRequestAndConsoleFlood_LogsBoundedSafeSamplesAndDetachesHandlers()
    {
        var logger = new RecordingLogger();
        var page = Page();
        using var diagnostics = new SchedulePdfDiagnostics(logger, Frontend, true);
        diagnostics.Attach(page);
        diagnostics.Stage("render-state-wait");
        diagnostics.Navigation(new Uri(Frontend, "calendar?locationId=123&token=secret"), Response(200));
        for (var index = 0; index < 100; index++)
        {
            StubProxy.Emit(page, "Request", Request("http://web:8080/api/users/Alice?secret=" + index));
            StubProxy.Emit(page, "PageError", "TypeError: Alice secret schedule content");
        }
        StubProxy.Emit(page, "Response", Response(401));
        var failed = Request("http://web:8080/api/auth/user?token=secret");
        StubProxy.Emit(page, "Request", failed);
        StubProxy.Emit(page, "RequestFailed", failed);
        StubProxy.Emit(
            page,
            "Console",
            StubProxy.Create<IConsoleMessage>(
                (method, _) =>
                    method.Name switch
                    {
                        "get_Type" => "warning",
                        "get_Text" => "ReferenceError: private schedule content",
                        _ => throw new NotSupportedException(),
                    }
            )
        );

        await diagnostics.FailureAsync("playwright-failure");

        Assert.Equal(
            SchedulePdfDiagnostics.MaximumVerboseEvents,
            logger.Entries.Count(entry => entry.Level == LogLevel.Debug && entry.Message.Contains("diagnostic browser"))
        );
        Assert.Equal(
            SchedulePdfDiagnostics.MaximumRecentEvents,
            logger.Entries.Count(entry =>
                entry.Level == LogLevel.Warning && entry.Message.Contains("diagnostic browser")
            )
        );
        Assert.Equal(
            SchedulePdfDiagnostics.MaximumFailureRequests,
            logger.Entries.Count(entry => entry.Message.Contains("diagnostic pending"))
        );
        var summary = Assert.Single(logger.Entries, entry => entry.Message.Contains("diagnostic failure")).Message;
        Assert.Contains("Tracked=32", summary);
        Assert.Contains("OmittedRequests=69", summary);
        Assert.Contains("Stage=render-state-wait", summary);
        Assert.Contains("RootExists=True", summary);
        Assert.Contains("RenderState=loading", summary);
        Assert.Contains("ReadyState=complete", summary);
        Assert.Contains("NavigationStatus=200", summary);
        Assert.Contains("Status=401", string.Join('\n', logger.Entries.Select(entry => entry.Message)));
        Assert.Contains("request-failed", string.Join('\n', logger.Entries.Select(entry => entry.Message)));
        Assert.All(
            logger.Entries,
            entry =>
            {
                Assert.DoesNotContain("Alice", entry.Message);
                Assert.DoesNotContain("secret", entry.Message);
                Assert.DoesNotContain("private schedule", entry.Message);
                Assert.DoesNotContain("token=", entry.Message);
                Assert.Contains(diagnostics.RenderId, entry.Message);
            }
        );
        Assert.All(((StubProxy)(object)page).Handlers.Values, handler => Assert.Null(handler));
    }

    [Fact]
    public async Task Failure_WithoutVerboseDiagnostics_StillSummarizesRedirectsAndFinishedRequests()
    {
        var logger = new RecordingLogger();
        var page = Page();
        using var diagnostics = new SchedulePdfDiagnostics(logger, Frontend, false);
        diagnostics.Attach(page);
        diagnostics.Stage("navigation");
        var finished = Request("http://web:8080/api/auth/user");
        StubProxy.Emit(page, "Request", finished);
        StubProxy.Emit(page, "RequestFinished", finished);
        StubProxy.Emit(page, "Response", Response(302));
        Assert.Empty(logger.Entries);
        await diagnostics.FailureAsync("render-failure");
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("Status=302"));
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("InFlight=0"));
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Debug);
    }

    [Fact]
    public async Task Failure_WithMissingRootAndUnexpectedStates_DoesNotLogAttributeValues()
    {
        var logger = new RecordingLogger();
        using var diagnostics = new SchedulePdfDiagnostics(logger, Frontend, false);
        diagnostics.Attach(
            Page(
                Task.FromResult(
                    new SchedulePdfPageSnapshot
                    {
                        RootExists = false,
                        RenderState = "Alice secret",
                        ReadyState = "token=value",
                    }
                )
            )
        );
        await diagnostics.FailureAsync("render-failure");
        var summary = Assert.Single(logger.Entries).Message;
        Assert.Contains("RootExists=False", summary);
        Assert.Contains("RenderState=[unknown]", summary);
        Assert.Contains("ReadyState=[unknown]", summary);
        Assert.DoesNotContain("secret", summary);
        Assert.DoesNotContain("Alice", summary);
        Assert.DoesNotContain("token=", summary);
    }

    [Fact]
    public async Task Failure_WhenSnapshotNeverCompletes_UsesIndependentBoundAndKeepsSummary()
    {
        var logger = new RecordingLogger();
        var never = new TaskCompletionSource<SchedulePdfPageSnapshot>();
        var page = Page(never.Task);
        using var diagnostics = new SchedulePdfDiagnostics(logger, Frontend, false);
        diagnostics.Attach(page);
        // External bound prevents a regression from hanging the test runner.
        await diagnostics
            .FailureAsync("render-budget-exhausted")
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("Snapshot=unavailable"));
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("SnapshotFailure=diagnostic-timeout"));
        never.SetResult(new() { RootExists = false, ReadyState = "complete" });
    }

    [Fact]
    public async Task Failure_WhenBrowserEvaluationFails_LogsOnlyFixedFailureCategory()
    {
        var logger = new RecordingLogger();
        var snapshot = Task.FromException<SchedulePdfPageSnapshot>(new PlaywrightException("private token=secret"));
        using var diagnostics = new SchedulePdfDiagnostics(logger, Frontend, false);
        diagnostics.Attach(Page(snapshot));
        await diagnostics.FailureAsync("render-failure");
        var summary = Assert.Single(logger.Entries).Message;
        Assert.Contains("SnapshotFailure=browser-evaluation-failed", summary);
        Assert.DoesNotContain("private", summary);
        Assert.DoesNotContain("secret", summary);
    }

    [Fact]
    public async Task Failure_WhenPageCrashes_RecordsSafeIndicatorsAndDetachesHandlers()
    {
        var logger = new RecordingLogger();
        var page = Page(snapshotFailure: true);
        using var diagnostics = new SchedulePdfDiagnostics(logger, Frontend, false);
        diagnostics.Attach(page);
        StubProxy.Emit(page, "Crash", page);
        await diagnostics.FailureAsync("render-failure");
        var summary = logger.Entries.First().Message;
        Assert.Contains("PageCrashed=True", summary);
        Assert.Contains("PageClosed=False", summary);
        Assert.Contains("SnapshotFailure=snapshot-failed", summary);
        Assert.All(logger.Entries, entry => Assert.DoesNotContain("secret", entry.Message));
        Assert.All(((StubProxy)(object)page).Handlers.Values, handler => Assert.Null(handler));
    }

    // Explicit: requires the matching Playwright Chromium installation; no application or network access.
    [Theory(Explicit = true)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Failure_WithRealBrowser_CapturesSnapshotThroughPlaywrightConversion(bool rootExists)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        if (rootExists)
            await page.SetContentAsync("<div id='calendar-print-root' data-render-state='loading'></div>");
        var logger = new RecordingLogger();
        using var diagnostics = new SchedulePdfDiagnostics(logger, Frontend, false);
        diagnostics.Attach(page);
        await diagnostics.FailureAsync("render-failure");
        var summary = Assert.Single(logger.Entries).Message;
        Assert.Contains("Snapshot=captured", summary);
        Assert.Contains("SnapshotFailure=none", summary);
        Assert.Contains($"RootExists={rootExists}", summary);
        Assert.Contains(rootExists ? "RenderState=loading" : "RenderState=[absent]", summary);
        Assert.Contains("ReadyState=complete", summary);
        Assert.Contains("PageClosed=False", summary);
        Assert.Contains("PageCrashed=False", summary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Render_WhenReadinessFails_PreservesOriginalExceptionEvenWhenDiagnosticsFail(bool failingLogger)
    {
        var original = new PlaywrightException("original secret URL http://web/calendar?token=secret");
        var locator = StubProxy.Create<ILocator>(
            (method, _) =>
                method.Name == "WaitForAsync" ? Task.FromException(original) : throw new NotSupportedException()
        );
        var page = Page(locator: locator, snapshotFailure: true);
        var context = StubProxy.Create<IBrowserContext>(
            (method, _) =>
                method.Name switch
                {
                    "AddCookiesAsync" or "CloseAsync" => Task.CompletedTask,
                    "NewPageAsync" => Task.FromResult(page),
                    _ => throw new NotSupportedException(method.Name),
                }
        );
        var browser = StubProxy.Create<IBrowser>(
            (method, _) =>
                method.Name switch
                {
                    "get_IsConnected" => true,
                    "NewContextAsync" => Task.FromResult(context),
                    "CloseAsync" => Task.CompletedTask,
                    _ => throw new NotSupportedException(method.Name),
                }
        );
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = "UnifiedAuth.Development=session-secret";
        var logger = new RecordingLogger { ThrowOnLog = failingLogger };
        await using var renderer = new PlaywrightSchedulePdfRenderer(
            new HttpContextAccessor { HttpContext = httpContext },
            new TestEnvironment(),
            Options.Create(new ScheduleDistributionRenderingOptions { FrontendBaseUrl = Frontend.AbsoluteUri }),
            Options.Create(new KeycloakOptions { CookieName = "UnifiedAuth" }),
            logger
        );
        typeof(PlaywrightSchedulePdfRenderer)
            .GetField("_browser", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(renderer, browser);

        var exception = await Assert.ThrowsAsync<PdfRenderingException>(() =>
            renderer.RenderAsync(
                new()
                {
                    View = SchedulePdfView.Schedule,
                    LocationId = 1,
                    StartDate = new(2026, 10, 26),
                    EndDate = new(2026, 10, 27),
                    Period = SchedulePdfPeriod.Day,
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Same(original, exception.InnerException);
        Assert.Equal("The calendar PDF could not be generated.", exception.Message);
        Assert.All(logger.Entries, entry => Assert.DoesNotContain("secret", entry.Message));
        Assert.All(((StubProxy)(object)page).Handlers.Values, handler => Assert.Null(handler));
    }

    private static IRequest Request(string url) =>
        StubProxy.Create<IRequest>(
            (method, _) =>
                method.Name switch
                {
                    "get_Url" => url,
                    "get_Method" => "POST",
                    "get_Failure" => "net::ERR_CONNECTION_REFUSED secret",
                    _ => throw new NotSupportedException(method.Name),
                }
        );

    private static IResponse Response(int status) =>
        StubProxy.Create<IResponse>(
            (method, _) =>
                method.Name switch
                {
                    "get_Status" => status,
                    "get_Ok" => status < 400,
                    "get_Url" => "http://web:8080/api/auth/user?secret=value",
                    "get_Request" => Request("http://web:8080/api/auth/user?secret=value"),
                    _ => throw new NotSupportedException(method.Name),
                }
        );

    private static IPage Page(
        Task<SchedulePdfPageSnapshot>? snapshot = null,
        ILocator? locator = null,
        bool snapshotFailure = false
    ) =>
        StubProxy.Create<IPage>(
            (method, _) =>
                method.Name switch
                {
                    "get_Url" => "http://web:8080/unified-scheduling/calendar?token=secret#private",
                    "get_IsClosed" => false,
                    "EvaluateAsync" => snapshotFailure
                        ? throw new InvalidOperationException("secret")
                        : snapshot
                            ?? Task.FromResult(
                                new SchedulePdfPageSnapshot
                                {
                                    RootExists = true,
                                    RenderState = "loading",
                                    ReadyState = "complete",
                                }
                            ),
                    "Locator" => locator,
                    "GotoAsync" => Task.FromResult<IResponse?>(Response(200)),
                    "SetDefaultTimeout" or "SetDefaultNavigationTimeout" => null,
                    _ => throw new NotSupportedException(method.Name),
                }
        );

    public class StubProxy : DispatchProxy
    {
        public Dictionary<string, Delegate?> Handlers { get; } = [];
        private Func<MethodInfo, object?[]?, object?> implementation = null!;

        public static T Create<T>(Func<MethodInfo, object?[]?, object?> implementation)
            where T : class
        {
            var proxy = Create<T, StubProxy>();
            ((StubProxy)(object)proxy).implementation = implementation;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var name = targetMethod!.Name;
            if (
                name.StartsWith("add_", StringComparison.Ordinal)
                || name.StartsWith("remove_", StringComparison.Ordinal)
            )
            {
                var eventName = name[(name.IndexOf('_') + 1)..];
                Handlers.TryGetValue(eventName, out var existing);
                Handlers[eventName] = name.StartsWith("add_", StringComparison.Ordinal)
                    ? Delegate.Combine(existing, (Delegate?)args![0])
                    : Delegate.Remove(existing, (Delegate?)args![0]);
                return null;
            }
            return implementation(targetMethod, args);
        }

        public static void Emit<T>(object proxy, string name, T value)
        {
            if (((StubProxy)proxy).Handlers.TryGetValue(name, out var handler))
                ((EventHandler<T>?)handler)?.Invoke(proxy, value);
        }
    }

    private sealed class RecordingLogger : ILogger<PlaywrightSchedulePdfRenderer>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public bool ThrowOnLog { get; init; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel level,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (ThrowOnLog)
                throw new InvalidOperationException("log sink failed");
            Assert.Null(exception);
            Entries.Add((level, formatter(state, exception)));
        }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
