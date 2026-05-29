using System;

namespace Unified.UserManagement.Models;

public partial class RegionDto
{
    public int Id { get; set; }
    public int? JustinId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset? ExpiryDate { get; set; }
    public uint ConcurrencyToken { get; set; }
}
