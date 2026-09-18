using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Core.Email;
using Unified.Db;
using Unified.Db.Models.Training;

namespace Unified.Training.Services;

public sealed class UserTrainingExpiryNotificationService(
    UnifiedDbContext db,
    IEnumerable<IEmailService> emailServices,
    TimeProvider timeProvider,
    ILogger<UserTrainingExpiryNotificationService> logger
) : IUserTrainingExpiryNotificationService
{
    private readonly IEmailService? _emailService = emailServices.FirstOrDefault();

    public async Task<int> SendDueExpiryNoticesAsync(CancellationToken cancellationToken = default)
    {
        if (_emailService is null)
        {
            logger.LogInformation("Skipping expiry notice run because no email service is registered");
            return 0;
        }

        var now = timeProvider.GetUtcNow();
        var today = now.UtcDateTime.Date;
        var todayUtc = new DateTimeOffset(today, TimeSpan.Zero);

        logger.LogDebug("Selecting expiry notice candidates for UTC date {TodayUtc}", todayUtc);

        // Find all latest user-trainings that are expiring soon and have not yet been sent a notice.
        var candidates = await db
            .UserTrainings.Include(ut => ut.User)
            .Include(ut => ut.Training)
            .AsNoTracking()
            .Where(ut => ut.ExpiryDate >= todayUtc)
            .Where(ut => ut.Training.AdvanceNoticeDays > 0)
            .Where(ut => ut.NoticeState == UserTrainingNoticeStates.None)
            .Where(ut => ut.User.IsEnabled)
            .Where(ut => !string.IsNullOrWhiteSpace(ut.User.Email))
            .Where(ut =>
                !db.UserTrainings.Any(newerVersion =>
                    newerVersion.UserId == ut.UserId
                    && newerVersion.TrainingId == ut.TrainingId
                    && newerVersion.Version > ut.Version
                )
            )
            .ToListAsync(cancellationToken);

        logger.LogDebug("Found {CandidateCount} expiry notice candidate(s)", candidates.Count);

        var sentCount = 0;
        var outsideWindowCount = 0;
        var alreadyClaimedCount = 0;
        var failures = new List<Exception>();

        foreach (var candidate in candidates)
        {
            var expiryDate = candidate.ExpiryDate!.Value;
            var daysUntilExpiry = (expiryDate.UtcDateTime.Date - today).Days;
            var advanceNoticeDays = candidate.Training.AdvanceNoticeDays!.Value;

            if (daysUntilExpiry < 0 || daysUntilExpiry > advanceNoticeDays)
            {
                outsideWindowCount++;
                continue;
            }

            var claimedCount = await db
                .UserTrainings.Where(ut => ut.Id == candidate.Id)
                .Where(ut => ut.NoticeState == UserTrainingNoticeStates.None)
                .ExecuteUpdateAsync(
                    updates => updates.SetProperty(ut => ut.NoticeState, UserTrainingNoticeStates.Pending),
                    cancellationToken
                );

            if (claimedCount == 0)
            {
                alreadyClaimedCount++;
                continue;
            }

            var subject = BuildSubject(candidate);
            var body = BuildBody(candidate, daysUntilExpiry);

            try
            {
                await _emailService.SendAsync(
                    new EmailMessage
                    {
                        To = [candidate.User.Email.Trim()],
                        Subject = subject,
                        Body = body,
                        UnifiedCorrelationId = $"training-expiry-notice:{candidate.Id}",
                    },
                    cancellationToken
                );
            }
            catch (EmailDeliveryStateUnknownException ex)
            {
                // Leave the row in Pending so retries do not duplicate delivery when provider outcome is unknown.
                logger.LogWarning(
                    ex,
                    "Email delivery outcome unknown for user training {UserTrainingId}; leaving notice state as Pending",
                    candidate.Id
                );
                failures.Add(ex);
                continue;
            }
            catch (Exception ex)
            {
                try
                {
                    await db
                        .UserTrainings.Where(ut => ut.Id == candidate.Id)
                        .Where(ut => ut.NoticeState == UserTrainingNoticeStates.Pending)
                        .ExecuteUpdateAsync(
                            updates => updates.SetProperty(ut => ut.NoticeState, UserTrainingNoticeStates.None),
                            cancellationToken
                        );

                    logger.LogWarning(
                        ex,
                        "Email send failed for user training {UserTrainingId}; notice state reset to None for retry",
                        candidate.Id
                    );
                }
                catch (Exception resetEx)
                {
                    logger.LogError(
                        resetEx,
                        "Failed resetting notice state to None for user training {UserTrainingId} after send failure; leaving state as Pending",
                        candidate.Id
                    );
                }

                failures.Add(ex);
                continue;
            }

            try
            {
                var markedSentCount = await db
                    .UserTrainings.Where(ut => ut.Id == candidate.Id)
                    .Where(ut => ut.NoticeState == UserTrainingNoticeStates.Pending)
                    .ExecuteUpdateAsync(
                        updates => updates.SetProperty(ut => ut.NoticeState, UserTrainingNoticeStates.Sent),
                        cancellationToken
                    );

                if (markedSentCount == 0)
                {
                    logger.LogWarning(
                        "Email sent for user training {UserTrainingId}, but notice state was not updated to Sent; leaving as Pending",
                        candidate.Id
                    );
                    throw new InvalidOperationException(
                        $"Failed to mark training notice as Sent for user training {candidate.Id}."
                    );
                }

                sentCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Email sent for user training {UserTrainingId}, but persisting Sent state failed; leaving state as Pending",
                    candidate.Id
                );
                failures.Add(ex);
                continue;
            }
        }

        logger.LogInformation(
            "Expiry notice run complete: candidates {CandidateCount}, sent {SentCount}, skipped outside window {OutsideWindowCount}, already claimed {AlreadyClaimedCount}, failures {FailureCount}",
            candidates.Count,
            sentCount,
            outsideWindowCount,
            alreadyClaimedCount,
            failures.Count
        );

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"One or more training expiry notices failed. Failure count: {failures.Count}.",
                failures
            );
        }

        return sentCount;
    }

    private static string BuildBody(UserTraining userTraining, int daysUntilExpiry)
    {
        var expiresOn = userTraining.ExpiryDate!.Value.UtcDateTime.ToString("yyyy-MM-dd");
        var countdown = daysUntilExpiry == 0 ? "today" : $"in {daysUntilExpiry} day(s)";

        if (userTraining.Training.Rotating)
        {
            return $"Hello {userTraining.User.FirstName},\n\n"
                + $"This is a reminder that your rotating training '{userTraining.Training.Code}' expires {countdown} ({expiresOn} UTC).\n"
                + "Please complete requalification by retaking this training before expiry.\n\n"
                + "Unified Scheduling";
        }

        return $"Hello {userTraining.User.FirstName},\n\n"
            + $"This is a reminder that your training '{userTraining.Training.Code}' expires {countdown} ({expiresOn} UTC).\n"
            + "No immediate action is required; this is a heads up that it is approaching expiry.\n\n"
            + "Unified Scheduling";
    }

    private static string BuildSubject(UserTraining userTraining) =>
        userTraining.Training.Rotating
            ? $"Training requalification notice: {userTraining.Training.Code}"
            : $"Training expiry notice: {userTraining.Training.Code}";
}
