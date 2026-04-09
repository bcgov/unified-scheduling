namespace Unified.UserManagement.Models;

public class LocationDto
{
    public int Id { get; set; }
    public string AgencyId { get; set; }
    public string Name { get; set; }
    public string JustinCode { get; set; }
    public int? ParentLocationId { get; set; }
    public DateTimeOffset? ExpiryDate { get; set; }
    public int? RegionId { get; set; }
    public string Timezone { get; set; }
    public uint ConcurrencyToken { get; set; }
}
