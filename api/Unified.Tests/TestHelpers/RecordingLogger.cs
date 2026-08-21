using Microsoft.Extensions.Logging;

namespace Unified.Tests.TestHelpers;

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<RecordingLogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        Entries.Add(new RecordingLogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose() { }
    }
}

internal sealed record RecordingLogEntry(LogLevel Level, string Message, Exception? Exception);
