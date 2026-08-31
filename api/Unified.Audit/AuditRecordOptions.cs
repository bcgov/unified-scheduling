namespace Unified.Audit;

public sealed class AuditRecordOptions
{
    public const string SectionName = "AuditRecordInterceptor";

    public string SourceModule { get; set; } = "api";

    public string[] CorrelationIdHeaderNames { get; set; } = ["X-Correlation-Id", "X-Correlation-ID", "X-Request-Id"];

    public string[] ExcludedPropertyNames { get; set; } = ["xmin", "ConcurrencyToken"];

    /// <summary>Property names containing any of these substrings are excluded (e.g. "ApiToken" matches "Token").</summary>
    public string[] ExcludedPropertyNameContains { get; set; } = ["Password", "Token", "Secret"];

    /// <summary>Property names ending with any of these suffixes are excluded (e.g. "ApiKey" matches "Key", but "KeyCloakId" does not).</summary>
    public string[] ExcludedPropertyNameEndsWith { get; set; } = ["Key"];
}
