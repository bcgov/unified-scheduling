using Unified.Db.Models.Lookup;
using Unified.TimeOff.Models;

namespace Unified.TimeOff.Mappings;

internal static class LeaveTypeMapper
{
    public static LeaveTypeResponse ToResponse(LeaveType leaveType) =>
        new()
        {
            Id = leaveType.Id,
            Name = leaveType.Code,
            Description = leaveType.Description,
            IsPaid = leaveType.IsPaid,
            EffectiveDateUtc = leaveType.EffectiveDate,
            ExpiryDateUtc = leaveType.ExpiryDate,
        };

    public static void Apply(LeaveType leaveType, LeaveTypeRequest request, string code)
    {
        leaveType.Code = code;
        leaveType.Description = request.Description.Trim();
        leaveType.IsPaid = request.IsPaid;
        leaveType.ExpiryDate = request.ExpiryDateUtc;
    }
}
