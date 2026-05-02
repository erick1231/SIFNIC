using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sifnic.Api.Security;

public sealed class SafeErrorResponseFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult objectResult ||
            objectResult.StatusCode is null or < 500)
        {
            return;
        }

        objectResult.Value = SanitizePayload(objectResult.Value);
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    private static object SanitizePayload(object? value)
    {
        if (value is null || value is string)
        {
            return new
            {
                ok = false,
                message = "Ocurrio un error interno. Contacta al administrador del sistema.",
            };
        }

        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.GetType().GetProperties())
        {
            if (string.Equals(property.Name, "detail", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            payload[property.Name] = property.GetValue(value);
        }

        payload["ok"] = false;
        if (!payload.ContainsKey("message") || string.IsNullOrWhiteSpace(Convert.ToString(payload["message"])))
        {
            payload["message"] = "Ocurrio un error interno. Contacta al administrador del sistema.";
        }

        return payload;
    }
}
