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
    public async Task SaveChangesAsync_NoCorrelationHeader_FallsBackToTraceIdentifier()
    {
        await using var context = CreateContext(CreateAuditInterceptor(withHttpContextWithoutCorrelationHeader: true));
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.AuditedEntities.Add(new AuditedEntity { Name = "new entity", Notes = "created" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auditRecord = await context.AuditRecords.SingleAsync(TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(auditRecord.CorrelationId));
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
    public async Task SaveChangesAsync_AddedEntityWithGeneratedKey_NewValuesMatchesFinalKeyValue()
    {
        await using var context = CreateContext(CreateAuditInterceptor());
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // No explicit ID -> temporary key -> deferred audit path.
        var entity = new AuditedEntity { Name = "generated-key-entity" };
        context.AuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auditRecord = await context.AuditRecords.SingleAsync(TestContext.Current.CancellationToken);
        var keyValues = Deserialize(auditRecord.KeyValues);
        var newValues = Deserialize(auditRecord.NewValues!);

        Assert.Equal(entity.Id, keyValues[nameof(AuditedEntity.Id)].GetInt32());
        Assert.Equal(entity.Id, newValues[nameof(AuditedEntity.Id)].GetInt32());
    }

    [Fact]
    public async Task SaveChangesAsync_ChildWithKnownKeyAndTemporaryParentFk_NewValuesMatchesFinalParentId()
    {
        await using var context = CreateContext(CreateAuditInterceptor());
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Parent has a generated (temporary-at-capture) key. Child's own key is explicit/known up front,
        // so only its ParentId FK is temporary when CaptureAuditRecords runs - this must still defer.
        var parent = new AuditedParentEntity { Name = "parent" };
        var child = new AuditedChildEntity
        {
            Id = 500,
            Description = "child",
            Parent = parent,
        };
        context.AuditedParentEntities.Add(parent);
        context.AuditedChildEntities.Add(child);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var childAuditRecord = await context
            .AuditRecords.Where(r => r.EntityType == nameof(AuditedChildEntity))
            .SingleAsync(TestContext.Current.CancellationToken);
        var newValues = Deserialize(childAuditRecord.NewValues!);

        Assert.Equal(parent.Id, newValues[nameof(AuditedChildEntity.ParentId)].GetInt32());
    }

    [Fact]
    public async Task SaveChangesAsync_ExistingChildReparentedToNewParent_NewValuesMatchesFinalParentId()
    {
        var interceptor = CreateAuditInterceptor();
        await using var context = CreateContext(interceptor);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var originalParent = new AuditedParentEntity { Name = "original parent" };
        var child = new AuditedChildEntity
        {
            Id = 501,
            Description = "child",
            Parent = originalParent,
        };
        context.AuditedParentEntities.Add(originalParent);
        context.AuditedChildEntities.Add(child);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.AuditRecords.RemoveRange(context.AuditRecords);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Child's own key is already persisted/known; only its FK is re-pointed at a brand-new parent
        // in the same SaveChanges batch - this is the Modified-state analogue of the Added-state test above.
        var newParent = new AuditedParentEntity { Name = "new parent" };
        context.AuditedParentEntities.Add(newParent);
        child.Parent = newParent;

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var childAuditRecord = await context
            .AuditRecords.Where(r => r.EntityType == nameof(AuditedChildEntity) && r.Action == "Modified")
            .SingleAsync(TestContext.Current.CancellationToken);
        var newValues = Deserialize(childAuditRecord.NewValues!);

        Assert.Equal(newParent.Id, newValues[nameof(AuditedChildEntity.ParentId)].GetInt32());
    }

    [Fact]
    public async Task SaveChangesAsync_ThreeLevelDeepGraphWithMixedKeyStates_ResolvesFksAtEveryLevel()
    {
        await using var context = CreateContext(CreateAuditInterceptor());
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // GrandParent and Parent both have generated (temporary-at-capture) keys; Child's own key is
        // explicit/known. Verifies deferral and FK resolution cascade through every level, not just one.
        var grandParent = new AuditedGrandParentEntity { Name = "grandparent" };
        var parent = new AuditedParentEntity { Name = "parent", GrandParent = grandParent };
        var child = new AuditedChildEntity
        {
            Id = 502,
            Description = "child",
            Parent = parent,
        };
        context.AuditedGrandParentEntities.Add(grandParent);
        context.AuditedParentEntities.Add(parent);
        context.AuditedChildEntities.Add(child);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var parentAuditRecord = await context
            .AuditRecords.Where(r => r.EntityType == nameof(AuditedParentEntity))
            .SingleAsync(TestContext.Current.CancellationToken);
        var childAuditRecord = await context
            .AuditRecords.Where(r => r.EntityType == nameof(AuditedChildEntity))
            .SingleAsync(TestContext.Current.CancellationToken);

        var parentNewValues = Deserialize(parentAuditRecord.NewValues!);
        var childNewValues = Deserialize(childAuditRecord.NewValues!);

        Assert.Equal(grandParent.Id, parentNewValues[nameof(AuditedParentEntity.GrandParentId)].GetInt32());
        Assert.Equal(parent.Id, childNewValues[nameof(AuditedChildEntity.ParentId)].GetInt32());
    }

    [Fact]
    public async Task SaveChangesAsync_ChildWithTwoTemporaryFksToDifferentNewParents_NewValuesMatchesBothFinalIds()
    {
        await using var context = CreateContext(CreateAuditInterceptor());
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Mirrors real join entities in this codebase (e.g. RolePermission, UserRole) that have their
        // own known key plus two independent FKs, both temporary when both principals are new.
        var parent = new AuditedParentEntity { Name = "parent" };
        var secondParent = new AuditedGrandParentEntity { Name = "second-parent" };
        var child = new AuditedChildEntity
        {
            Id = 503,
            Description = "join-row",
            Parent = parent,
            SecondParent = secondParent,
        };
        context.AuditedParentEntities.Add(parent);
        context.AuditedGrandParentEntities.Add(secondParent);
        context.AuditedChildEntities.Add(child);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var childAuditRecord = await context
            .AuditRecords.Where(r => r.EntityType == nameof(AuditedChildEntity))
            .SingleAsync(TestContext.Current.CancellationToken);
        var newValues = Deserialize(childAuditRecord.NewValues!);

        Assert.Equal(parent.Id, newValues[nameof(AuditedChildEntity.ParentId)].GetInt32());
        Assert.Equal(secondParent.Id, newValues[nameof(AuditedChildEntity.SecondParentId)].GetInt32());
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
        string sourceModule = "test-module",
        bool withHttpContextWithoutCorrelationHeader = false
    )
    {
        var accessor = new HttpContextAccessor();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Correlation-Id"] = correlationId;
            accessor.HttpContext = context;
        }
        else if (withHttpContextWithoutCorrelationHeader)
        {
            accessor.HttpContext = new DefaultHttpContext();
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

        public DbSet<AuditedGrandParentEntity> AuditedGrandParentEntities => Set<AuditedGrandParentEntity>();

        public DbSet<AuditedParentEntity> AuditedParentEntities => Set<AuditedParentEntity>();

        public DbSet<AuditedChildEntity> AuditedChildEntities => Set<AuditedChildEntity>();

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
            modelBuilder.Entity<AuditedGrandParentEntity>(builder =>
            {
                builder.ToTable("AuditedGrandParentEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
            });
            modelBuilder.Entity<AuditedParentEntity>(builder =>
            {
                builder.ToTable("AuditedParentEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder.HasOne(entity => entity.GrandParent).WithMany().HasForeignKey(entity => entity.GrandParentId);
            });
            modelBuilder.Entity<AuditedChildEntity>(builder =>
            {
                builder.ToTable("AuditedChildEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedNever();
                builder.HasOne(entity => entity.Parent).WithMany().HasForeignKey(entity => entity.ParentId);
                builder
                    .HasOne(entity => entity.SecondParent)
                    .WithMany()
                    .HasForeignKey(entity => entity.SecondParentId);
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

    private sealed class AuditedGrandParentEntity
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class AuditedParentEntity
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int? GrandParentId { get; set; }

        public AuditedGrandParentEntity? GrandParent { get; set; }
    }

    private sealed class AuditedChildEntity
    {
        [Key]
        public int Id { get; set; }

        public int ParentId { get; set; }

        public AuditedParentEntity? Parent { get; set; }

        public int? SecondParentId { get; set; }

        public AuditedGrandParentEntity? SecondParent { get; set; }

        public string Description { get; set; } = string.Empty;
    }
}
