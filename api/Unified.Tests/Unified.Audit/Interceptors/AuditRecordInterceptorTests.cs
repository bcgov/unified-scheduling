using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Audit;
using Unified.Audit.Interceptors;
using Unified.Common.Audit;
using Unified.Common.Interceptors;
using Unified.Db.Configuration;
using Unified.Db.Models;
using Xunit;

namespace Unified.Tests.Unified.Audit.Interceptors;

public class AuditRecordInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public AuditRecordInterceptorTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    [Fact]
    public async Task SaveChangesAsync_AddedEntity_CreatesAuditRecordWithExpectedFields()
    {
        var userId = Guid.NewGuid();
        await using var context = CreateContext(
            CreateAuditInterceptor(
                actorResolver: new FakeActorResolver(new CurrentActor(userId, "Robin Reviewer")),
                correlationId: "corr-123",
                sourceModule: "unit-tests"
            )
        );

        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.AuditedEntities.Add(new AuditedEntity { Name = "new entity", Notes = "created" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auditRecord = await context.AuditRecords.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Added", auditRecord.Action);
        Assert.Equal(nameof(AuditedEntity), auditRecord.EntityType);
        Assert.Equal("AuditedEntities", auditRecord.TableName);
        Assert.Equal(userId, auditRecord.ActorUserId);
        Assert.Equal("Robin Reviewer", auditRecord.ActorName);
        Assert.Equal("unit-tests", auditRecord.SourceModule);
        Assert.Equal("corr-123", auditRecord.CorrelationId);
        Assert.NotNull(auditRecord.NewValues);
        Assert.NotEqual("{}", auditRecord.KeyValues);

        var newValues = Deserialize(auditRecord.NewValues);
        Assert.Equal("new entity", newValues[nameof(AuditedEntity.Name)].GetString());
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedEntity_CapturesOldNewAndChangedColumns()
    {
        await using var context = CreateContext(CreateAuditInterceptor());
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var entity = new AuditedEntity { Name = "before", Notes = "old" };
        context.AuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.AuditRecords.RemoveRange(context.AuditRecords);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.Name = "after";
        entity.Notes = "new";
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auditRecord = await context.AuditRecords.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Modified", auditRecord.Action);
        Assert.NotNull(auditRecord.OldValues);
        Assert.NotNull(auditRecord.NewValues);
        Assert.NotNull(auditRecord.ChangedColumns);
        Assert.Contains(nameof(AuditedEntity.Name), auditRecord.ChangedColumns);
        Assert.Contains(nameof(AuditedEntity.Notes), auditRecord.ChangedColumns);

        var oldValues = Deserialize(auditRecord.OldValues);
        var newValues = Deserialize(auditRecord.NewValues);

        Assert.Equal("before", oldValues[nameof(AuditedEntity.Name)].GetString());
        Assert.Equal("after", newValues[nameof(AuditedEntity.Name)].GetString());
    }

    [Fact]
    public async Task SaveChangesAsync_DeletedEntity_CapturesOldValuesOnly()
    {
        await using var context = CreateContext(CreateAuditInterceptor());
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var entity = new AuditedEntity { Name = "delete-me" };
        context.AuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.AuditRecords.RemoveRange(context.AuditRecords);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.AuditedEntities.Remove(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auditRecord = await context.AuditRecords.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Deleted", auditRecord.Action);
        Assert.NotNull(auditRecord.OldValues);
        Assert.Null(auditRecord.NewValues);
    }

    [Fact]
    public async Task SaveChangesAsync_DenyListFields_AreExcludedFromPayloads()
    {
        await using var context = CreateContext(CreateAuditInterceptor());
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.AuditedEntities.Add(
            new AuditedEntity
            {
                Name = "sensitive",
                ApiToken = "do-not-store",
                Photo = [1, 2, 3],
                ConcurrencyToken = 22,
            }
        );

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auditRecord = await context.AuditRecords.SingleAsync(TestContext.Current.CancellationToken);
        var newValues = Deserialize(auditRecord.NewValues!);

        Assert.DoesNotContain(nameof(AuditedEntity.ApiToken), newValues.Keys);
        Assert.DoesNotContain(nameof(AuditedEntity.Photo), newValues.Keys);
        Assert.DoesNotContain(nameof(AuditedEntity.ConcurrencyToken), newValues.Keys);
    }

    [Fact]
    public async Task SaveChangesAsync_AuditExcludeProperties_AreExcludedFromPayloads()
    {
        await using var context = CreateContext(CreateAuditInterceptor());
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.AuditedEntities.Add(new AuditedEntity { Name = "test", InternalOnly = "skip-me" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auditRecord = await context.AuditRecords.SingleAsync(TestContext.Current.CancellationToken);
        var newValues = Deserialize(auditRecord.NewValues!);

        Assert.DoesNotContain(nameof(AuditedEntity.InternalOnly), newValues.Keys);
    }

    [Fact]
    public async Task SaveChanges_Sync_ThrowsNotSupportedException()
    {
        await using var context = CreateContext(CreateAuditInterceptor());
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.AuditedEntities.Add(new AuditedEntity { Name = "sync-attempt" });

        Assert.Throws<NotSupportedException>(() => context.SaveChanges());
    }

    [Fact]
    public async Task SaveChangesAsync_NoTrackedChanges_DoesNotCreateAuditRecord()
    {
        await using var context = CreateContext(CreateAuditInterceptor());
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(context.AuditRecords);
    }

    [Fact]
    public async Task SaveChangesAsync_NoHttpUser_UsesSystemActor()
    {
        var actorResolver = new HttpContextActorResolver(new HttpContextAccessor());
        await using var context = CreateContext(CreateAuditInterceptor(actorResolver: actorResolver));
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.AuditedEntities.Add(new AuditedEntity { Name = "system-change" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auditRecord = await context.AuditRecords.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Guid.Empty, auditRecord.ActorUserId);
        Assert.Equal("system", auditRecord.ActorName);
    }

    [Fact]
    public async Task SaveChangesAsync_AuditRecordEntity_DoesNotCreateRecursiveAuditRecord()
    {
        await using var context = CreateContext(CreateAuditInterceptor());
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.AuditRecords.Add(
            new AuditRecord
            {
                OccurredOn = DateTimeOffset.UtcNow,
                Action = "Added",
                EntityType = "Manual",
                TableName = "Manual",
                KeyValues = "{}",
            }
        );

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Single(context.AuditRecords);
    }

    [Fact]
    public async Task SaveChangesAsync_WithSaveRulesInterceptor_StillExecutesRules()
    {
        var saveRule = new CountingSaveRule();
        var saveRulesInterceptor = new SaveRulesInterceptor([saveRule], NullLogger<SaveRulesInterceptor>.Instance);

        await using var context = CreateContext(saveRulesInterceptor, CreateAuditInterceptor());
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Explicit ID avoids temporary-key deferral and exercises a single SaveChanges pipeline.
        context.AuditedEntities.Add(new AuditedEntity { Id = 123, Name = "coexist" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, saveRule.ExecutionCount);
        Assert.Single(context.AuditRecords);
    }

    [Fact]
    public async Task SaveChangesAsync_DeferredKeyFailure_RollsBackEntityAndAuditInAmbientTransaction()
    {
        var throwInterceptor = new ThrowOnAuditInsertCommandInterceptor();

        var interceptor = CreateAuditInterceptor();
        await using var context = CreateContext(interceptor, throwInterceptor);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.AuditedEntities.Add(new AuditedEntity { Name = "rollback-me" });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            context.SaveChangesAsync(TestContext.Current.CancellationToken)
        );
        Assert.IsType<InvalidOperationException>(exception.InnerException);

        await using var verifyContext = CreateContext(CreateAuditInterceptor());

        var entityCount = await verifyContext.AuditedEntities.CountAsync(TestContext.Current.CancellationToken);
        var auditCount = await verifyContext.AuditRecords.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, entityCount);
        Assert.Equal(0, auditCount);
    }

    [Fact]
    public async Task SaveChangesAsync_EntityInsertFailure_DoesNotCreateAuditRecord()
    {
        var throwInterceptor = new ThrowOnEntityInsertCommandInterceptor();

        await using var context = CreateContext(CreateAuditInterceptor(), throwInterceptor);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Explicit ID avoids temporary-key deferral, so entity and audit insert share one SaveChanges call.
        context.AuditedEntities.Add(new AuditedEntity { Id = 456, Name = "will-fail" });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            context.SaveChangesAsync(TestContext.Current.CancellationToken)
        );

        await using var verifyContext = CreateContext(CreateAuditInterceptor());

        var entityCount = await verifyContext.AuditedEntities.CountAsync(TestContext.Current.CancellationToken);
        var auditCount = await verifyContext.AuditRecords.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, entityCount);
        Assert.Equal(0, auditCount);
    }

    [Fact]
    public async Task SaveChangesAsync_EntityInsertFailureWithGeneratedKey_DoesNotCreateAuditRecord()
    {
        var throwInterceptor = new ThrowOnEntityInsertCommandInterceptor();

        await using var context = CreateContext(CreateAuditInterceptor(), throwInterceptor);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // No explicit ID -> temporary key -> deferred audit path, still exercised before the entity insert fails.
        context.AuditedEntities.Add(new AuditedEntity { Name = "will-fail-generated" });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            context.SaveChangesAsync(TestContext.Current.CancellationToken)
        );

        await using var verifyContext = CreateContext(CreateAuditInterceptor());

        var entityCount = await verifyContext.AuditedEntities.CountAsync(TestContext.Current.CancellationToken);
        var auditCount = await verifyContext.AuditRecords.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, entityCount);
        Assert.Equal(0, auditCount);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private TestAuditDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptors)
            .Options;

        return new TestAuditDbContext(options);
    }

    private static AuditRecordInterceptor CreateAuditInterceptor(
        ICurrentActorResolver? actorResolver = null,
        string? correlationId = null,
        string sourceModule = "test-module"
    )
    {
        var accessor = new HttpContextAccessor();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Correlation-Id"] = correlationId;
            accessor.HttpContext = context;
        }

        var options = Options.Create(
            new AuditRecordInterceptorOptions
            {
                SourceModule = sourceModule,
                ExcludedPropertyNames = ["xmin", "ConcurrencyToken"],
                ExcludedPropertyNameContains = ["Password", "Token", "Secret"],
                ExcludedPropertyNameEndsWith = ["Key"],
            }
        );

        return new AuditRecordInterceptor(
            actorResolver ?? new FakeActorResolver(new CurrentActor(Guid.NewGuid(), "default-actor")),
            accessor,
            options
        );
    }

    private static Dictionary<string, JsonElement> Deserialize(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
    }

    private sealed class FakeActorResolver(CurrentActor actor) : ICurrentActorResolver
    {
        public CurrentActor Resolve() => actor;
    }

    private sealed class CountingSaveRule : ISaveRule
    {
        public int ExecutionCount { get; private set; }

        public Task ExecuteAsync(DbContext context, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowOnAuditInsertCommandInterceptor : DbCommandInterceptor
    {
        private const string AuditInsertSqlFragment = "INSERT INTO \"AuditRecords\"";

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result
        )
        {
            ThrowIfAuditInsert(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfAuditInsert(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private static void ThrowIfAuditInsert(DbCommand command)
        {
            if (command.CommandText.Contains(AuditInsertSqlFragment, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Simulated deferred audit insert failure");
            }
        }
    }

    private sealed class ThrowOnEntityInsertCommandInterceptor : DbCommandInterceptor
    {
        private const string EntityInsertSqlFragment = "INSERT INTO \"AuditedEntities\"";

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result
        )
        {
            ThrowIfEntityInsert(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfEntityInsert(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private static void ThrowIfEntityInsert(DbCommand command)
        {
            if (command.CommandText.Contains(EntityInsertSqlFragment, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Simulated entity insert failure");
            }
        }
    }

    private sealed class TestAuditDbContext(DbContextOptions<TestAuditDbContext> options) : DbContext(options)
    {
        public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

        public DbSet<AuditedEntity> AuditedEntities => Set<AuditedEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new AuditRecordConfiguration());
            modelBuilder.Entity<AuditedEntity>(builder =>
            {
                builder.ToTable("AuditedEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
            });
        }
    }

    private sealed class AuditedEntity
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Notes { get; set; }

        public string? ApiToken { get; set; }

        public byte[]? Photo { get; set; }

        public uint ConcurrencyToken { get; set; }

        [AuditExclude]
        public string? InternalOnly { get; set; }
    }
}
