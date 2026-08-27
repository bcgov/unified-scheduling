namespace Unified.Db.Audit;

/// <summary>
/// Excludes a property from audit diff capture.
/// Use for binary blobs, concurrency tokens, and derived/computed fields.
/// Not intended as a PII safeguard — access control governs who can read audit records.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class AuditExcludeAttribute : Attribute { }
