using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace Unified.DataMigration.Controllers;

/// <summary>
/// CSRF hardening for cookie-authenticated (SameSite=None) migration operations. Unsafe requests must declare
/// <c>application/json</c>, which a cross-site browser can only send after a CORS preflight. Unlike
/// <see cref="ConsumesAttribute"/>, a missing Content-Type is also refused so body-less no-cors POSTs are blocked.
/// Runs after authorization and before model binding or any audited operation.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireJsonContentTypeAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var request = context.HttpContext.Request;
        if (
            HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method)
        )
        {
            return;
        }

        if (
            !MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType)
            || !mediaType.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
        )
        {
            context.Result = new UnsupportedMediaTypeResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
