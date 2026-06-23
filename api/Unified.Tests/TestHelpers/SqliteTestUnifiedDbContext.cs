using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Unified.Db;
using Unified.Db.Models.Abstract;

namespace Unified.Tests.TestHelpers;

internal sealed class SqliteTestUnifiedDbContext(DbContextOptions<UnifiedDbContext> options) : UnifiedDbContext(options)
{
    private static readonly ValueConverter<DateTimeOffset, string> DateTimeOffsetConverter = new(
        value => value.ToString("O"),
        value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
    );

    private static readonly ValueConverter<DateTimeOffset?, string?> NullableDateTimeOffsetConverter = new(
        value => value.HasValue ? value.Value.ToString("O") : null,
        value =>
            value == null
                ? null
                : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
    );

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyAllConfigurations(typeof(UnifiedDbContext).Assembly, this);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(DateTimeOffsetConverter);
                    property.SetColumnType("TEXT");
                }

                if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(NullableDateTimeOffsetConverter);
                    property.SetColumnType("TEXT");
                }
            }

            var concurrencyProperty = entityType.FindProperty(nameof(BaseEntity.ConcurrencyToken));
            if (concurrencyProperty is null)
            {
                continue;
            }

            concurrencyProperty.ValueGenerated = ValueGenerated.Never;
            concurrencyProperty.SetDefaultValue(0u);
            concurrencyProperty.IsConcurrencyToken = false;
        }
    }
}
