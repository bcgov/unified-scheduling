using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Core.Email;
using Unified.Core.Pdf;
using Unified.Db;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class ScheduleDistributionService(
    UnifiedDbContext db,
    ISchedulePdfRenderer pdfRenderer,
    IEmailService emailService,
    ILogger<ScheduleDistributionService> logger
) : IScheduleDistributionService
{
    public async Task<ScheduleDistributionResult> DistributeAsync(
        ScheduleDistributionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var context = request.Context ?? throw new InvalidOperationException("A distribution context is required.");
        var recipientUserIds = request.RecipientUserIds ?? [];
        var location = await db
            .Locations.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == context.LocationId, cancellationToken);
        if (location is null)
            throw new KeyNotFoundException($"Location {context.LocationId} was not found.");

        var recipients = await db
            .Users.AsNoTracking()
            .Where(candidate =>
                candidate.IsEnabled
                && candidate.HomeLocationId == context.LocationId
                && recipientUserIds.Contains(candidate.Id)
            )
            .Select(candidate => new { candidate.Id, candidate.Email })
            .ToListAsync(cancellationToken);
        if (recipients.Count != recipientUserIds.Count)
            throw new InvalidOperationException("One or more selected recipients are unavailable.");

        if (recipients.Any(recipient => string.IsNullOrWhiteSpace(recipient.Email)))
            throw new InvalidOperationException("One or more selected recipients do not have an email address.");
        var recipientEmails = recipients.Select(recipient => recipient.Email.Trim()).ToArray();

        var pdf = await pdfRenderer.RenderAsync(
            new SchedulePdfRenderRequest
            {
                View = ToPdfView(context.View),
                LocationId = context.LocationId,
                StartDate = context.StartDate,
                EndDate = context.EndDate,
                Period = ToPdfPeriod(context.Period),
                IncludeAssignmentTimes = context.IncludeAssignmentTimes,
                ViewportWidth = context.ViewportWidth,
                ViewportHeight = context.ViewportHeight,
            },
            cancellationToken
        );

        await using var attachmentContent = new MemoryStream(pdf, writable: false);
        await emailService.SendAsync(
            new EmailMessage
            {
                To = recipientEmails.Length == 1 ? recipientEmails : [],
                Bcc = recipientEmails.Length > 1 ? recipientEmails : [],
                Subject =
                    $"Schedule for {location.Name} ({context.StartDate:yyyy-MM-dd}-{context.EndDate.AddDays(-1):yyyy-MM-dd})",
                Body = "A PDF copy of the schedule is attached.",
                Attachments =
                [
                    new EmailAttachment
                    {
                        FileName =
                            $"schedule-{location.Id}-{context.StartDate:yyyyMMdd}-{context.EndDate.AddDays(-1):yyyyMMdd}.pdf",
                        ContentType = "application/pdf",
                        Content = attachmentContent,
                    },
                ],
                UnifiedCorrelationId = $"schedule-distribution:{location.Id}:{Guid.NewGuid():D}",
            },
            cancellationToken
        );

        logger.LogInformation(
            "Distributed {View} schedule PDF for location {LocationId}, start date {StartDate}, end date {EndDate}, to {RecipientCount} recipients",
            context.View,
            context.LocationId,
            context.StartDate,
            context.EndDate,
            recipientEmails.Length
        );
        return new ScheduleDistributionResult(recipientEmails.Length);
    }

    private static SchedulePdfView ToPdfView(string value) =>
        value.ToLowerInvariant() switch
        {
            "schedule" => SchedulePdfView.Schedule,
            "assignments" => SchedulePdfView.Assignments,
            _ => throw new InvalidOperationException("The distribution view is invalid."),
        };

    private static SchedulePdfPeriod ToPdfPeriod(string value) =>
        value.ToLowerInvariant() switch
        {
            "day" => SchedulePdfPeriod.Day,
            "week" => SchedulePdfPeriod.Week,
            "work-week" => SchedulePdfPeriod.WorkWeek,
            _ => throw new InvalidOperationException("The distribution period is invalid."),
        };
}
