using System.Reflection;

#if MICROSOFT_OPENAPI
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using StateleSSE.AspNetCore;

namespace StateleSSE.AspNetCore.CodeGen;

/// <summary>
/// Microsoft.AspNetCore.OpenApi operation transformer that adds x-event-source and x-event-type extensions
/// to OpenAPI spec for endpoints marked with [EventSourceEndpoint]
/// </summary>
public sealed class MicrosoftOpenApiEventSourceTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        // Try to get attribute from endpoint metadata (for minimal APIs and controllers)
        var attribute = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<EventSourceEndpointAttribute>()
            .FirstOrDefault();

        // Fallback: Try to get from MethodInfo (for controller actions)
        if (attribute == null && context.Description.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerDescriptor)
        {
            attribute = controllerDescriptor.MethodInfo.GetCustomAttribute<EventSourceEndpointAttribute>();
        }

        if (attribute == null) return Task.CompletedTask;

        operation.Extensions["x-event-source"] = new OpenApiBoolean(true);
        operation.Extensions["x-event-type"] = new OpenApiString(attribute.EventType.Name);

        return Task.CompletedTask;
    }
}
#endif
