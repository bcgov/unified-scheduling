using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Unified.Calendar.Models;

namespace Unified.Calendar.Conflicts;

public sealed class CalendarConflictExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not CalendarConflictException exception)
            return;

        context.Result = new ConflictObjectResult(
            new CalendarConflictRejectionResponse(
                exception.Message,
                exception.Conflicts.Select(CalendarConflictResponse.FromConflict).ToList()
            )
        );
        context.ExceptionHandled = true;
    }
}
