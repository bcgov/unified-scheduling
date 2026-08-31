using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Unified.Api.Validators;
using Unified.Infrastructure.Email.Ches;

namespace Unified.Api.Controllers;

public static class ChesTestControllerRegistration
{
    public static IMvcBuilder AddChesTestController(
        this IMvcBuilder mvcBuilder,
        IHostEnvironment environment,
        IConfiguration configuration
    )
    {
        var isEnabled = environment.IsDevelopment() && ChesEmailConfiguration.IsEnabled(configuration);

        if (isEnabled)
            mvcBuilder.Services.AddScoped<TestEmailRequestValidator>();

        mvcBuilder.ConfigureApplicationPartManager(manager =>
            manager.FeatureProviders.Add(new ChesTestControllerFeatureProvider(isEnabled))
        );

        return mvcBuilder;
    }

    private sealed class ChesTestControllerFeatureProvider(bool isEnabled)
        : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            if (!isEnabled)
                feature.Controllers.Remove(typeof(ChesTestController).GetTypeInfo());
        }
    }
}
