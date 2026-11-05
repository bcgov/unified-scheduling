using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Core.Email;
using Unified.Core.Pdf;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;
using Unified.LoanOut.Constants;
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
    private static readonly DateTimeOffset RangeStart = new(2026, 8, 24, 7, 0, 0, TimeSpan.Zero);

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

    [Theory]
    [InlineData(4, 12, true)]
    [InlineData(-1, 1, true)]
    [InlineData(23, 25, true)]
    [InlineData(25, 26, false)]
    [InlineData(-2, -1, false)]
    [InlineData(-1, 0, false)]
    [InlineData(24, 25, false)]
    [InlineData(0, null, true)]
    [InlineData(12, null, true)]
    [InlineData(-1, null, false)]
    [InlineData(24, null, false)]
    public async Task DistributeAsync_WhenBorrowedRecipientHasLoanOut_RequiresHalfOpenOverlap(
        int startHour,
        int? endHour,
        bool eligible
    )
    {
        await AddLoanOutAsync(
            RangeStart.AddHours(startHour),
            endHour.HasValue ? RangeStart.AddHours(endHour.Value) : null
        );
        var renderer = new RecordingPdfRenderer();
        var emailService = new RecordingEmailService();
        var service = CreateService(renderer, emailService);

        if (eligible)
        {
            var result = await service.DistributeAsync(
                CreateRequest([CrossLocationUserId]),
                TestContext.Current.CancellationToken
            );
            Assert.Equal(1, result.RecipientCount);
            Assert.Equal(["cross@example.com"], emailService.LastMessage!.To);
            Assert.Empty(emailService.LastMessage.Bcc);
        }
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DistributeAsync(CreateRequest([CrossLocationUserId]), TestContext.Current.CancellationToken)
            );

        Assert.Equal(eligible ? 1 : 0, renderer.CallCount);
        Assert.Equal(eligible ? 1 : 0, emailService.CallCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task DistributeAsync_WhenHomeAndBorrowedRecipientsAreEligible_SendsOnceUsingBcc(int loanOutCount)
    {
        for (var index = 0; index < loanOutCount; index++)
            await AddLoanOutAsync(RangeStart.AddHours(4 + index), RangeStart.AddHours(12));
        Assert.Empty(_db.ShiftEntries);
        var renderer = new RecordingPdfRenderer();
        var emailService = new RecordingEmailService();

        var result = await CreateService(renderer, emailService)
            .DistributeAsync(CreateRequest([EligibleUserId, CrossLocationUserId]), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.RecipientCount);
        Assert.Equal(1, renderer.CallCount);
        Assert.Equal(1, emailService.CallCount);
        Assert.Empty(emailService.LastMessage!.To);
        Assert.Equal(["cross@example.com", "eligible@example.com"], emailService.LastMessage.Bcc.Order());
    }

    [Fact]
    public async Task DistributeAsync_WhenLoanOutCoversOnlyPartOfWeek_AcceptsBorrowedRecipientWithoutShift()
    {
        await AddLoanOutAsync(RangeStart.AddDays(2).AddHours(4), RangeStart.AddDays(2).AddHours(12));
        var request = CreateRequest([CrossLocationUserId]);
        request = request with { Context = request.Context! with { EndDate = new DateOnly(2026, 8, 31), Period = "week" } };
        var renderer = new RecordingPdfRenderer();
        var emailService = new RecordingEmailService();

        var result = await CreateService(renderer, emailService)
            .DistributeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.RecipientCount);
        Assert.Equal(1, renderer.CallCount);
        Assert.Equal(1, emailService.CallCount);
    }

    [Theory]
    [InlineData(7, CalendarEventStatusTypeCodes.Cancelled, "loanOut", "loanOut", true)]
    [InlineData(8, CalendarEventStatusTypeCodes.Active, "loanOut", "loanOut", true)]
    [InlineData(7, CalendarEventStatusTypeCodes.Active, "calendar", "loanOut", true)]
    [InlineData(7, CalendarEventStatusTypeCodes.Active, "loanOut", CalendarEventTypeCodes.General, true)]
    [InlineData(7, CalendarEventStatusTypeCodes.Active, "loanOut", "loanOut", false)]
    public async Task DistributeAsync_WhenLoanOutDoesNotQualify_RejectsAllRecipientsBeforeRendering(
        int destinationId,
        string status,
        string sourceModule,
        string eventType,
        bool matchesUser
    )
    {
        var entry = await AddLoanOutAsync(RangeStart.AddHours(4), RangeStart.AddHours(12));
        entry.LoanOutLocationId = destinationId;
        entry.Event!.StatusTypeCode = status;
        entry.Event.SourceModule = sourceModule;
        entry.Event.EventTypeCode = eventType;
        if (!matchesUser)
            entry.Users = [new LoanOutEntryUser { UserId = EligibleUserId }];
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var renderer = new RecordingPdfRenderer();
        var emailService = new RecordingEmailService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(renderer, emailService)
                .DistributeAsync(
                    CreateRequest([EligibleUserId, CrossLocationUserId]),
                    TestContext.Current.CancellationToken
                )
        );

        Assert.Equal(0, renderer.CallCount);
        Assert.Equal(0, emailService.CallCount);
    }

    [Theory]
    [InlineData(false, "cross@example.com")]
    [InlineData(true, "")]
    [InlineData(true, " ")]
    public async Task DistributeAsync_WhenBorrowedRecipientBecomesInvalid_RejectsAllRecipientsBeforeRendering(
        bool isEnabled,
        string email
    )
    {
        await AddLoanOutAsync(RangeStart.AddHours(4), RangeStart.AddHours(12));
        var renderer = new RecordingPdfRenderer();
        var emailService = new RecordingEmailService();
        var service = CreateService(renderer, emailService);
        var recipient = await _db.Users.SingleAsync(
            user => user.Id == CrossLocationUserId,
            TestContext.Current.CancellationToken
        );
        recipient.IsEnabled = isEnabled;
        recipient.Email = email;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DistributeAsync(
                CreateRequest([EligibleUserId, CrossLocationUserId]),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(0, renderer.CallCount);
        Assert.Equal(0, emailService.CallCount);
    }

    [Theory]
    [MemberData(nameof(FeatureDisabledRecipientCases))]
    public async Task DistributeAsync_WhenLoanOutIsDisabled_OnlyHomeRecipientsAreEligible(
        Guid[] recipientIds,
        bool eligible
    )
    {
        await AddLoanOutAsync(RangeStart.AddHours(4), RangeStart.AddHours(12));
        var renderer = new RecordingPdfRenderer();
        var emailService = new RecordingEmailService();
        var service = CreateService(renderer, emailService, loanOutEnabled: false);

        if (eligible)
        {
            var result = await service.DistributeAsync(CreateRequest(recipientIds), TestContext.Current.CancellationToken);
            Assert.Equal(1, result.RecipientCount);
            Assert.Equal(["eligible@example.com"], emailService.LastMessage!.To);
        }
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DistributeAsync(CreateRequest(recipientIds), TestContext.Current.CancellationToken)
            );

        Assert.Equal(eligible ? 1 : 0, renderer.CallCount);
        Assert.Equal(eligible ? 1 : 0, emailService.CallCount);
    }

    public static TheoryData<Guid[], bool> FeatureDisabledRecipientCases =>
        new()
        {
            { [EligibleUserId], true },
            { [CrossLocationUserId], false },
            { [EligibleUserId, CrossLocationUserId], false },
        };

    [Theory]
    [InlineData(3, 7, 30, false)]
    [InlineData(3, 8, 0, true)]
    [InlineData(10, 6, 30, true)]
    [InlineData(10, 7, 0, false)]
    [InlineData(10, 7, 30, false)]
    public async Task DistributeAsync_WhenWeekCrossesDaylightSaving_UsesDestinationLocalBoundaries(
        int day,
        int hour,
        int minute,
        bool eligible
    )
    {
        var start = new DateTimeOffset(2025, 3, day, hour, minute, 0, TimeSpan.Zero);
        await AddLoanOutAsync(start, start.AddMinutes(15));
        var request = CreateRequest([CrossLocationUserId]);
        request = request with
        {
            Context = request.Context! with
            {
                StartDate = new DateOnly(2025, 3, 3),
                EndDate = new DateOnly(2025, 3, 10),
                Period = "week",
            },
        };
        var renderer = new RecordingPdfRenderer();
        var emailService = new RecordingEmailService();
        var service = CreateService(renderer, emailService);

        if (eligible)
            Assert.Equal(
                1,
                (await service.DistributeAsync(request, TestContext.Current.CancellationToken)).RecipientCount
            );
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DistributeAsync(request, TestContext.Current.CancellationToken)
            );

        Assert.Equal(eligible ? 1 : 0, renderer.CallCount);
        Assert.Equal(eligible ? 1 : 0, emailService.CallCount);
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
        RecordingEmailService emailService,
        bool loanOutEnabled = true
    )
    {
        var timeZoneService = new TimeZoneService();
        var resolver = new CalendarTimeZoneResolver(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
            timeZoneService
        );
        return new(
            _db,
            renderer,
            emailService,
            NullLogger<ScheduleDistributionService>.Instance,
            SchedulingLoanOutTestServices.CreateAvailability(
                _db,
                timeZoneService,
                resolver,
                loanOutEnabled: loanOutEnabled
            ),
            resolver,
            timeZoneService
        );
    }

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
        var effectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _db.EventTypes.AddRange(
            new EventType
            {
                Code = LoanOutConstants.LoanOutEventTypeCode,
                Description = "Loan Out",
                EffectiveDate = effectiveDate,
            },
            new EventType
            {
                Code = CalendarEventTypeCodes.General,
                Description = "General",
                EffectiveDate = effectiveDate,
            }
        );
        _db.EventStatusTypes.AddRange(
            new EventStatusType
            {
                Code = CalendarEventStatusTypeCodes.Active,
                Description = "Active",
                EffectiveDate = effectiveDate,
            },
            new EventStatusType
            {
                Code = CalendarEventStatusTypeCodes.Cancelled,
                Description = "Cancelled",
                EffectiveDate = effectiveDate,
            }
        );
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
            CreateUser(CrossLocationUserId, true, 8, " cross@example.com "),
            CreateUser(MissingEmailUserId, true, 7, string.Empty),
            CreateUser(SecondEligibleUserId, true, 7, " second@example.com ")
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<LoanOutEntry> AddLoanOutAsync(DateTimeOffset startAtUtc, DateTimeOffset? endAtUtc)
    {
        var entry = new LoanOutEntry
        {
            LoanOutLocationId = 7,
            Event = new Event
            {
                Title = "Loan Out",
                StartAtUtc = startAtUtc,
                EndAtUtc = endAtUtc,
                LocationId = 8,
                TimeZoneId = "America/Edmonton",
                SourceModule = LoanOutConstants.SourceModule,
                EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
                StatusTypeCode = CalendarEventStatusTypeCodes.Active,
            },
            Users = [new LoanOutEntryUser { UserId = CrossLocationUserId }],
        };
        _db.LoanOutEntries.Add(entry);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entry;
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
