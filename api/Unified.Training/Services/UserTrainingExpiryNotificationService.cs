using Microsoft.EntityFrameworkCore;
using Unified.Core.Email;
using Unified.Db;
using Unified.Db.Models.Training;

namespace Unified.Training.Services;

public sealed class UserTrainingExpiryNotificationService(
    UnifiedDbContext db,
    IEnumerable<IEmailService> emailServices,
    TimeProvider timeProvider
) : IUserTrainingExpiryNotificationService
{
    private readonly IEmailService? _emailService = emailServices.FirstOrDefault();

    public async Task<int> SendDueExpiryNoticesAsync(CancellationToken cancellationToken = default)
    {
        if (_emailService is null)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();
        var today = now.UtcDateTime.Date;

        var candidates = await db
            .UserTrainings.Include(ut => ut.User)
            .Include(ut => ut.Training)
            .Where(ut => ut.ExpiryDate.HasValue)
            .Where(ut => ut.ExpiryDate > now)
            .Where(ut => ut.Training.AdvanceNoticeDays.HasValue)
            .Where(ut => ut.Training.AdvanceNoticeDays > 0)
            .Where(ut => ut.NoticeState != UserTrainingNoticeStates.Sent)
            .Where(ut => ut.User.IsEnabled)
            .Where(ut => ut.User.Email != null && ut.User.Email != string.Empty)
            .Where(ut =>
                !db.UserTrainings.Any(newerVersion =>
                    newerVersion.UserId == ut.UserId
                    && newerVersion.TrainingId == ut.TrainingId
                    && newerVersion.Version > ut.Version
                )
            )
            .ToListAsync(cancellationToken);

        var sentCount = 0;

        foreach (var candidate in candidates)
        {
            var expiryDate = candidate.ExpiryDate!.Value;
            var daysUntilExpiry = (expiryDate.UtcDateTime.Date - today).Days;
            var advanceNoticeDays = candidate.Training.AdvanceNoticeDays!.Value;

            if (daysUntilExpiry < 0 || daysUntilExpiry > advanceNoticeDays)
            {
                continue;
            }

            var subject = $"Training expiry notice: {candidate.Training.Code}";
            var body = BuildBody(candidate, daysUntilExpiry);

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

            candidate.NoticeState = UserTrainingNoticeStates.Sent;
            sentCount++;

            await db.SaveChangesAsync(cancellationToken);
        }

        return sentCount;
    }

    private static string BuildBody(UserTraining userTraining, int daysUntilExpiry)
    {
        var expiresOn = userTraining.ExpiryDate!.Value.UtcDateTime.ToString("yyyy-MM-dd");
        var countdown = daysUntilExpiry == 0 ? "today" : $"in {daysUntilExpiry} day(s)";

        return
            $"Hi {userTraining.User.FirstName},\n\n"
            + $"This is a reminder that your training '{userTraining.Training.Code}' expires {countdown} ({expiresOn} UTC).\n"
            + "Please renew it before expiry if renewal is required.\n\n"
            + "Unified Scheduling";
    }
}
