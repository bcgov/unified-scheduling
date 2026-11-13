using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Unified.Db;
using Unified.Infrastructure;
using Unified.Infrastructure.Encryption;
using DataProtectionOptions = Unified.Infrastructure.Options.DataProtectionOptions;

namespace Unified.Tests.Infrastructure;

public class DataProtectionTests
{
    private static readonly XNamespace DataProtectionNamespace = "http://schemas.asp.net/2015/03/dataProtection";

    // Deterministic test-only material, never used for application configuration.
    private static readonly string TestKey = Convert.ToBase64String(
        Enumerable.Range(0, 32).Select(i => (byte)i).ToArray()
    );
    private static readonly string OtherKey = Convert.ToBase64String(Enumerable.Repeat((byte)127, 32).ToArray());

    public static bool IsPostgresConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING"));

    [Fact(
        Skip = "Set UNIFIED_TEST_POSTGRES_CONNECTION_STRING to run against PostgreSQL.",
        SkipUnless = nameof(IsPostgresConfigured)
    )]
    public async Task Protect_PostgresRepository_IndependentProvidersShareEncryptedKeysAfterRestart()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(
            Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING")
        );
        await connection.OpenAsync(token);
        await using var db = new DataProtectionKeyDbContext(
            new DbContextOptionsBuilder<DataProtectionKeyDbContext>().UseNpgsql(connection).Options
        );
        // Follow the existing PostgreSQL tests: connection-local tables, no changes
        // to application schemas or migrations. Providers have independent scopes.
        await db.Database.ExecuteSqlRawAsync(
            db.Database.GenerateCreateScript().Replace("CREATE TABLE", "CREATE TEMP TABLE"),
            token
        );

        string payload;
        using (var first = CreateProvider(options => options.UseNpgsql(connection)))
        using (var second = CreateProvider(options => options.UseNpgsql(connection)))
        {
            payload = Protector(first).Protect("PostgreSQL payload");
            Assert.Equal("PostgreSQL payload", Protector(second).Unprotect(payload));
        }

