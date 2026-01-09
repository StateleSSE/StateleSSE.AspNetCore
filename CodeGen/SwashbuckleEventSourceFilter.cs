using System.Reflection;
using StateleSSE.AspNetCore;

#if SWASHBUCKLE
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace StateleSSE.AspNetCore.CodeGen;

/// <summary>
/// Swashbuckle operation filter that adds x-event-source and x-event-type extensions
/// to OpenAPI spec for endpoints marked with [EventSourceEndpoint]
/// </summary>
public sealed class SwashbuckleEventSourceFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attribute = context.MethodInfo.GetCustomAttribute<EventSourceEndpointAttribute>();
        if (attribute == null) return;

        operation.Extensions["x-event-source"] = new OpenApiBoolean(true);
        operation.Extensions["x-event-type"] = new OpenApiString(attribute.EventType.Name);
    }
}
#endif
