using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Npgsql;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Options;
using Unified.DataMigration.Sources;

namespace Unified.Tests.DataMigration;

public sealed class SsLegacyMigrationSourceTests
{
    private const string SensitiveError = "Password=secret; Host=private-host; source row PII";
    private const string SafeError = "SS schema preflight failed. Verify read-only access and the reference schema.";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Preflight_DisabledFeatureOrSsSource_NeverCreatesConnection(bool featureEnabled, bool ssEnabled)
    {
        var connection = new FakeConnection();
        var source = CreateSource(connection, featureEnabled, ssEnabled);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            source.PreflightAsync(TestContext.Current.CancellationToken)
        );

        Assert.Equal("SS", source.Source);
        Assert.Equal("SS schema preflight requires an enabled SS source.", exception.Message);
        Assert.Null(connection.ReceivedConnectionString);
        Assert.Empty(connection.Stages);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("character varying")]
    public async Task Preflight_UsesOnlySsConnectionAndExplicitZeroRowQueriesInReadOnlyTransaction(string stringType)
    {
        var connection = new FakeConnection { StringType = stringType };

        var result = await CreateSource(connection).PreflightAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Region", "Location", "LookupCode", "LookupSortOrder" }, result.CheckedTables);
        var settings = new NpgsqlConnectionStringBuilder(connection.ReceivedConnectionString);
        Assert.Equal("ss-only", settings.Host);
        Assert.False(settings.IncludeErrorDetail);
        Assert.False(settings.Pooling);
        Assert.Equal(
            new[]
            {
                "open",
                "begin",
                "readonly",
                "Region",
                "Location",
                "LookupCode",
                "LookupSortOrder",
                "rollback",
                "transaction-dispose",
                "dispose",
            },
            connection.Stages
        );
        Assert.Equal("SET TRANSACTION READ ONLY", connection.Commands[0]);
        foreach (var table in result.CheckedTables)
        {
            var expectedColumns = string.Join(", ", connection.Schemas[table].Select(column => $"\"{column.Name}\""));
            var query = connection.Commands[result.CheckedTables.ToList().IndexOf(table) + 1];
            Assert.Equal(
                $"SELECT {expectedColumns} FROM \"public\".\"{table}\" LIMIT 0",
                string.Join(' ', query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            );
            Assert.DoesNotContain("*", query, StringComparison.Ordinal);
            Assert.DoesNotContain("User", query, StringComparison.Ordinal);
            Assert.DoesNotContain("CreatedById", query, StringComparison.Ordinal);
            Assert.DoesNotContain("UpdatedById", query, StringComparison.Ordinal);
            Assert.DoesNotContain("xmin", query, StringComparison.Ordinal);
        }
        Assert.Equal(0, connection.RowReadCount);
        Assert.True(connection.AllCommandsUseTransaction);
    }

    [Theory]
    [InlineData("extra")]
    [InlineData("missing")]
    [InlineData("renamed")]
    [InlineData("wrong-type")]
    public async Task Preflight_SchemaMismatch_FailsClosedWithoutLeakingMetadata(string mismatch)
    {
        var connection = new FakeConnection();
        var columns = connection.Schemas["Region"];
        switch (mismatch)
        {
            case "extra":
                columns.Add((SensitiveError, "text"));
                break;
            case "missing":
                columns.RemoveAt(0);
                break;
            case "renamed":
                columns[0] = (SensitiveError, "integer");
                break;
            case "wrong-type":
                columns[0] = ("Id", SensitiveError);
                break;
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSource(connection).PreflightAsync(TestContext.Current.CancellationToken)
        );

        AssertSafe(exception);
        Assert.DoesNotContain("Location", connection.Stages);
        Assert.Contains("transaction-dispose", connection.Stages);
        Assert.Contains("dispose", connection.Stages);
        Assert.Equal(0, connection.RowReadCount);
    }

    [Theory]
    [InlineData("factory")]
    [InlineData("open")]
    [InlineData("begin")]
    [InlineData("readonly")]
    [InlineData("Region")]
    [InlineData("rollback")]
    [InlineData("transaction-dispose")]
    [InlineData("dispose")]
    public async Task Preflight_ProviderError_RedactsAllExceptionDetails(string failingStage)
    {
        var connection = new FakeConnection { FailingStage = failingStage };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSource(connection).PreflightAsync(TestContext.Current.CancellationToken)
        );

        AssertSafe(exception);
        Assert.Equal(0, connection.RowReadCount);
        if (failingStage == "readonly")
        {
            Assert.Single(connection.Commands);
        }
    }

    [Theory]
    [InlineData("Region", "Id", "bigint")]
    [InlineData("Region", "JustinId", "text")]
    [InlineData("Region", "Code", "integer")]
    [InlineData("Region", "CreatedOn", "timestamp without time zone")]
    [InlineData("Region", "UpdatedOn", "timestamp without time zone")]
    [InlineData("LookupCode", "Mandatory", "integer")]
    public async Task Preflight_IncompatibleProjectionType_FailsForEachTypeFamily(
        string table,
        string columnName,
        string incompatibleType
    )
    {
        var connection = new FakeConnection();
        var columns = connection.Schemas[table];
        var ordinal = columns.FindIndex(column => column.Name == columnName);
        columns[ordinal] = (columnName, incompatibleType);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSource(connection).PreflightAsync(TestContext.Current.CancellationToken)
        );

        AssertSafe(exception);
        Assert.DoesNotContain("rollback", connection.Stages);
        Assert.Contains("transaction-dispose", connection.Stages);
        Assert.Contains("dispose", connection.Stages);
    }

    [Fact]
    public async Task Preflight_InvalidConnectionString_RedactsParserErrorWithoutOpeningConnection()
    {
        var connection = new FakeConnection();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSource(connection, connectionString: SensitiveError + ";InvalidKey=secret")
                .PreflightAsync(TestContext.Current.CancellationToken)
        );

        AssertSafe(exception);
        Assert.Empty(connection.Stages);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("Region")]
    [InlineData("rollback")]
    public async Task Preflight_CallerCancellation_PreservesTokenNotProviderMessage(string stage)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var connection = new FakeConnection { CancelAtStage = stage, Cancellation = cancellation };

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateSource(connection).PreflightAsync(cancellation.Token)
        );

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal("SS schema preflight was canceled.", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("secret", exception.ToString(), StringComparison.Ordinal);
        Assert.Contains("dispose", connection.Stages);
        Assert.Equal(0, connection.RowReadCount);
    }

    [Fact]
    public async Task Preflight_UnrelatedProviderCancellation_IsSafeFailure()
    {
        var connection = new FakeConnection
        {
            FailingStage = "open",
            Failure = new OperationCanceledException(SensitiveError),
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSource(connection).PreflightAsync(TestContext.Current.CancellationToken)
        );

        AssertSafe(exception);
    }

    private static void AssertSafe(InvalidOperationException exception)
    {
        Assert.Equal(SafeError, exception.Message);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("secret", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("PII", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private-host", exception.ToString(), StringComparison.Ordinal);
    }

    private static SsLegacyMigrationSource CreateSource(
        FakeConnection connection,
        bool featureEnabled = true,
        bool ssEnabled = true,
        string connectionString = "Host=ss-only;Password=secret;Include Error Detail=true;Pooling=true"
    ) =>
        new(
            Microsoft.Extensions.Options.Options.Create(new DataMigrationFeatureFlags { Enabled = featureEnabled }),
            Microsoft.Extensions.Options.Options.Create(
                new DataMigrationOptions
                {
                    Sources = new DataMigrationSourcesOptions
                    {
                        SS = new DataMigrationSourceOptions
                        {
                            Enabled = ssEnabled,
                            ConnectionString = connectionString,
                        },
                        CASS = new DataMigrationSourceOptions
                        {
                            Enabled = true,
                            ConnectionString = "Host=cass-only;Password=cass-secret",
                        },
                    },
                }
            ),
            settings =>
            {
                connection.Visit("factory", record: false);
                connection.ReceivedConnectionString = settings;
                return connection;
            }
        );

    private sealed class FakeConnection : DbConnection
    {
        public Dictionary<string, List<(string Name, string Type)>> Schemas { get; } =
            new()
            {
                ["Region"] =
                [
                    ("Id", "integer"),
                    ("JustinId", "integer"),
                    ("Code", "text"),
                    ("Name", "text"),
                    ("ExpiryDate", "timestamp with time zone"),
                    ("CreatedOn", "timestamp with time zone"),
                    ("UpdatedOn", "timestamp with time zone"),
                ],
                ["Location"] =
                [
                    ("Id", "integer"),
                    ("AgencyId", "text"),
                    ("Name", "text"),
                    ("JustinCode", "text"),
                    ("ParentLocationId", "integer"),
                    ("RegionId", "integer"),
                    ("Timezone", "text"),
                    ("ExpiryDate", "timestamp with time zone"),
                    ("CreatedOn", "timestamp with time zone"),
                    ("UpdatedOn", "timestamp with time zone"),
                ],
                ["LookupCode"] =
                [
                    ("Id", "integer"),
                    ("Type", "integer"),
                    ("Code", "text"),
                    ("SubCode", "text"),
                    ("Description", "text"),
                    ("EffectiveDate", "timestamp with time zone"),
                    ("ExpiryDate", "timestamp with time zone"),
                    ("LocationId", "integer"),
                    ("Mandatory", "boolean"),
                    ("ValidityPeriod", "integer"),
                    ("Category", "text"),
                    ("AdvanceNotice", "integer"),
                    ("Rotating", "boolean"),
                    ("CreatedOn", "timestamp with time zone"),
                    ("UpdatedOn", "timestamp with time zone"),
                ],
                ["LookupSortOrder"] =
                [
                    ("Id", "integer"),
                    ("LookupCodeId", "integer"),
                    ("LocationId", "integer"),
                    ("SortOrder", "integer"),
                    ("CreatedOn", "timestamp with time zone"),
                    ("UpdatedOn", "timestamp with time zone"),
                ],
            };
        public List<string> Stages { get; } = [];
        public List<string> Commands { get; } = [];
        public string? ReceivedConnectionString { get; set; }
        public string? FailingStage { get; init; }
        public Exception Failure { get; init; } = new InvalidOperationException(SensitiveError);
        public string? CancelAtStage { get; init; }
        public CancellationTokenSource? Cancellation { get; init; }
        public string StringType { get; init; } = "text";
        public int RowReadCount { get; set; }
        public bool AllCommandsUseTransaction { get; set; } = true;
        public FakeTransaction? ActiveTransaction { get; private set; }

        public void Visit(string stage, bool record = true)
        {
            if (record)
                Stages.Add(stage);
            if (CancelAtStage == stage)
            {
                Cancellation!.Cancel();
                throw new OperationCanceledException(SensitiveError, Cancellation.Token);
            }
            if (FailingStage == stage)
                throw Failure;
        }

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "SS";
        public override string DataSource => "ss-only";
        public override string ServerVersion => "test";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close() { }

        public override void Open() => Visit("open");

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Open();
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            Visit("begin");
            ActiveTransaction = new FakeTransaction(this);
            return ActiveTransaction;
        }

        protected override DbCommand CreateDbCommand() => new FakeCommand(this);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Visit("dispose");
            base.Dispose(disposing);
        }
    }

    private sealed class FakeTransaction(FakeConnection connection) : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => connection;

        public override void Commit() => throw new InvalidOperationException("Preflight must never commit.");

        public override void Rollback() => connection.Visit("rollback");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                connection.Visit("transaction-dispose");
            base.Dispose(disposing);
        }
    }

    private sealed class FakeCommand(FakeConnection connection) : DbCommand
    {
        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = connection;
        protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();

        public override void Cancel() => throw new NotSupportedException();

        public override void Prepare() => throw new NotSupportedException();

        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();

        public override object ExecuteScalar() => throw new InvalidOperationException("No source values may be read.");

        public override int ExecuteNonQuery()
        {
            Track();
            Assert.Equal("SET TRANSACTION READ ONLY", CommandText);
            connection.Visit("readonly");
            return 0;
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            Track();
            Assert.Contains("readonly", connection.Stages);
            var table = Assert.Single(
                connection.Schemas.Keys,
                table => CommandText.Contains($"FROM \"public\".\"{table}\" LIMIT 0", StringComparison.Ordinal)
            );
            connection.Visit(table);
            return new FakeSchemaReader(connection, connection.Schemas[table]);
        }

        private void Track()
        {
            connection.Commands.Add(CommandText);
            connection.AllCommandsUseTransaction &=
                ReferenceEquals(DbTransaction, connection.ActiveTransaction) && DbTransaction is not null;
        }
    }

    private sealed class FakeSchemaReader(FakeConnection connection, List<(string Name, string Type)> columns)
        : DbDataReader
    {
        public override int FieldCount => columns.Count;

        public override int GetOrdinal(string name)
        {
            var ordinal = columns.FindIndex(column => column.Name == name);
            return ordinal >= 0 ? ordinal : throw new IndexOutOfRangeException(SensitiveError);
        }

        public override string GetDataTypeName(int ordinal) =>
            columns[ordinal].Type == "text" ? connection.StringType : columns[ordinal].Type;

        public override string GetName(int ordinal) => columns[ordinal].Name;

        public override bool Read()
        {
            connection.RowReadCount++;
            throw new InvalidOperationException("Preflight must not read rows.");
        }

        public override bool NextResult() => throw new NotSupportedException();

        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int Depth => 0;
        public override int RecordsAffected => 0;
        public override object this[int ordinal] => throw new NotSupportedException();
        public override object this[string name] => throw new NotSupportedException();

        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();

        public override byte GetByte(int ordinal) => throw new NotSupportedException();

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();

        public override char GetChar(int ordinal) => throw new NotSupportedException();

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();

        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();

        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();

        public override double GetDouble(int ordinal) => throw new NotSupportedException();

        public override float GetFloat(int ordinal) => throw new NotSupportedException();

        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();

        public override short GetInt16(int ordinal) => throw new NotSupportedException();

        public override int GetInt32(int ordinal) => throw new NotSupportedException();

        public override long GetInt64(int ordinal) => throw new NotSupportedException();

        public override string GetString(int ordinal) => throw new NotSupportedException();

        public override object GetValue(int ordinal) => throw new NotSupportedException();

        public override int GetValues(object[] values) => throw new NotSupportedException();

        public override bool IsDBNull(int ordinal) => throw new NotSupportedException();

        public override Type GetFieldType(int ordinal) => throw new NotSupportedException();

        public override IEnumerator GetEnumerator() => throw new NotSupportedException();
    }
}