        using var restarted = CreateProvider(options => options.UseNpgsql(connection));
        Assert.Equal("PostgreSQL payload", Protector(restarted).Unprotect(payload));
        var xml = XElement.Parse((await db.DataProtectionKeys.SingleAsync(token)).Xml!);
        Assert.Single(xml.Descendants(DataProtectionNamespace + "encryptedSecret"));
        Assert.Empty(xml.Descendants("masterKey"));
    }

    [Fact]
    public void Protect_IndependentInstancesSharingDatabaseAndSecret_CanUnprotectEachOthersPayloads()
    {
        using var database = new KeyDatabase();
        using var first = database.CreateProvider();
        using var second = database.CreateProvider();
        var firstProtector = Protector(first);
        var secondProtector = Protector(second);

        Assert.Equal("first", secondProtector.Unprotect(firstProtector.Protect("first")));
        Assert.Equal("second", firstProtector.Unprotect(secondProtector.Protect("second")));
        Assert.Single(first.GetRequiredService<IKeyManager>().GetAllKeys());
    }

    [Fact]
    public void Unprotect_AfterProviderIsRecreated_ReadsPersistedKeyAndDecryptorMetadata()
    {
        using var database = new KeyDatabase();
        string payload;
        using (var original = database.CreateProvider())
            payload = Protector(original).Protect("before restart");

        using var restarted = database.CreateProvider();

        Assert.Equal("before restart", Protector(restarted).Unprotect(payload));
    }

    [Fact]
    public void Unprotect_WithUnrelatedSecret_RejectsExistingKeyAndPayload()
    {
        using var database = new KeyDatabase();
        using var first = database.CreateProvider();
        var payload = Protector(first).Protect("private");
        using var other = database.CreateProvider(OtherKey);

        // Mirrors the startup check: unreadable persisted keys must stop rollout.
        var key = Assert.Single(other.GetRequiredService<IKeyManager>().GetAllKeys());
        Assert.ThrowsAny<CryptographicException>(() => key.CreateEncryptor());
        Assert.ThrowsAny<CryptographicException>(() => Protector(other).Unprotect(payload));
    }

    public static IEnumerable<object?[]> InvalidKeys()
    {
        yield return [null];
        yield return [""];
        yield return ["   "];
        yield return ["not-base64!"];
        foreach (var size in new[] { 16, 24, 31, 33 })
            yield return [Convert.ToBase64String(new byte[size])];
    }

    [Theory]
    [MemberData(nameof(InvalidKeys))]
    public void ValidateOnStart_MissingMalformedOrWrongSizeSecret_FailsWithoutDisclosingValue(string? key)
    {
        var configuration = Configuration(key);
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration);
        services.AddUnifiedDataProtection(configuration);
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IStartupValidator>().Validate()
        );

        Assert.Contains("EncryptionKey", exception.Message);
        if (!string.IsNullOrWhiteSpace(key))
            Assert.DoesNotContain(key, exception.Message);
    }

    [Fact]
    public void ValidateOnStart_ValidSecretAndApplicationName_Succeeds()
    {
        var configuration = Configuration(TestKey);
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration);
        services.AddUnifiedDataProtection(configuration);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStartupValidator>().Validate();

        Assert.Equal(TestKey, provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.EncryptionKey);
        Assert.Equal(
            "UnifiedScheduling",
            provider
                .GetRequiredService<IOptions<Microsoft.AspNetCore.DataProtection.DataProtectionOptions>>()
                .Value.ApplicationDiscriminator
        );
    }

    [Fact]
    public void Protect_PersistedXml_EncryptsSecretMaterialAndRecordsDecryptorType()
    {
        using var database = new KeyDatabase();
        using var provider = database.CreateProvider();
        _ = Protector(provider).Protect("payload");
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataProtectionKeyDbContext>();
        var xml = XElement.Parse(Assert.Single(db.DataProtectionKeys).Xml!);

        Assert.Empty(xml.Descendants("masterKey"));
        var encryptedSecret = Assert.Single(xml.Descendants(DataProtectionNamespace + "encryptedSecret"));
        Assert.Contains(typeof(AesGcmXmlDecryptor).FullName!, encryptedSecret.Attribute("decryptorType")!.Value);
        var encryptedKey = Assert.Single(encryptedSecret.Elements("encryptedKey"));
        Assert.False(string.IsNullOrWhiteSpace(encryptedKey.Element("value")!.Value));
        var decrypted = new AesGcmXmlDecryptor(provider).Decrypt(encryptedKey);
        Assert.Equal("masterKey", decrypted.Name.LocalName);
    }

    [Fact]
    public void AddDbModule_KeyContext_ExcludesBusinessAuditingMappingsAndInterceptors()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DatabaseConnectionString"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                }
            )
            .Build();
        var interceptor = new RejectBusinessSaveInterceptor();
        var services = new ServiceCollection().AddLogging().AddSingleton<IInterceptor>(interceptor);
        services.AddDbModule(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataProtectionKeyDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DataProtectionKeyDbContext>>();

        Assert.Equal(typeof(DbContext), db.GetType().BaseType);
        Assert.Single(db.Model.GetEntityTypes());
        Assert.Equal("DataProtectionKeys", db.Model.GetEntityTypes().Single().GetTableName());
        Assert.Empty(options.FindExtension<CoreOptionsExtension>()!.Interceptors ?? []);
        Assert.Contains(
            interceptor,
            scope
                .ServiceProvider.GetRequiredService<DbContextOptions<UnifiedDbContext>>()
                .FindExtension<CoreOptionsExtension>()!
                .Interceptors!
        );
        Assert.Contains(
            DataProtectionKeyDbContext.MigrationsHistoryTableName,
            db.GetService<IHistoryRepository>().GetCreateScript()
        );
    }

    [Fact]
    public void CreateNewKey_ReplacementKey_KeepsExistingPayloadReadableAndAutomaticGenerationEnabled()
    {
        using var database = new KeyDatabase();
        using var first = database.CreateProvider();
        var oldPayload = Protector(first).Protect("old payload");
        var originalKey = Assert.Single(first.GetRequiredService<IKeyManager>().GetAllKeys());
        var options = first.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        Assert.True(options.AutoGenerateKeys);
        Assert.Equal(TimeSpan.FromDays(90), options.NewKeyLifetime);

        var now = DateTimeOffset.UtcNow;
        var replacement = first.GetRequiredService<IKeyManager>().CreateNewKey(now, now.AddDays(90));
        Assert.NotEqual(originalKey.KeyId, replacement.KeyId);
        using var restarted = database.CreateProvider();
        var newPayload = Protector(restarted).Protect(Encoding.UTF8.GetBytes("new payload"));

        Assert.Equal(replacement.KeyId, new Guid(newPayload.AsSpan(4, 16)));
        Assert.Equal("old payload", Protector(restarted).Unprotect(oldPayload));
        Assert.Equal("new payload", Encoding.UTF8.GetString(Protector(first).Unprotect(newPayload)));
        Assert.Equal(2, restarted.GetRequiredService<IKeyManager>().GetAllKeys().Count);
    }

    [Fact]
    public void AddUnifiedDataProtection_AuthenticationOptions_PreservesCookieAndOidcConfiguration()
    {
        using var baseline = AuthenticationServiceCollectionExtensionTests.CreateServiceProvider();
        using var database = new KeyDatabase();
        using var shared = database.CreateProvider();
        var beforeCookie = baseline.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Cookies");
        var afterCookie = shared.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Cookies");
        var beforeOidc = baseline.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get("OpenIdConnect");
        var afterOidc = shared.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get("OpenIdConnect");

        Assert.Equal(beforeCookie.Cookie.Name, afterCookie.Cookie.Name);
        Assert.Equal(beforeCookie.Cookie.HttpOnly, afterCookie.Cookie.HttpOnly);
        Assert.Equal(beforeCookie.Cookie.SameSite, afterCookie.Cookie.SameSite);
        Assert.Equal(beforeCookie.Cookie.SecurePolicy, afterCookie.Cookie.SecurePolicy);
        Assert.Equal(beforeCookie.ExpireTimeSpan, afterCookie.ExpireTimeSpan);
        Assert.Equal(beforeCookie.SlidingExpiration, afterCookie.SlidingExpiration);
        Assert.Equal(beforeOidc.Authority, afterOidc.Authority);
        Assert.Equal(beforeOidc.ClientId, afterOidc.ClientId);
        Assert.Equal(beforeOidc.CallbackPath, afterOidc.CallbackPath);
        Assert.Equal(beforeOidc.SignInScheme, afterOidc.SignInScheme);
        Assert.Equal(beforeOidc.ResponseType, afterOidc.ResponseType);
        Assert.Equal(beforeOidc.UsePkce, afterOidc.UsePkce);
        Assert.Equal(beforeOidc.SaveTokens, afterOidc.SaveTokens);
        Assert.Equal(beforeOidc.Scope, afterOidc.Scope);
        Assert.Equal(
            beforeOidc.ClaimActions.Select(a => (a.GetType(), a.ClaimType)),
            afterOidc.ClaimActions.Select(a => (a.GetType(), a.ClaimType))
        );
        var beforeAuthentication = baseline.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var afterAuthentication = shared.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        Assert.Equal(beforeAuthentication.DefaultScheme, afterAuthentication.DefaultScheme);
        Assert.Equal(beforeAuthentication.DefaultAuthenticateScheme, afterAuthentication.DefaultAuthenticateScheme);
        Assert.Equal(beforeAuthentication.DefaultChallengeScheme, afterAuthentication.DefaultChallengeScheme);

        var ticket = new AuthenticationTicket(
            AuthenticationServiceCollectionExtensionTests.CreatePrincipal(),
            "Cookies"
        );
        var protectedTicket = afterCookie.TicketDataFormat.Protect(ticket);
        using var replica = database.CreateProvider();
        var replicaCookie = replica.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Cookies");
        Assert.NotNull(replicaCookie.TicketDataFormat.Unprotect(protectedTicket));
    }

    [Fact]
    public void Encrypt_RepeatedPlaintext_UsesDifferentNoncesAndAuthenticatesCiphertext()
    {
        using var aes = new AesGcmService(Convert.FromBase64String(TestKey));
        var first = aes.Encrypt("key material");
        var second = aes.Encrypt("key material");
        Assert.NotEqual(first, second);
        Assert.Equal("key material", aes.Decrypt(first));
        var tampered = Convert.FromBase64String(first);
        tampered[^1] ^= 1;

        Assert.ThrowsAny<CryptographicException>(() => aes.Decrypt(Convert.ToBase64String(tampered)));
    }

    [Fact]
    public void Decrypt_TruncatedOrInvalidEnvelope_RejectsBeforeReadingLengths()
    {
        using var aes = new AesGcmService(Convert.FromBase64String(TestKey));
        Assert.Throws<CryptographicException>(() => aes.Decrypt(Convert.ToBase64String(new byte[2])));
        Assert.Throws<CryptographicException>(() => aes.Decrypt(Convert.ToBase64String(new byte[40])));
    }

    [Fact]
    public void Decrypt_MissingXmlValue_RejectsMalformedKey()
    {
        using var database = new KeyDatabase();
        using var provider = database.CreateProvider();

        Assert.Throws<CryptographicException>(() =>
            new AesGcmXmlDecryptor(provider).Decrypt(new XElement("encryptedKey"))
        );
    }

    private static IDataProtector Protector(IServiceProvider provider) =>
        provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("SharedDataProtectionTests");

    private static IConfiguration Configuration(string? key) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(AuthenticationServiceCollectionExtensionTests.CreateConfiguration().AsEnumerable())
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DataProtection:ApplicationName"] = "UnifiedScheduling",
                    ["DataProtection:EncryptionKey"] = key,
                }
            )
            .Build();

    private static ServiceProvider CreateProvider(Action<DbContextOptionsBuilder> configureDatabase, string? key = null)
    {
        var configuration = Configuration(key ?? TestKey);
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IInterceptor>(new RejectBusinessSaveInterceptor());
        services.AddDbContext<DataProtectionKeyDbContext>(configureDatabase);
        services.AddUnifiedAuthentication(
            new AuthenticationServiceCollectionExtensionTests.FakeHostEnvironment(),
            configuration
        );
        services.AddUnifiedDataProtection(configuration).PersistKeysToDbContext<DataProtectionKeyDbContext>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class KeyDatabase : IDisposable
    {
        private readonly string connectionString = $"Data Source=dp-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        private readonly SqliteConnection keeper;

        public KeyDatabase()
        {
            keeper = new SqliteConnection(connectionString);
            keeper.Open();
            using var provider = CreateProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<DataProtectionKeyDbContext>().Database.EnsureCreated();
        }

        public ServiceProvider CreateProvider(string? encryptionKey = null) =>
            DataProtectionTests.CreateProvider(options => options.UseSqlite(connectionString), encryptionKey);

        public void Dispose() => keeper.Dispose();
    }

    private sealed class RejectBusinessSaveInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result
        ) => throw new InvalidOperationException("Business interceptor must not run for key persistence.");

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Business interceptor must not run for key persistence.");
    }
}
