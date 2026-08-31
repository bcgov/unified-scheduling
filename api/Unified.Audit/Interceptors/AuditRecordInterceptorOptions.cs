namespace Unified.Audit.Interceptors;

public sealed class AuditRecordOptions
{
    public const string SectionName = "AuditRecord";

    public string[] ExcludedPropertyNames { get; set; } = ["xmin", "ConcurrencyToken", "IdirId", "KeyCloakId"];
}
