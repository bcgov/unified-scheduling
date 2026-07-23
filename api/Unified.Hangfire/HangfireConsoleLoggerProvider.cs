using Hangfire.Console;
using Microsoft.Extensions.Logging;

namespace Unified.Hangfire;

/// <summary>
/// Bridges standard <c>ILogger</c> output into the Hangfire dashboard's per-job Console tab.
/// Only forwards log entries while a job is executing (i.e. when <see cref="HangfireConsoleContext.Current"/>
/// is set by <see cref="HangfireConsoleLoggingFilter"/>) - outside of job execution this provider is a no-op,
/// so it's safe to register globally alongside the app's normal logging providers.
///
/// Framework/infrastructure categories (e.g. <c>Microsoft.EntityFrameworkCore.*</c>, which logs every SQL
/// command at Information) are suppressed below Warning to avoid flooding a job's console output - and
/// therefore the "hangfire" schema's storage - with noise on every database call a job makes.
/// </summary>
public sealed class HangfireConsoleLoggerProvider : ILoggerProvider
{
    private static readonly string[] NoisyCategoryPrefixes = ["Microsoft.", "System."];

    public ILogger CreateLogger(string categoryName) => new HangfireConsoleLogger(categoryName);

    public void Dispose() { }

    private sealed class HangfireConsoleLogger(string categoryName) : ILogger
    {
        private readonly bool _isNoisyCategory = NoisyCategoryPrefixes.Any(prefix =>
            categoryName.StartsWith(prefix, StringComparison.Ordinal)
        );

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel == LogLevel.None || HangfireConsoleContext.Current is null)
                return false;

            // Suppress routine framework chatter (e.g. EF Core's per-query command logging) unless
            // it's a Warning or above - keeps the dashboard's Console tab focused on the job's own
            // logs and prevents unbounded growth of the per-job console data in storage.
            return !_isNoisyCategory || logLevel >= LogLevel.Warning;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            var context = HangfireConsoleContext.Current;
            if (context is null || !IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (exception is not null)
            {
                message = $"{message}{Environment.NewLine}{exception}";
            }

            var line = $"[{logLevel}] {categoryName}: {message}";

            switch (logLevel)
            {
                case LogLevel.Critical:
                case LogLevel.Error:
                    context.WriteLine(ConsoleTextColor.Red, line);
                    break;
                case LogLevel.Warning:
                    context.WriteLine(ConsoleTextColor.Yellow, line);
                    break;
                default:
                    context.WriteLine(line);
                    break;
            }
        }
    }
}
