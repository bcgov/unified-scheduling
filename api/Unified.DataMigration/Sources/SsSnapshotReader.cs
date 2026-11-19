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
            "Discriminator",
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

    public async Task<IReadOnlyDictionary<string, long>> ReadExcludedCountsAsync(CancellationToken ct)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        var connectionOptions = CreateSourceConnectionOptions();
        await using var connection = new NpgsqlConnection(connectionOptions.ConnectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, tx))
            await readOnly.ExecuteNonQueryAsync(ct);
        // Fixed identifiers and COUNT only. Never fetch key rings, source audits or permission payloads.
        foreach (
            var table in new[] { "Audit", "JcSynchronization", "DataProtectionKeys", "Permission", "RolePermission" }
        )
        {
            await using var exists = new NpgsqlCommand("SELECT to_regclass(@name) IS NOT NULL", connection, tx);
            exists.Parameters.AddWithValue("name", $"public.\"{table}\"");
            if (await exists.ExecuteScalarAsync(ct) is not true)
            {
                counts[table] = 0;
                continue;
            }
            await using var count = new NpgsqlCommand($"SELECT COUNT(*) FROM public.\"{table}\"", connection, tx);
            counts[table] = (long)(await count.ExecuteScalarAsync(ct))!;
        }
        await tx.RollbackAsync(ct);
        return counts;
    }

    public async Task<Dictionary<string, List<SsRow>>> ReadAsync(
        IEnumerable<string> tables,
        int batchSize,
        CancellationToken ct,
        DateTimeOffset? userHistoryBoundary = null
    )
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        var builder = CreateSourceConnectionOptions();
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
            string upperChanged;
            object upperId;
            await using (
                var upperCommand = new NpgsqlCommand(
                    $"""
                    SELECT COALESCE("UpdatedOn", "CreatedOn")::text, "Id" FROM public."{table}"
                    ORDER BY COALESCE("UpdatedOn", "CreatedOn") DESC, "Id" DESC LIMIT 1
                    """,
                    connection,
                    tx
                )
            )
            {
                await using var upperReader = await upperCommand.ExecuteReaderAsync(ct);
                if (!await upperReader.ReadAsync(ct))
                    continue;
                upperChanged = upperReader.GetString(0);
                upperId = upperReader.GetValue(1);
            }
            string? lastChanged = null;
            object? lastId = null;
            // Typed PostgreSQL ID comparison (integer or uuid), never lexicographic ID ordering.
            while (true)
            {
                var projection = string.Join(
                    ", ",
                    new[] { "Id", "CreatedOn", "UpdatedOn" }.Concat(Fields[table]).Select(x => $"\"{x}\"")
                );
                var condition =
                    "WHERE (COALESCE(\"UpdatedOn\", \"CreatedOn\"), \"Id\") <= (CAST(@upperChanged AS timestamptz), @upperId)";
                if (lastId is not null)
                    condition +=
                        " AND (COALESCE(\"UpdatedOn\", \"CreatedOn\"), \"Id\") > (CAST(@changed AS timestamptz), @id)";
                await using var command = new NpgsqlCommand(
                    $"""
                    SELECT {projection}, COALESCE("UpdatedOn", "CreatedOn")::text AS "__MigrationChanged"
                    FROM public."{table}" {condition}
                    ORDER BY COALESCE("UpdatedOn", "CreatedOn"), "Id" LIMIT @batch
                    """,
                    connection,
                    tx
                );
                command.Parameters.AddWithValue("batch", batchSize);
                command.Parameters.AddWithValue("upperChanged", upperChanged);
                command.Parameters.AddWithValue("upperId", upperId);
                if (lastId is not null)
                {
                    command.Parameters.AddWithValue("changed", lastChanged!);
                    command.Parameters.AddWithValue("id", lastId);
                }
                var count = 0;
                await using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
                        for (var i = 0; i < reader.FieldCount - 1; i++)
                            values[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        var row = new SsRow(values);
                        result[table].Add(row);
                        // Keep server timestamp text only for bound parameterization. Npgsql's
                        // DateTime.MinValue -> -infinity conversion would otherwise omit finite
                        // 0001-01-01 seed rows. Comparisons remain typed PostgreSQL timestamptz.
                        lastChanged = reader.GetString(reader.FieldCount - 1);
                        lastId = values["Id"];
                        count++;
                    }
                }
                if (count < batchSize)
                    break;
            }
        }
        if (userHistoryBoundary is { } boundary)
        {
            if (!result.TryGetValue("User", out var users))
                throw new ArgumentException("User history checks require the User source table.", nameof(tables));
            var userIds = users
                .Where(x => x.Guid("KeyCloakId") is not null)
                .GroupBy(x => x.Guid("KeyCloakId")!.Value)
                .Where(group =>
                    group.Count() > 1 && group.Any(x => x.Bool("IsEnabled") && x.Guid("IdirId") is not null)
                )
                .SelectMany(group => group.Where(x => !x.Bool("IsEnabled") && x.Guid("IdirId") is null))
                .Select(x => Guid.Parse(x.Id))
                .ToArray();
            if (userIds.Length == 0)
                result["__UsersWithHistory"] = [];
            else
                result["__UsersWithHistory"] = await ReadUsersWithHistoryAsync(connection, tx, userIds, boundary, ct);
        }
        await tx.RollbackAsync(ct);
        return result;
    }

    private NpgsqlConnectionStringBuilder CreateSourceConnectionOptions()
    {
        var builder = new NpgsqlConnectionStringBuilder(options.Value.Sources.SS.ConnectionString)
        {
            IncludeErrorDetail = false,
        };
        // Preserve an explicitly supplied Npgsql Command Timeout; otherwise scope the
        // eight-hour migration default to this source connection.
        if (!builder.ShouldSerialize("Command Timeout"))
            builder.CommandTimeout = options.Value.CommandTimeoutSeconds;
        return builder;
    }

    private static async Task<List<SsRow>> ReadUsersWithHistoryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid[] userIds,
        DateTimeOffset boundary,
        CancellationToken ct
    )
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT DISTINCT user_id
            FROM (
                SELECT r."UserId" AS user_id
                FROM public."UserRole" r WHERE r."UserId" = ANY(@userIds)
                UNION
                SELECT r."SheriffId"
                FROM public."SheriffActingRank" r
                WHERE r."SheriffId" = ANY(@userIds) AND r."EndDate" >= @boundary
                UNION
                SELECT r."SheriffId"
                FROM public."SheriffAwayLocation" r
                WHERE r."SheriffId" = ANY(@userIds) AND r."EndDate" >= @boundary
                UNION
                SELECT r."SheriffId"
                FROM public."SheriffLeave" r
                WHERE r."SheriffId" = ANY(@userIds) AND r."EndDate" >= @boundary
                UNION
                SELECT r."SheriffId"
                FROM public."SheriffTraining" r
                JOIN public."User" u ON u."Id" = r."SheriffId"
                WHERE r."SheriffId" = ANY(@userIds) AND r."EndDate" >= @boundary
                    AND r."ExpiryDate" IS NULL AND NOT COALESCE(u."Excused", false)
                UNION
                SELECT r."SheriffId"
                FROM public."Shift" r
                WHERE r."SheriffId" = ANY(@userIds) AND r."EndDate" >= @boundary
                UNION
                SELECT slot."SheriffId"
                FROM public."DutySlot" slot
                JOIN public."Duty" duty ON duty."Id" = slot."DutyId"
                LEFT JOIN public."Assignment" assignment ON assignment."Id" = duty."AssignmentId"
                LEFT JOIN public."LookupCode" code ON code."Id" = assignment."LookupCodeId"
                WHERE slot."SheriffId" = ANY(@userIds) AND slot."ExpiryDate" IS NULL
                    AND slot."EndDate" >= @boundary AND duty."EndDate" >= @boundary
                    AND COALESCE(code."Code", '') NOT IN ('Test', 'Leave Waitlist')
            ) history
            WHERE user_id IS NOT NULL
            """,
            connection,
            transaction
        );
        command.Parameters.AddWithValue("userIds", userIds);
        command.Parameters.AddWithValue("boundary", boundary);
        var usersWithHistory = new List<SsRow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            usersWithHistory.Add(new SsRow(new Dictionary<string, object?> { ["Id"] = reader.GetGuid(0) }));
        return usersWithHistory;
    }
}

public sealed class SsRow(IReadOnlyDictionary<string, object?> values)
{
    // Hash only: private mapped text stays in memory; photo bytes and Region.Code never enter manifests.
    internal object SimulationInput(string table) =>
        new
        {
            Values = values
                .Where(x => x.Key != "Photo" && !(table == "Region" && x.Key == "Code"))
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value),
            HasPhoto = values.TryGetValue("Photo", out var photo) && photo is byte[],
        };

    internal SsRow ProjectNullable(params string[] fields) =>
        new(fields.ToDictionary(x => x, x => values.TryGetValue(x, out var value) ? value : null));

    internal SsRow WithUserIdentities(Guid? idirId, Guid? keycloakId)
    {
        var projected = values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        projected["IdirId"] = idirId;
        projected["KeyCloakId"] = keycloakId;
        return new SsRow(projected);
    }

    internal SsRow WithReversedDatesSwapped()
    {
        if (Date("StartDate") is not { } start || Date("EndDate") is not { } end || end >= start)
            return this;
        var swapped = values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        swapped["StartDate"] = values["EndDate"];
        swapped["EndDate"] = values["StartDate"];
        return new SsRow(swapped);
    }

    public string Id => Convert.ToString(values["Id"], CultureInfo.InvariantCulture)!;

    public string? Text(string key) => values[key] as string;

    public int? Int(string key) => values[key] is { } v ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : null;

    public Guid? Guid(string key) => values[key] is System.Guid v ? v : null;

    public bool Bool(string key) => values[key] is true;

    public DateTimeOffset? Date(string key) =>
        values[key] is DateTime d ? new DateTimeOffset(d.ToUniversalTime()) : null;

    public byte[]? Bytes(string key) => values[key] as byte[];

    public TimeSpan? Time(string key) =>
        values[key] switch
        {
            TimeSpan time => time,
            TimeOnly time => time.ToTimeSpan(),
            _ => null,
        };
}
