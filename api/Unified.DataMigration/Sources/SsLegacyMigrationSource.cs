using System.Data.Common;
using Microsoft.Extensions.Options;
using Npgsql;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Options;

namespace Unified.DataMigration.Sources;

public sealed class SsLegacyMigrationSource(
    IOptions<DataMigrationFeatureFlags> featureFlags,
    IOptions<DataMigrationOptions> options
) : ISsLegacyMigrationSource
{
    private readonly Func<string, DbConnection> _connectionFactory = connectionString => new NpgsqlConnection(
        connectionString
    );

    internal SsLegacyMigrationSource(
        IOptions<DataMigrationFeatureFlags> featureFlags,
        IOptions<DataMigrationOptions> options,
        Func<string, DbConnection> connectionFactory
    )
        : this(featureFlags, options)
    {
        _connectionFactory = connectionFactory;
    }

    private static readonly (string Table, string Query, Type Projection)[] ReferenceProjections =
    [
        (
            "Region",
            """
            SELECT "Id", "JustinId", "Code", "Name", "ExpiryDate", "CreatedOn", "UpdatedOn"
            FROM "public"."Region" LIMIT 0
            """,
            typeof(SsRegionDto)
        ),
        (
            "Location",
            """
            SELECT "Id", "AgencyId", "Name", "JustinCode", "ParentLocationId", "RegionId",
                   "Timezone", "ExpiryDate", "CreatedOn", "UpdatedOn"
            FROM "public"."Location" LIMIT 0
            """,
            typeof(SsLocationDto)
        ),
        (
            "LookupCode",
            """
            SELECT "Id", "Type", "Code", "SubCode", "Description", "EffectiveDate", "ExpiryDate",
                   "LocationId", "Mandatory", "ValidityPeriod", "Category", "AdvanceNotice", "Rotating",
                   "CreatedOn", "UpdatedOn"
            FROM "public"."LookupCode" LIMIT 0
            """,
            typeof(SsLookupCodeDto)
        ),
        (
            "LookupSortOrder",
            """
            SELECT "Id", "LookupCodeId", "LocationId", "SortOrder", "CreatedOn", "UpdatedOn"
            FROM "public"."LookupSortOrder" LIMIT 0
            """,
            typeof(SsLookupSortOrderDto)
        ),
    ];

    public string Source => "SS";

    public async Task<SsSchemaPreflightResult> PreflightAsync(CancellationToken cancellationToken = default)
    {
        if (!featureFlags.Value.Enabled || !options.Value.Sources.SS.Enabled)
        {
            throw new InvalidOperationException("SS schema preflight requires an enabled SS source.");
        }

        try
        {
            var connectionOptions = new NpgsqlConnectionStringBuilder(options.Value.Sources.SS.ConnectionString)
            {
                IncludeErrorDetail = false,
                Pooling = false,
            };
            await using var connection = _connectionFactory(connectionOptions.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var readOnlyCommand = connection.CreateCommand())
            {
                readOnlyCommand.CommandText = "SET TRANSACTION READ ONLY";
                readOnlyCommand.Transaction = transaction;
                await readOnlyCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            var checkedTables = new List<string>();
            foreach (var projection in ReferenceProjections)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = projection.Query;
                command.Transaction = transaction;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var properties = projection.Projection.GetProperties();
                if (reader.FieldCount != properties.Length)
                {
                    throw new InvalidOperationException();
                }

                foreach (var property in properties)
                {
                    var ordinal = reader.GetOrdinal(property.Name);
                    var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    var sourceType = reader.GetDataTypeName(ordinal);
                    var matches = type switch
                    {
                        _ when type == typeof(int) => sourceType == "integer",
                        _ when type == typeof(bool) => sourceType == "boolean",
                        _ when type == typeof(DateTimeOffset) => sourceType == "timestamp with time zone",
                        _ when type == typeof(string) => sourceType is "text" or "character varying",
                        _ => false,
                    };
                    if (!matches)
                    {
                        throw new InvalidOperationException();
                    }
                }

                checkedTables.Add(projection.Table);
            }

            await transaction.RollbackAsync(cancellationToken);
            return new SsSchemaPreflightResult(checkedTables.AsReadOnly());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("SS schema preflight was canceled.", cancellationToken);
        }
        catch (Exception)
        {
            throw new InvalidOperationException(
                "SS schema preflight failed. Verify read-only access and the reference schema."
            );
        }
    }
}
