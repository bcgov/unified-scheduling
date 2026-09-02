namespace Unified.Tests.TestHelpers;

/// <summary>Test helper: configures <see cref="SimulatedDbFailureInterceptor"/>.</summary>
public sealed class SimulatedDbFailureOptions
{
    public bool Enabled { get; set; }

    /// <summary>Unquoted table name to fail inserts against, e.g. "Users".</summary>
    public string TableName { get; set; } = string.Empty;
}
