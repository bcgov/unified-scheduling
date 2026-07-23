using Unified.Db.Extensions;
using Unified.Db.Models.Abstract;

namespace Unified.Tests.Unified.Db.Extensions;

public class QueryExtensionsTests
{
    private sealed class SoftDeletableEntity : ISoftDeletable
    {
        public int Id { get; init; }
        public DateTimeOffset? DeletedOn { get; set; }
        public Guid? DeletedById { get; set; }
    }

    private static IQueryable<SoftDeletableEntity> BuildQuery(params SoftDeletableEntity[] items) =>
        items.AsQueryable();

    [Fact]
    public void WhereActive_ExcludesEntitiesWithDeletedOnSet()
    {
        var query = BuildQuery(
            new SoftDeletableEntity { Id = 1, DeletedOn = DateTimeOffset.UtcNow },
            new SoftDeletableEntity { Id = 2 }
        );

        var result = query.WhereActive().ToList();

        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void WhereActive_ExcludesEntitiesWithDeletedByIdSet()
    {
        var query = BuildQuery(
            new SoftDeletableEntity { Id = 1, DeletedById = Guid.NewGuid() },
            new SoftDeletableEntity { Id = 2 }
        );

        var result = query.WhereActive().ToList();

        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void WhereActive_ExcludesEntitiesWithBothFieldsSet()
    {
        var query = BuildQuery(
            new SoftDeletableEntity
            {
                Id = 1,
                DeletedOn = DateTimeOffset.UtcNow,
                DeletedById = Guid.NewGuid(),
            },
            new SoftDeletableEntity { Id = 2 }
        );

        var result = query.WhereActive().ToList();

        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void WhereActive_ReturnsAllWhenNoneAreDeleted()
    {
        var query = BuildQuery(new SoftDeletableEntity { Id = 1 }, new SoftDeletableEntity { Id = 2 });

        var result = query.WhereActive().ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void WhereActive_ReturnsEmptyWhenAllAreDeleted()
    {
        var query = BuildQuery(
            new SoftDeletableEntity { Id = 1, DeletedOn = DateTimeOffset.UtcNow },
            new SoftDeletableEntity { Id = 2, DeletedById = Guid.NewGuid() }
        );

        var result = query.WhereActive().ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void SoftDelete_SetsDeletedOnAndDeletedById()
    {
        var entity = new SoftDeletableEntity { Id = 1 };
        var deletedById = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        entity.SoftDelete(deletedById);

        var after = DateTimeOffset.UtcNow;

        Assert.Equal(deletedById, entity.DeletedById);
        Assert.NotNull(entity.DeletedOn);
        Assert.InRange(entity.DeletedOn!.Value, before, after);
    }
}
