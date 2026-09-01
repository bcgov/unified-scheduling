using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Unified.Audit;
using Unified.Audit.Services;
using Unified.Db;

namespace Unified.Tests.Unified.Audit.Services;

public class AuditSchemaServiceTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private AuditSchemaService _service = null!;

    public ValueTask InitializeAsync()
    {
        var dbOptions = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(dbOptions);
        _service = new AuditSchemaService(
            _dbContext,
            Options.Create(new AuditRecordOptions()),
            new MemoryCache(new MemoryCacheOptions())
        );

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public void EntityTypeExists_When_Known_Type_Should_Return_True()
    {
        Assert.True(_service.EntityTypeExists("User"));
    }

    [Fact]
    public void EntityTypeExists_Should_Be_Case_Insensitive()
    {
        Assert.True(_service.EntityTypeExists("user"));
    }

    [Fact]
    public void EntityTypeExists_When_Unknown_Type_Should_Return_False()
    {
        Assert.False(_service.EntityTypeExists("NotAnEntity"));
    }

    [Fact]
    public void GetFields_When_Unknown_Type_Should_Return_Null()
    {
        Assert.Null(_service.GetFields("NotAnEntity"));
    }

    [Fact]
    public void GetFields_Should_Exclude_AuditExclude_Attributed_Properties()
    {
        var fields = _service.GetFields("User");

        Assert.NotNull(fields);
        Assert.DoesNotContain(fields.Fields, f => f.Name == "IdirId");
        Assert.DoesNotContain(fields.Fields, f => f.Name == "KeyCloakId");
    }

    [Fact]
    public void GetFields_Should_Exclude_Byte_Array_Properties()
    {
        var fields = _service.GetFields("User");

        Assert.NotNull(fields);
        Assert.DoesNotContain(fields.Fields, f => f.Name == "Photo");
    }

    [Fact]
    public void GetFields_Should_Include_Fields_Ordered_By_Name()
    {
        var fields = _service.GetFields("User");

        Assert.NotNull(fields);
        var names = fields.Fields.Select(f => f.Name).ToList();
        var sorted = names.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, names);
    }

    [Fact]
    public void GetFields_Should_Resolve_Expected_Field_Types()
    {
        var fields = _service.GetFields("User");

        Assert.NotNull(fields);
        var byName = fields.Fields.ToDictionary(f => f.Name);

        Assert.Equal("string", byName["FirstName"].Type);
        Assert.Equal("boolean", byName["IsEnabled"].Type);
        Assert.Equal("uuid", byName["Id"].Type);
    }

    [Fact]
    public void GetFields_Should_Split_PascalCase_Name_Into_Label_When_No_Display_Attribute()
    {
        var fields = _service.GetFields("User");

        Assert.NotNull(fields);
        var firstName = fields.Fields.Single(f => f.Name == "FirstName");
        Assert.Equal("First Name", firstName.Label);
    }

    [Fact]
    public void GetFields_Should_Return_Same_Cached_Result_On_Second_Call()
    {
        var first = _service.GetFields("User");
        var second = _service.GetFields("User");

        Assert.Same(first, second);
    }
}
