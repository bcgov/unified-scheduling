using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Calendar.Models;

namespace Unified.Calendar.Conflicts;

public sealed class CalendarConflictExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not CalendarConflictException exception)
            return;

        var canViewTraining = context.HttpContext.User.HasClaim(
            UnifiedClaimTypes.Permission,
            nameof(Permissions.UserTrainingsView)
        );
        context.Result = new ConflictObjectResult(
            new CalendarConflictRejectionResponse(
                exception.Message,
                exception
                    .Conflicts.Where(conflict =>
                        canViewTraining
                        || (conflict.Entry.SourceModule != "training" && conflict.Overlaps.SourceModule != "training")
                    )
                    .Select(CalendarConflictResponse.FromConflict)
                    .ToList()
            )
        );
        context.ExceptionHandled = true;
    }
}
