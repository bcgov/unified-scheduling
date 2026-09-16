using Microsoft.EntityFrameworkCore;
using Unified.Core.Email;
using Unified.Db;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;
using Unified.Training.Services;

namespace Unified.Tests.Training.Services;

public sealed class UserTrainingExpiryNotificationServiceTests : IAsyncLifetime
{
    private readonly string _databaseName = $"user-training-expiry-notification-{Guid.NewGuid():N}";
    private UnifiedDbContext _db = null!;

    private static readonly Guid UserId = Guid.NewGuid();
    private const int TrainingId = 201;
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseSqlite($"Data Source={_databaseName};Mode=Memory;Cache=Shared")
            .Options;

        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.EnsureCreatedAsync();

        await SeedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task SendDueExpiryNoticesAsync_WhenInsideAdvanceNoticeWindow_SendsAndMarksSent()
    {
        // Arrange
        var emailService = new RecordingEmailService();
        await SeedUserTrainingAsync(expiryDate: FixedNow.AddDays(2), noticeState: UserTrainingNoticeStates.None);
        var sut = new UserTrainingExpiryNotificationService(_db, [emailService], new FixedTimeProvider(FixedNow));

        // Act
        var sentCount = await sut.SendDueExpiryNoticesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, sentCount);
        Assert.Single(emailService.Messages);

