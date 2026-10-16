using Mapster;
using Unified.Db.Models.Lookup;
using Unified.Leave.Models;

namespace Unified.Leave.Mappings;

internal static class LeaveTypeMapper
{
    private static readonly TypeAdapterConfig ResponseConfig = BuildResponseConfig();

    public static LeaveTypeResponse ToResponse(LeaveType leaveType) =>
        leaveType.Adapt<LeaveTypeResponse>(ResponseConfig);

    public static void Apply(LeaveType leaveType, LeaveTypeRequest request, string code)
    {
        leaveType.Code = code;
        leaveType.Description = request.Description.Trim();
        leaveType.IsPaid = request.IsPaid;
        leaveType.ExpiryDate = request.ExpiryDateUtc;
    }

    private static TypeAdapterConfig BuildResponseConfig()
    {
        var config = new TypeAdapterConfig();
        config
            .NewConfig<LeaveType, LeaveTypeResponse>()
            .Map(response => response.Name, leaveType => leaveType.Code)
            .Map(response => response.EffectiveDateUtc, leaveType => leaveType.EffectiveDate)
            .Map(response => response.ExpiryDateUtc, leaveType => leaveType.ExpiryDate);

        return config;
    }
}
