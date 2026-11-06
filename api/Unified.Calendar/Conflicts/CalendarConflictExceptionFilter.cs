using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Unified.Calendar.Models;

namespace Unified.Calendar.Conflicts;

public sealed class CalendarConflictExceptionFilter(CalendarConflictProjection projection) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not CalendarConflictException exception)
            return;

        var visible = await projection.ProjectAsync(
            exception.Conflicts,
            context.HttpContext.User,
            context.HttpContext.RequestAborted
        );
        context.Result = new ConflictObjectResult(
            new CalendarConflictRejectionResponse(exception.Message, visible.Conflicts)
            {
                RestrictedWarnings = visible.RestrictedWarnings,
                HasRestrictedConflicts = visible.HasRestrictedConflicts,
            }
        );
        context.ExceptionHandled = true;
    }
}
