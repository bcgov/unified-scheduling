using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Unified.Common.Audit;
using Unified.Db.Models;

namespace Unified.Audit.Interceptors;

public sealed class AuditRecordInterceptor(
    ICurrentActorResolver actorResolver,
    IHttpContextAccessor httpContextAccessor,
    IOptions<AuditRecordInterceptorOptions> options
) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly List<DeferredAuditRecord> _deferredAudits = [];
    private bool _isWritingDeferredAuditRecords;
    private readonly AuditRecordInterceptorOptions _options = options.Value;
    private IDbContextTransaction? _ownedTransaction;

    // Sync SaveChanges bypasses this interceptor's async-only hooks, which would silently skip audit
    // capture. Fail fast instead so misuse is caught immediately rather than losing audit records.
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        throw new NotSupportedException(
            "Synchronous SaveChanges is not supported when AuditRecordInterceptor is registered. Use SaveChangesAsync instead. "
                + "If sync support is required, implement the sync SavingChanges/SavedChanges/SaveChangesFailed overrides."
        );
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is null || _isWritingDeferredAuditRecords)
        {
            return result;
        }

        CaptureAuditRecords(eventData.Context);
        await EnsureOwnedTransactionAsync(eventData.Context, cancellationToken);
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is null || _isWritingDeferredAuditRecords)
        {
            return result;
        }

        try
        {
            await PersistDeferredAuditRecordsAsync(eventData.Context, cancellationToken);
            await CommitOwnedTransactionAsync(cancellationToken);
        }
        catch
        {
            await RollbackOwnedTransactionAsync(cancellationToken);
            throw;
        }

        return result;
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        _deferredAudits.Clear();
        _isWritingDeferredAuditRecords = false;
        await RollbackOwnedTransactionAsync(cancellationToken);
    }

    private void CaptureAuditRecords(DbContext context)
    {
        _deferredAudits.Clear();
        context.ChangeTracker.DetectChanges();

        var actor = actorResolver.Resolve();
        var correlationId = ResolveCorrelationId();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (!ShouldAuditEntry(entry))
            {
                continue;
            }

            var auditRecord = BuildAuditRecord(entry, actor, correlationId);
            if (HasTemporaryKey(entry))
            {
                _deferredAudits.Add(new DeferredAuditRecord(entry, auditRecord));
                continue;
            }

            auditRecord.KeyValues = SerializeKeyValues(entry);
            context.Set<AuditRecord>().Add(auditRecord);
        }
    }

    private async Task EnsureOwnedTransactionAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (
            _deferredAudits.Count == 0
            || _ownedTransaction is not null
            || context.Database.CurrentTransaction is not null
        )
        {
            return;
        }

        _ownedTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
    }

    private async Task CommitOwnedTransactionAsync(CancellationToken cancellationToken)
    {
        if (_ownedTransaction is null)
        {
            return;
        }

        await _ownedTransaction.CommitAsync(cancellationToken);
        await _ownedTransaction.DisposeAsync();
        _ownedTransaction = null;
    }

    private async Task RollbackOwnedTransactionAsync(CancellationToken cancellationToken)
    {
        if (_ownedTransaction is null)
        {
            return;
        }

        await _ownedTransaction.RollbackAsync(cancellationToken);
        await _ownedTransaction.DisposeAsync();
        _ownedTransaction = null;
    }

    private async Task PersistDeferredAuditRecordsAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (_deferredAudits.Count == 0)
        {
            return;
        }

        _isWritingDeferredAuditRecords = true;
        try
        {
            foreach (var deferredAudit in _deferredAudits)
            {
                var entry = deferredAudit.Entry;
                deferredAudit.AuditRecord.KeyValues = SerializeKeyValues(entry);
                // NewValues was captured pre-save with EF's temporary key placeholder; re-serialize now
                // that the store-generated key has replaced it.
                deferredAudit.AuditRecord.NewValues = BuildValues(entry, oldValues: false, GetChangedColumns(entry));
                context.Set<AuditRecord>().Add(deferredAudit.AuditRecord);
            }

            _deferredAudits.Clear();
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _isWritingDeferredAuditRecords = false;
        }
    }

    private AuditRecord BuildAuditRecord(EntityEntry entry, CurrentActor actor, string? correlationId)
    {
        var changedColumns = GetChangedColumns(entry);

        return new AuditRecord
        {
            OccurredOn = DateTimeOffset.UtcNow,
            ActorUserId = actor.ActorUserId,
            ActorName = actor.ActorName,
            Action = entry.State.ToString(),
            EntityType = entry.Metadata.ClrType.Name,
            TableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
            KeyValues = "{}",
            OldValues = BuildValues(entry, oldValues: true, changedColumns),
            NewValues = BuildValues(entry, oldValues: false, changedColumns),
            ChangedColumns = changedColumns?.ToArray(),
            SourceModule = _options.SourceModule,
            CorrelationId = correlationId,
        };
    }

    private List<string>? GetChangedColumns(EntityEntry entry)
    {
        if (entry.State != EntityState.Modified)
        {
            return null;
        }

        return entry
            .Properties.Where(property => property.IsModified && !ShouldExcludeProperty(property))
            .Select(property => property.Metadata.Name)
            .ToList();
    }

    private string? BuildValues(EntityEntry entry, bool oldValues, IReadOnlyCollection<string>? changedColumns)
    {
        Dictionary<string, object?> values = [];

        foreach (var property in entry.Properties)
        {
            if (ShouldExcludeProperty(property))
            {
                continue;
            }

            var propertyName = property.Metadata.Name;
            if (
                entry.State == EntityState.Modified
                && changedColumns is not null
                && !changedColumns.Contains(propertyName)
            )
            {
                continue;
            }

            if (entry.State == EntityState.Added && oldValues)
            {
                continue;
            }

            if (entry.State == EntityState.Deleted && !oldValues)
            {
                continue;
            }

            values[propertyName] = oldValues ? property.OriginalValue : property.CurrentValue;
        }

        return values.Count == 0 ? null : JsonSerializer.Serialize(values, SerializerOptions);
    }

    private string SerializeKeyValues(EntityEntry entry)
    {
        var keyValues = entry
            .Properties.Where(property => property.Metadata.IsPrimaryKey())
            .ToDictionary(property => property.Metadata.Name, property => property.CurrentValue);

        return JsonSerializer.Serialize(keyValues, SerializerOptions);
    }

    private bool HasTemporaryKey(EntityEntry entry)
    {
        return entry.Properties.Any(property => property.Metadata.IsPrimaryKey() && property.IsTemporary);
    }

    private bool ShouldAuditEntry(EntityEntry entry)
    {
        if (entry.Entity is AuditRecord)
        {
            return false;
        }

        if (entry.State is EntityState.Detached or EntityState.Unchanged)
        {
            return false;
        }

        return entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;
    }

    private bool ShouldExcludeProperty(PropertyEntry property)
    {
        if (property.Metadata.ClrType == typeof(byte[]))
        {
            return true;
        }

        if (property.Metadata.PropertyInfo?.GetCustomAttribute<AuditExcludeAttribute>() is not null)
        {
            return true;
        }

        var propertyName = property.Metadata.Name;

        if (
            _options.ExcludedPropertyNames.Any(excluded =>
                string.Equals(excluded, propertyName, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return true;
        }

        if (
            _options.ExcludedPropertyNameContains.Any(pattern =>
                propertyName.Contains(pattern, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return true;
        }

        return _options.ExcludedPropertyNameEndsWith.Any(suffix =>
            propertyName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
        );
    }

    private string? ResolveCorrelationId()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        foreach (var headerName in _options.CorrelationIdHeaderNames)
        {
            if (httpContext.Request.Headers.TryGetValue(headerName, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.ToString();
            }
        }

        // Falls back to ASP.NET Core's per-request identifier, matching the traceId GlobalExceptionHandler returns to clients.
        return httpContext.TraceIdentifier;
    }

    private sealed record DeferredAuditRecord(EntityEntry Entry, AuditRecord AuditRecord);
}
