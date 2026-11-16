using System.Data;
using System.Globalization;
using Microsoft.Extensions.Options;
using Npgsql;
using Unified.DataMigration.Options;

namespace Unified.DataMigration.Sources;

// Source snapshots are read-only and bounded. No source connection or row is logged.
public sealed class SsSnapshotReader(IOptions<DataMigrationOptions> options)
{
    private static readonly HashSet<string> Tables =
    [
        "Region",
        "Location",
        "LookupCode",
        "LookupSortOrder",
        "User",
        "Role",
        "UserRole",
        "SheriffActingRank",
        "SheriffAwayLocation",
        "SheriffLeave",
        "SheriffTraining",
        "SheriffStandardTraining",
        "Assignment",
        "Duty",
        "DutySlot",
        "Shift",
    ];
    private static readonly Dictionary<string, string[]> Fields = new()
    {
        ["Region"] = ["JustinId", "Name", "ExpiryDate"],
        ["Location"] = ["AgencyId", "Name", "JustinCode", "ParentLocationId", "RegionId", "Timezone", "ExpiryDate"],
        ["LookupCode"] =
        [
            "Type",
            "Code",
            "SubCode",
            "Description",
            "EffectiveDate",
            "ExpiryDate",
            "LocationId",
            "Mandatory",
            "ValidityPeriod",
            "Category",
            "AdvanceNotice",
            "Rotating",
        ],
        ["LookupSortOrder"] = ["LookupCodeId", "LocationId", "SortOrder"],
        ["Role"] = ["Name"],
        ["User"] =
        [
            "IdirName",
            "IdirId",
            "KeyCloakId",
            "FirstName",
            "LastName",
            "Email",
            "Gender",
            "HomeLocationId",
            "BadgeNumber",
            "Rank",
            "IsEnabled",
            "LastLogin",
            "Photo",
            "LastPhotoUpdate",
            "Excused",
        ],
        ["UserRole"] = ["UserId", "RoleId", "EffectiveDate", "ExpiryDate", "ExpiryReason"],
        ["SheriffActingRank"] =
        [
            "SheriffId",
            "Rank",
            "StartDate",
            "EndDate",
            "ExpiryDate",
            "ExpiryReason",
            "Comment",
            "Timezone",
        ],
        ["SheriffAwayLocation"] =
        [
            "SheriffId",
            "LocationId",
            "StartDate",
            "EndDate",
            "ExpiryDate",
            "ExpiryReason",
            "Comment",
            "Timezone",
        ],
        ["SheriffLeave"] =
        [
            "SheriffId",
            "LeaveTypeId",
            "StartDate",
            "EndDate",
            "ExpiryDate",
            "ExpiryReason",
            "Comment",
            "Timezone",
        ],
        ["SheriffTraining"] =
        [
            "SheriffId",
            "TrainingTypeId",
            "StartDate",
            "EndDate",
            "ExpiryDate",
            "TrainingCertificationExpiry",
            "FirstNotice",
            "Comment",
            "Note",
            "Timezone",
        ],
        ["SheriffStandardTraining"] = ["TrainingTypeId"],
        ["Assignment"] =
        [
            "LookupCodeId",
            "AdhocStartDate",
            "AdhocEndDate",
            "Timezone",
            "Name",
            "Monday",
            "Tuesday",
            "Wednesday",
            "Thursday",
            "Friday",
            "Saturday",
            "Sunday",
            "LocationId",
            "ExpiryDate",
            "Start",
            "End",
            "Comment",
        ],
        ["Duty"] = ["AssignmentId", "StartDate", "EndDate", "LocationId", "ExpiryDate", "Timezone", "Comment"],
        ["DutySlot"] =
        [
            "DutyId",
            "SheriffId",
            "StartDate",
            "EndDate",
            "LocationId",
            "ExpiryDate",
            "Timezone",
            "IsNotAvailable",
            "IsNotRequired",
            "IsClosed",
            "IsOvertime",
        ],
        ["Shift"] =
        [
            "SheriffId",
            "StartDate",
            "EndDate",
            "LocationId",
            "ExpiryDate",
            "Timezone",
            "AnticipatedAssignmentId",
            "OvertimeHours",
            "Comment",
        ],
    };

    public async Task<Dictionary<string, List<SsRow>>> ReadAsync(
        IEnumerable<string> tables,
        int batchSize,
        CancellationToken ct
    )
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        var builder = new NpgsqlConnectionStringBuilder(options.Value.Sources.SS.ConnectionString)
        {
            IncludeErrorDetail = false,
        };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var command = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, tx))
            await command.ExecuteNonQueryAsync(ct);
        var result = new Dictionary<string, List<SsRow>>();
        foreach (var table in tables.Distinct())
        {
            if (!Tables.Contains(table))
                throw new ArgumentOutOfRangeException(nameof(tables));
            result[table] = [];
            DateTime? lastChanged = null;
            object? lastId = null;
            // Typed PostgreSQL ID comparison (integer or uuid), never lexicographic ID ordering.
            while (true)
            {
                var projection = string.Join(
                    ", ",
                    new[] { "Id", "CreatedOn", "UpdatedOn" }.Concat(Fields[table]).Select(x => $"\"{x}\"")
                );
                var condition = lastId is null
                    ? ""
                    : "WHERE (COALESCE(\"UpdatedOn\", \"CreatedOn\"), \"Id\") > (@changed, @id)";
                await using var command = new NpgsqlCommand(
                    $"""
                    SELECT {projection} FROM public."{table}" {condition}
                    ORDER BY COALESCE("UpdatedOn", "CreatedOn"), "Id" LIMIT @batch
                    """,
                    connection,
                    tx
                );
                command.Parameters.AddWithValue("batch", batchSize);
                if (lastId is not null)
                {
                    command.Parameters.AddWithValue("changed", lastChanged!.Value);
                    command.Parameters.AddWithValue("id", lastId);
                }
                var count = 0;
                await using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
                        for (var i = 0; i < reader.FieldCount; i++)
                            values[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        var row = new SsRow(values);
                        result[table].Add(row);
                        lastChanged = (DateTime?)(values["UpdatedOn"] ?? values["CreatedOn"]);
                        lastId = values["Id"];
                        count++;
                    }
                }
                if (count < batchSize)
                    break;
            }
        }
        await tx.RollbackAsync(ct);
        return result;
    }
}

public sealed class SsRow(IReadOnlyDictionary<string, object?> values)
{
    public string Id => Convert.ToString(values["Id"], CultureInfo.InvariantCulture)!;

    public string? Text(string key) => values[key] as string;

    public int? Int(string key) => values[key] is { } v ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : null;

    public Guid? Guid(string key) => values[key] is System.Guid v ? v : null;

    public bool Bool(string key) => values[key] is true;

    public DateTimeOffset? Date(string key) =>
        values[key] is DateTime d ? new DateTimeOffset(d.ToUniversalTime()) : null;

    public byte[]? Bytes(string key) => values[key] as byte[];
}
