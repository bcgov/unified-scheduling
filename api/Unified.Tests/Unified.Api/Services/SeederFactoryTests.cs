using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Api.Services;
using Unified.Common.Seeding;

namespace Unified.Tests.Api.Services;

public sealed class SeederFactoryTests
{
    [Fact]
    public async Task SeedAsync_RegisteredSeeders_ExecutesInOrderThenByName()
    {
        var executions = new List<string>();
        var factory = new SeederFactory<DbContext>(
            NullLogger<SeederFactory<DbContext>>.Instance,
            [
                new TestSeeder(2, "B", executions),
                new TestSeeder(1, "B", executions),
                new TestSeeder(1, "A", executions),
            ]
        );

        await factory.SeedAsync(null!, TestContext.Current.CancellationToken);

        Assert.Equal(["A", "B", "B"], executions);
    }

    private sealed class TestSeeder(int order, string name, List<string> executions)
        : SeederBase<DbContext>(NullLogger<TestSeeder>.Instance)
    {
        public override int Order => order;

        public override string Name => name;

        protected override Task ExecuteAsync(DbContext dbContext, CancellationToken cancellationToken)
        {
            executions.Add(Name);
            return Task.CompletedTask;
        }
    }
}