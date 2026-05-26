using System;

namespace Unified.UserManagement.Models;

public partial class RegionDto
{
    public int Id { get; set; }
    public int? JustinId { get; set; }
    public string Code { get; set; } = String.Empty;
    public string Name { get; set; } = String.Empty;
    public DateTimeOffset? ExpiryDate { get; set; }
    public uint ConcurrencyToken { get; set; }
}
