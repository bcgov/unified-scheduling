using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Core.Email;
using Unified.Core.Pdf;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.UserManagement;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Scheduling.Services;

public sealed class ScheduleDistributionServiceTests : IAsyncLifetime
{
    private static readonly Guid EligibleUserId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DisabledUserId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CrossLocationUserId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid MissingEmailUserId = new("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SecondEligibleUserId = new("55555555-5555-5555-5555-555555555555");

    private SqliteConnection _connection = null!;
    private UnifiedDbContext _db = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;
        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task DistributeAsync_WhenRecipientIsEligible_RendersAndSendsPdf()
    {
        var renderer = new RecordingPdfRenderer();
        var emailService = new RecordingEmailService();
        var service = CreateService(renderer, emailService);

        var result = await service.DistributeAsync(
            CreateRequest([EligibleUserId]),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(1, result.RecipientCount);
        Assert.Equal(1, renderer.CallCount);
        Assert.Equal(1, emailService.CallCount);
        Assert.Equal(
            new SchedulePdfRenderRequest
            {
                View = SchedulePdfView.Schedule,
                LocationId = 7,
                StartDate = new DateOnly(2026, 8, 24),
                EndDate = new DateOnly(2026, 8, 25),
                Period = SchedulePdfPeriod.Day,
            },
            renderer.LastRequest
        );
        var message = Assert.IsType<EmailMessage>(emailService.LastMessage);
        Assert.Equal(["eligible@example.com"], message.To);
        Assert.Empty(message.Bcc);
        Assert.Equal("Schedule for Seven (2026-08-24-2026-08-24)", message.Subject);
        var attachment = Assert.Single(message.Attachments);
        Assert.Equal("schedule-7-20260824-20260824.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal([1, 2, 3], emailService.AttachmentBytes);
    }

    [Fact]
    public async Task DistributeAsync_WhenMultipleRecipientsAreEligible_UsesTrimmedBccAndReturnsCount()
    {
        var renderer = new RecordingPdfRenderer();
        var emailService = new RecordingEmailService();
        var service = CreateService(renderer, emailService);

        var result = await service.DistributeAsync(
            CreateRequest([EligibleUserId, SecondEligibleUserId]),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.RecipientCount);
        Assert.Empty(emailService.LastMessage!.To);
        Assert.Equal(["eligible@example.com", "second@example.com"], emailService.LastMessage.Bcc);
    }

    [Fact]
    public async Task DistributeAsync_WhenRenderingFails_DoesNotSendEmail()
    {
        var renderer = new RecordingPdfRenderer(new PdfRenderingException("Rendering failed."));
        var emailService = new RecordingEmailService();
        var service = CreateService(renderer, emailService);

        await Assert.ThrowsAsync<PdfRenderingException>(() =>
            service.DistributeAsync(CreateRequest([EligibleUserId]), TestContext.Current.CancellationToken)
        );

        Assert.Equal(0, emailService.CallCount);
    }

    [Fact]
    public async Task DistributeAsync_WhenEmailSubmissionFails_DoesNotReportSuccess()
    {
        var service = CreateService(
            new RecordingPdfRenderer(),
            new RecordingEmailService(new InvalidOperationException("CHES failed."))
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DistributeAsync(CreateRequest([EligibleUserId]), TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [MemberData(nameof(IneligibleRecipientCases))]
    public async Task DistributeAsync_WhenAnyRecipientIsIneligible_RejectsBeforeRendering(Guid[] recipientIds)
    {
        var renderer = new RecordingPdfRenderer();
        var emailService = new RecordingEmailService();
        var service = CreateService(renderer, emailService);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DistributeAsync(CreateRequest(recipientIds), TestContext.Current.CancellationToken)
        );

        Assert.Equal(0, renderer.CallCount);
        Assert.Equal(0, emailService.CallCount);
    }

    public static TheoryData<Guid[]> IneligibleRecipientCases =>
        new()
        {
            new[] { DisabledUserId },
            new[] { CrossLocationUserId },
            new[] { Guid.Parse("99999999-9999-9999-9999-999999999999") },
            new[] { EligibleUserId, CrossLocationUserId },
            new[] { MissingEmailUserId },
        };

    [Fact]
    public async Task DistributeAsync_WhenLocationDoesNotExist_RejectsBeforeRendering()
    {
        var renderer = new RecordingPdfRenderer();
        var emailService = new RecordingEmailService();
        var service = CreateService(renderer, emailService);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DistributeAsync(
                CreateRequest([EligibleUserId]) with
                {
                    Context = CreateRequest([EligibleUserId]).Context! with { LocationId = 999 },
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(0, renderer.CallCount);
        Assert.Equal(0, emailService.CallCount);
    }

    private ScheduleDistributionService CreateService(
        RecordingPdfRenderer renderer,
        RecordingEmailService emailService
    ) => new(_db, renderer, emailService, NullLogger<ScheduleDistributionService>.Instance);

    private static ScheduleDistributionRequest CreateRequest(IReadOnlyCollection<Guid> recipientIds) =>
        new()
        {
            Context = new ScheduleDistributionContext
            {
                View = "schedule",
                LocationId = 7,
                StartDate = new DateOnly(2026, 8, 24),
                EndDate = new DateOnly(2026, 8, 25),
                Period = "day",
            },
            RecipientUserIds = recipientIds,
        };

    private async Task SeedAsync()
    {
        _db.Locations.AddRange(
            new Location
            {
                Id = 7,
                AgencyId = "A7",
                Name = "Seven",
                Timezone = "America/Vancouver",
            },
            new Location
            {
                Id = 8,
                AgencyId = "A8",
                Name = "Eight",
                Timezone = "America/Edmonton",
            }
        );
        _db.Users.AddRange(
            CreateUser(EligibleUserId, true, 7, "eligible@example.com"),
            CreateUser(DisabledUserId, false, 7, "disabled@example.com"),
            CreateUser(CrossLocationUserId, true, 8, "cross@example.com"),
            CreateUser(MissingEmailUserId, true, 7, string.Empty),
            CreateUser(SecondEligibleUserId, true, 7, " second@example.com ")
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static User CreateUser(Guid id, bool isEnabled, int locationId, string email) =>
        new()
        {
            Id = id,
            IdirName = id.ToString("N"),
            IsEnabled = isEnabled,
            FirstName = "Test",
            LastName = "User",
            Email = email,
            HomeLocationId = locationId,
        };

    private sealed class RecordingPdfRenderer(Exception? exception = null) : ISchedulePdfRenderer
    {
        public int CallCount { get; private set; }
        public SchedulePdfRenderRequest? LastRequest { get; private set; }

        public Task<byte[]> RenderAsync(SchedulePdfRenderRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            if (exception is not null)
                return Task.FromException<byte[]>(exception);
            return Task.FromResult<byte[]>([1, 2, 3]);
        }
    }

    private sealed class RecordingEmailService(Exception? exception = null) : IEmailService
    {
        public int CallCount { get; private set; }
        public EmailMessage? LastMessage { get; private set; }
        public byte[]? AttachmentBytes { get; private set; }

        public async Task<EmailSendResult> SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken = default
        )
        {
            CallCount++;
            LastMessage = message;
            var attachment = Assert.Single(message.Attachments);
            using var copy = new MemoryStream();
            await attachment.Content.CopyToAsync(copy, cancellationToken);
            AttachmentBytes = copy.ToArray();
            if (exception is not null)
                throw exception;
            return new EmailSendResult { TransactionId = "transaction", Tag = "tag" };
        }
    }
}
