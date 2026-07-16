using System.ComponentModel.DataAnnotations;

namespace Unified.UserManagement.Options;

public class UserManagementOptions
{
    public const string SectionName = "UserManagement";

    [Range(1, long.MaxValue, ErrorMessage = "UploadPhotoSizeLimitKb must be greater than 0.")]
    public long UploadPhotoSizeLimitKb { get; set; } = 400;
}
