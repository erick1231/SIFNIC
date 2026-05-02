using Microsoft.Data.SqlClient;
using Sifnic.Api.Creditos;

namespace Sifnic.Api.Security;

public sealed class ApiSessionGuardMiddleware
{
    private static readonly HashSet<string> PublicEndpoints = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Seguridad/Login",
        "/Seguridad/CambiarClave",
        "/Seguridad/Logout",
    };

    private readonly RequestDelegate _next;

    public ApiSessionGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsPublicRequest(context.Request))
        {
            await _next(context);
            return;
        }

        var queryToken = context.Request.Query["sessionToken"].ToString().Trim();
        if (!string.IsNullOrWhiteSpace(queryToken) &&
            string.IsNullOrWhiteSpace(context.Request.Headers["X-Session-Token"].ToString()))
        {
            context.Request.Headers["X-Session-Token"] = queryToken;
        }

        try
        {
            await using var connection = new SqlConnection(ConexionDb.Cadena);
            await connection.OpenAsync(context.RequestAborted);

            var session = CreditPortfolioSecuritySupport.ResolveSession(context.Request, connection);
            if (session is null)
            {
                await WriteUnauthorized(context);
                return;
            }

            context.Items["SifnicSession"] = session;
            await _next(context);
        }
        catch
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                ok = false,
                message = "No se pudo validar la seguridad de la solicitud.",
            });
        }
    }

    private static bool IsPublicRequest(HttpRequest request)
    {
        if (HttpMethods.IsOptions(request.Method))
        {
            return true;
        }

        var path = request.Path.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return true;
        }

        if (path.StartsWith("/App", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return PublicEndpoints.Contains(path);
    }

    private static async Task WriteUnauthorized(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new
        {
            ok = false,
            message = "Sesion invalida o expirada.",
        });
    }
}
