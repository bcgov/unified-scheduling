using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Unified.Scheduling.Controllers;

namespace Unified.Scheduling.OpenApi;

public sealed class SchedulingConcurrencyOperationTransformer : IOpenApiOperationTransformer
{
    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (
            context.Description.ActionDescriptor is not ControllerActionDescriptor action
            || !(
                (
                    action.ControllerTypeInfo.AsType() == typeof(ShiftController)
                    && action.MethodInfo.Name == nameof(ShiftController.UpdateShiftEntry)
                )
                || (
                    action.ControllerTypeInfo.AsType() == typeof(AssignmentController)
                    && action.MethodInfo.Name == nameof(AssignmentController.UpdateAssignmentEntry)
                )
            )
            || operation.Responses is null
            || !operation.Responses.TryGetValue("409", out var response)
            || response.Content is null
        )
            return;

        var problem = await context.GetOrCreateSchemaAsync(
            typeof(ProblemDetails),
            parameterDescription: null,
            cancellationToken
        );
        // Calendar rejections remain application/json; the global concurrency handler
        // writes ProblemDetails. Repeated 409 attributes would overwrite the first shape.
        response.Content["application/problem+json"] = new OpenApiMediaType { Schema = problem };
    }
}
