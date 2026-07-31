using System.Text.Json.Serialization;

namespace Unified.Calendar.Holidays;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StatutoryHolidayType
{
    NewYearsDay,
    FamilyDay,
    GoodFriday,
    EasterMonday,
    VictoriaDay,
    CanadaDay,
    BcDay,
    LabourDay,
    TruthAndReconciliation,
    Thanksgiving,
    RemembranceDay,
    Christmas,
    BoxingDay,
}
