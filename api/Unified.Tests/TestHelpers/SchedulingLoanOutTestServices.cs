using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Scheduling.Mappings;
using Unified.Scheduling.Services;

namespace Unified.Tests.TestHelpers;

internal static class SchedulingLoanOutTestServices
{
    public static ShiftLoanOutAvailabilityService CreateAvailability(
        UnifiedDbContext db,
        ITimeZoneService timeZoneService,
        ICalendarTimeZoneResolver? resolver = null,
        Func<IShiftService>? shiftServiceFactory = null
    ) =>
        new(
            db,
            resolver
                ?? new CalendarTimeZoneResolver(
                    Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
                    timeZoneService
                ),
            timeZoneService,
            shiftServiceFactory ?? (() => throw new NotSupportedException())
        );

    public static ShiftLoanOutExceptionMarkerService CreateMarker(UnifiedDbContext db, ITimeZoneService timeZoneService)
    {
        var resolver = new CalendarTimeZoneResolver(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
            timeZoneService
        );
        return new(db, CreateAvailability(db, timeZoneService, resolver), resolver);
    }

    public static SchedulingLoanOutService Create(
        UnifiedDbContext db,
        SchedulingLoanOutEventMapper mapper,
        Func<IShiftService> shiftServiceFactory,
        Func<IShiftAssignmentService> assignmentServiceFactory,
        ICalendarTimeZoneResolver resolver,
        ITimeZoneService timeZoneService,
        IRecurrenceExpander expander
    )
    {
        var availability = CreateAvailability(db, timeZoneService, resolver, shiftServiceFactory);
        var markers = new ShiftLoanOutExceptionMarkerService(db, availability, resolver);
        var transfers = new LoanOutShiftTransferService(
            NullLogger<LoanOutShiftTransferService>.Instance,
            db,
            mapper,
            shiftServiceFactory,
            assignmentServiceFactory,
            availability,
            markers,
            expander
        );
        return new(availability, markers, transfers);
    }
}
