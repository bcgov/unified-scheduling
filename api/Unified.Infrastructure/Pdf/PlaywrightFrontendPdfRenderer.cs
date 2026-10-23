using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Unified.Core.Pdf;
using Unified.Infrastructure.Options;

namespace Unified.Infrastructure.Pdf;

public sealed class PlaywrightSchedulePdfRenderer(
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment environment,
    IOptions<ScheduleDistributionRenderingOptions> renderingOptions,
    IOptions<KeycloakOptions> keycloakOptions,
    ILogger<PlaywrightSchedulePdfRenderer> logger
) : ISchedulePdfRenderer, IAsyncDisposable
{
    private const string RenderRootSelector = "#calendar-print-root";
    private const string PdfMargin = "0.25in";
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _browserLaunchLock = new(1, 1);
    private readonly object _renderSemaphoreLock = new();
    private readonly CancellationTokenSource _disposeCancellation = new();
    private SemaphoreSlim? _renderSemaphore;
    private int _maximumConcurrentRenders;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _disposed;

    public async Task<byte[]> RenderAsync(
        SchedulePdfRenderRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ScheduleDistributionRenderingOptions options;
        try
        {
            options = renderingOptions.Value;
        }
        catch (OptionsValidationException exception)
        {
            logger.LogError(
                exception,
                "Schedule distribution rendering configuration is invalid. Invalid fields: {InvalidFields}",
                string.Join(", ", exception.Failures.Select(GetConfigurationField))
            );
            throw new PdfRenderingException(
                "The PDF rendering service is not configured.",
                exception,
                isServiceUnavailable: true
            );
        }

        using var timeoutCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(options.RenderTimeoutSeconds));
        using var renderCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellation.Token,
            _disposeCancellation.Token
        );
        var renderToken = renderCancellation.Token;
        var targetUri = BuildTrustedTargetUri(options.FrontendBaseUrl, request);
        var authenticationCookies = GetAuthenticationCookies(targetUri);
        var renderSemaphore = GetRenderSemaphore(options.MaximumConcurrentRenders);
        var permitAcquired = false;
        IBrowserContext? browserContext = null;

        try
        {
            await renderSemaphore.WaitAsync(renderToken);
            permitAcquired = true;

            ObjectDisposedException.ThrowIf(_disposed, this);
            var browser = await GetBrowserAsync(renderToken);
            var contextTask = browser.NewContextAsync(
                new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize
                    {
                        Width = options.DefaultViewportWidth,
                        Height = options.DefaultViewportHeight,
                    },
                    DeviceScaleFactor = 1,
                    ColorScheme = ColorScheme.Light,
                    AcceptDownloads = false,
                }
            );
            try
            {
                browserContext = await contextTask.WaitAsync(renderToken);
            }
            catch
            {
                _ = CloseBrowserContextWhenReadyAsync(contextTask);
                throw;
            }

            await browserContext.AddCookiesAsync(authenticationCookies).WaitAsync(renderToken);

            var page = await browserContext.NewPageAsync().WaitAsync(renderToken);
            page.SetDefaultTimeout(options.RenderTimeoutSeconds * 1000);
            page.SetDefaultNavigationTimeout(options.RenderTimeoutSeconds * 1000);

            var response = await page.GotoAsync(
                    targetUri.AbsoluteUri,
                    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }
                )
                .WaitAsync(renderToken);

            if (response is null || !response.Ok)
                throw new PdfRenderingException("The calendar print view could not be loaded.");

            var renderRoot = page.Locator(RenderRootSelector);
            await page.Locator(
                    $"{RenderRootSelector}[data-render-state='complete'], {RenderRootSelector}[data-render-state='error']"
                )
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached })
                .WaitAsync(renderToken);
            if (await renderRoot.GetAttributeAsync("data-render-state").WaitAsync(renderToken) == "error")
                throw new PdfRenderingException("The calendar print view reported a rendering error.");

            await page.EvaluateAsync(
                    "async () => { await document.fonts.ready; await new Promise(requestAnimationFrame); await new Promise(requestAnimationFrame); }"
                )
                .WaitAsync(renderToken);

            await page.EmulateMediaAsync(new PageEmulateMediaOptions { Media = Media.Print }).WaitAsync(renderToken);

            return await page.PdfAsync(
                    new PagePdfOptions
                    {
                        Format = "Letter",
                        Landscape = true,
                        PrintBackground = true,
                        PreferCSSPageSize = false,
                        Scale = 0.8f,
                        Margin = new Margin
                        {
                            Top = PdfMargin,
                            Right = PdfMargin,
                            Bottom = PdfMargin,
                            Left = PdfMargin,
                        },
                    }
                )
                .WaitAsync(renderToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (timeoutCancellation.IsCancellationRequested)
        {
            throw new PdfRenderingException("The calendar PDF rendering operation timed out.", exception);
        }
        catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(PlaywrightSchedulePdfRenderer));
        }
        catch (PlaywrightException exception)
        {
            throw new PdfRenderingException("The calendar PDF could not be generated.", exception);
        }
        finally
        {
            if (browserContext is not null)
                await CloseBrowserContextAsync(browserContext);

            if (permitAcquired)
                renderSemaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _disposeCancellation.CancelAsync();

        using var renderDrainTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            if (_renderSemaphore is not null)
            {
                for (var permit = 0; permit < _maximumConcurrentRenders; permit++)
                    await _renderSemaphore.WaitAsync(renderDrainTimeout.Token);
            }
        }
        catch (OperationCanceledException exception)
        {
            logger.LogWarning(exception, "Timed out while waiting for active schedule PDF renders to stop.");
        }

        using var browserDisposalTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await _browserLaunchLock.WaitAsync(browserDisposalTimeout.Token);
            try
            {
                if (_browser is not null)
                    await _browser.CloseAsync().WaitAsync(browserDisposalTimeout.Token);

                _playwright?.Dispose();
                _browser = null;
                _playwright = null;
            }
            finally
            {
                _browserLaunchLock.Release();
            }
        }
        catch (OperationCanceledException exception)
        {
            logger.LogWarning(exception, "Timed out while closing the schedule PDF browser.");
        }
        finally
        {
            _disposeCancellation.Dispose();
        }
    }

    private SemaphoreSlim GetRenderSemaphore(int maximumConcurrentRenders)
    {
        lock (_renderSemaphoreLock)
        {
            if (_renderSemaphore is null)
            {
                _maximumConcurrentRenders = maximumConcurrentRenders;
                _renderSemaphore = new SemaphoreSlim(maximumConcurrentRenders, maximumConcurrentRenders);
            }

            return _renderSemaphore;
        }
    }

    private async Task CloseBrowserContextWhenReadyAsync(Task<IBrowserContext> contextTask)
    {
        try
        {
            await CloseBrowserContextAsync(await contextTask);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Playwright could not finish creating an isolated PDF browser context after rendering stopped."
            );
        }
    }

    private async Task CloseBrowserContextAsync(IBrowserContext browserContext)
    {
        try
        {
            await browserContext.CloseAsync().WaitAsync(CleanupTimeout, CancellationToken.None);
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(exception, "Timed out while closing an isolated PDF browser context.");
        }
        catch (PlaywrightException exception)
        {
            logger.LogWarning(exception, "Playwright could not close an isolated PDF browser context cleanly.");
        }
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser?.IsConnected == true)
            return _browser;

        await _browserLaunchLock.WaitAsync(cancellationToken);
        try
        {
            if (_browser?.IsConnected == true)
                return _browser;

            if (_browser is not null)
                await _browser.CloseAsync().WaitAsync(cancellationToken);
            _playwright?.Dispose();

            _playwright = await Playwright.CreateAsync().WaitAsync(cancellationToken);
            _browser = await _playwright
                .Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true })
                .WaitAsync(cancellationToken);
            return _browser;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _browser = null;
            _playwright?.Dispose();
            _playwright = null;
            throw new PdfRenderingException(
                "The PDF rendering service is unavailable.",
                exception,
                isServiceUnavailable: true
            );
        }
        finally
        {
            _browserLaunchLock.Release();
        }
    }

    private IReadOnlyCollection<Cookie> GetAuthenticationCookies(Uri targetUri)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            throw new PdfRenderingException("An authenticated request is required to render a calendar PDF.");

        var configuredCookieName = keycloakOptions.Value.CookieName;
        var environmentCookieName = configuredCookieName;
        if (environment.IsDevelopment())
            environmentCookieName += ".Development";

        var cookies = httpContext
            .Request.Cookies.Where(cookie => IsAuthenticationCookie(cookie.Key, environmentCookieName))
            .Select(cookie => new Cookie
            {
                Name = cookie.Key,
                Value = cookie.Value,
                Url = targetUri.GetLeftPart(UriPartial.Authority) + "/",
                HttpOnly = true,
                Secure = targetUri.Scheme == Uri.UriSchemeHttps,
                SameSite = targetUri.Scheme == Uri.UriSchemeHttps ? SameSiteAttribute.None : SameSiteAttribute.Lax,
            })
            .ToArray();

        if (!cookies.Any(cookie => cookie.Name == environmentCookieName))
            throw new PdfRenderingException("The authenticated session cookie is unavailable for PDF rendering.");

        return cookies;
    }

    private static bool IsAuthenticationCookie(string candidateName, string configuredName)
    {
        if (candidateName == configuredName)
            return true;

        if (!candidateName.StartsWith(configuredName, StringComparison.Ordinal))
            return false;

        var chunkSuffix = candidateName.AsSpan(configuredName.Length);
        return chunkSuffix.Length > 1
            && chunkSuffix[0] == 'C'
            && chunkSuffix[1..].IndexOfAnyExceptInRange('0', '9') < 0;
    }

    private static Uri BuildTrustedTargetUri(string configuredBaseUrl, SchedulePdfRenderRequest request)
    {
        if (request.LocationId <= 0 || request.EndDate <= request.StartDate)
            throw new PdfRenderingException("The calendar print target is invalid.");

        var baseUri = new Uri(EnsureTrailingSlash(configuredBaseUrl), UriKind.Absolute);
        var view = request.View switch
        {
            SchedulePdfView.Schedule => "schedule",
            SchedulePdfView.Assignments => "assignments",
            _ => throw new PdfRenderingException("The calendar print view is invalid."),
        };
        var period = request.Period switch
        {
            SchedulePdfPeriod.Day => "day",
            SchedulePdfPeriod.Week => "week",
            SchedulePdfPeriod.WorkWeek => "work-week",
            _ => throw new PdfRenderingException("The calendar print period is invalid."),
        };
        var relativeTarget = FormattableString.Invariant(
            $"calendar?view={view}&mode=print&locationId={request.LocationId}&startDate={request.StartDate:yyyy-MM-dd}&endDate={request.EndDate:yyyy-MM-dd}&period={period}"
        );

        return new Uri(baseUri, relativeTarget);
    }

    private static string EnsureTrailingSlash(string value) => value.EndsWith('/') ? value : value + "/";

    private static string GetConfigurationField(string failure)
    {
        var separatorIndex = failure.IndexOf(" must ", StringComparison.Ordinal);
        return separatorIndex > 0 ? failure[..separatorIndex] : "ScheduleDistribution";
    }
}
