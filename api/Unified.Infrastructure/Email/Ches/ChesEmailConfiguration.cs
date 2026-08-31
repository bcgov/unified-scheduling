using Microsoft.Extensions.Configuration;

namespace Unified.Infrastructure.Email.Ches;

public static class ChesEmailConfiguration
{
    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetSection(ChesOptions.SectionName).GetValue<bool?>(nameof(ChesOptions.Enabled))
        ?? new ChesOptions().Enabled;
}