        var saved = await _db
            .UserTrainings.AsNoTracking()
            .SingleAsync(ut => ut.UserId == UserId, TestContext.Current.CancellationToken);
        Assert.Equal(UserTrainingNoticeStates.Sent, saved.NoticeState);
    }

    [Fact]
    public async Task SendDueExpiryNoticesAsync_WhenOutsideAdvanceNoticeWindow_DoesNotSend()
    {
        // Arrange
        var emailService = new RecordingEmailService();
        await SeedUserTrainingAsync(expiryDate: FixedNow.AddDays(3), noticeState: UserTrainingNoticeStates.None);
        var sut = new UserTrainingExpiryNotificationService(_db, [emailService], new FixedTimeProvider(FixedNow));

        // Act
        var sentCount = await sut.SendDueExpiryNoticesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, sentCount);
        Assert.Empty(emailService.Messages);
    }

    [Fact]
    public async Task SendDueExpiryNoticesAsync_WhenNoEmailProviderRegistered_SkipsWithoutFailing()
    {
        // Arrange
        await SeedUserTrainingAsync(expiryDate: FixedNow.AddDays(1), noticeState: UserTrainingNoticeStates.None);
        var sut = new UserTrainingExpiryNotificationService(
            _db,
            Enumerable.Empty<IEmailService>(),
            new FixedTimeProvider(FixedNow)
        );

        // Act
        var sentCount = await sut.SendDueExpiryNoticesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, sentCount);

        var saved = await _db
            .UserTrainings.AsNoTracking()
            .SingleAsync(ut => ut.UserId == UserId, TestContext.Current.CancellationToken);
        Assert.Equal(UserTrainingNoticeStates.None, saved.NoticeState);
    }

    [Fact]
    public async Task SendDueExpiryNoticesAsync_WhenNewerVersionExists_SendsOnlyLatestVersion()
    {
        // Arrange
        var emailService = new RecordingEmailService();
        await SeedUserTrainingAsync(
            expiryDate: FixedNow.AddDays(1),
            noticeState: UserTrainingNoticeStates.None,
            version: 1
        );
        await SeedUserTrainingAsync(
            expiryDate: FixedNow.AddDays(1),
            noticeState: UserTrainingNoticeStates.None,
            version: 2
        );

        var sut = new UserTrainingExpiryNotificationService(_db, [emailService], new FixedTimeProvider(FixedNow));

        // Act
        var sentCount = await sut.SendDueExpiryNoticesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, sentCount);
        Assert.Single(emailService.Messages);

        var versions = await _db
            .UserTrainings.Where(ut => ut.UserId == UserId)
            .AsNoTracking()
            .OrderBy(ut => ut.Version)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UserTrainingNoticeStates.None, versions[0].NoticeState);
        Assert.Equal(UserTrainingNoticeStates.Sent, versions[1].NoticeState);
    }

    [Fact]
    public async Task SendDueExpiryNoticesAsync_WhenEmailSubmissionFails_ResetsPendingForRetry()
    {
        // Arrange
        var emailService = new FailingEmailService();
        await SeedUserTrainingAsync(expiryDate: FixedNow.AddDays(1), noticeState: UserTrainingNoticeStates.None);
        var sut = new UserTrainingExpiryNotificationService(_db, [emailService], new FixedTimeProvider(FixedNow));

        // Act
        await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            sut.SendDueExpiryNoticesAsync(TestContext.Current.CancellationToken)
        );

        // Assert
        var saved = await _db
            .UserTrainings.AsNoTracking()
            .SingleAsync(ut => ut.UserId == UserId, TestContext.Current.CancellationToken);
        Assert.Equal(UserTrainingNoticeStates.None, saved.NoticeState);
    }

    [Fact]
    public async Task SendDueExpiryNoticesAsync_WhenEmailOutcomeIsUnknown_LeavesPendingToPreventDuplicates()
    {
        // Arrange
        var emailService = new UnknownOutcomeEmailService();
        await SeedUserTrainingAsync(expiryDate: FixedNow.AddDays(1), noticeState: UserTrainingNoticeStates.None);
        var sut = new UserTrainingExpiryNotificationService(_db, [emailService], new FixedTimeProvider(FixedNow));

        // Act
        await Assert.ThrowsAsync<EmailDeliveryStateUnknownException>(() =>
            sut.SendDueExpiryNoticesAsync(TestContext.Current.CancellationToken)
        );

        // Assert
        var saved = await _db
            .UserTrainings.AsNoTracking()
            .SingleAsync(ut => ut.UserId == UserId, TestContext.Current.CancellationToken);
        Assert.Equal(UserTrainingNoticeStates.Pending, saved.NoticeState);
    }

    [Fact]
    public async Task SendDueExpiryNoticesAsync_WhenNoticeAlreadyPending_DoesNotSendAgain()
    {
        // Arrange
        var emailService = new RecordingEmailService();
        await SeedUserTrainingAsync(expiryDate: FixedNow.AddDays(1), noticeState: UserTrainingNoticeStates.Pending);
        var sut = new UserTrainingExpiryNotificationService(_db, [emailService], new FixedTimeProvider(FixedNow));

        // Act
        var sentCount = await sut.SendDueExpiryNoticesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, sentCount);
        Assert.Empty(emailService.Messages);
    }

    private async Task SeedAsync()
    {
        _db.Users.Add(
            new User
            {
                Id = UserId,
                IdirName = "test.user",
                IdirId = Guid.NewGuid(),
                IsEnabled = true,
                FirstName = "Test",
                LastName = "User",
                Email = "test.user@gov.bc.ca",
                Gender = Gender.Other,
            }
        );

        _db.TrainingCategories.Add(new TrainingCategory { Id = 1, Name = "General" });

        _db.Trainings.Add(
            new global::Unified.Db.Models.Training.Training
            {
                Id = TrainingId,
                Code = "DEMO",
                Description = "Demo training",
                TrainingCategoryId = 1,
                Rotating = true,
                AdvanceNoticeDays = 2,
            }
        );

        await _db.SaveChangesAsync();
    }

    private async Task SeedUserTrainingAsync(DateTimeOffset expiryDate, string noticeState, int version = 1)
    {
        _db.UserTrainings.Add(
            new UserTraining
            {
                UserId = UserId,
                TrainingId = TrainingId,
                Version = version,
                AwardedOn = FixedNow.AddDays(-100),
                EndingOn = FixedNow.AddDays(-99),
                ExpiryDate = expiryDate,
                NoticeState = noticeState,
            }
        );

        await _db.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.FromResult(
                new EmailSendResult
                {
                    TransactionId = "tx",
                    Tag = "tag",
                    Messages = [],
                }
            );
        }
    }

    private sealed class FailingEmailService : IEmailService
    {
        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            throw new EmailDeliveryException(
                tag: "tag",
                correlationId: message.UnifiedCorrelationId,
                recipientCount: message.To.Count,
                attachmentCount: 0,
                statusCode: 503
            );
        }
    }

    private sealed class UnknownOutcomeEmailService : IEmailService
    {
        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            throw new EmailDeliveryStateUnknownException(
                tag: "tag",
                correlationId: message.UnifiedCorrelationId,
                recipientCount: message.To.Count,
                attachmentCount: 0,
                innerException: new InvalidOperationException("timeout")
            );
        }
    }
}
