using System.IO;
using Sifnic.Api;
using Sifnic.Api.Security;
using Microsoft.Extensions.FileProviders;

static string? FindProjectRoot(string startDirectory)
{
    var current = new DirectoryInfo(startDirectory);

    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "Sifnic.Api.csproj")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    return null;
}

static string ResolveAppRoot(string baseDirectory)
{
    var bundledWebRoot = Path.Combine(baseDirectory, "wwwroot");
    if (Directory.Exists(bundledWebRoot))
    {
        return baseDirectory;
    }

    var workingDirectory = Directory.GetCurrentDirectory();
    return FindProjectRoot(workingDirectory)
        ?? FindProjectRoot(baseDirectory)
        ?? Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", ".."));
}

var baseDirectory = AppContext.BaseDirectory;
var appRootPath = ResolveAppRoot(baseDirectory);
var webRootPath = Path.Combine(appRootPath, "wwwroot");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = appRootPath,
    WebRootPath = webRootPath,
});

ConexionDb.Configure(builder.Configuration.GetConnectionString("Credito"));

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<SafeErrorResponseFilter>();
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new
        {
            ok = false,
            message = "Ocurrio un error interno. Contacta al administrador del sistema.",
        });
    });
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRootPath),
});
app.UseRouting();
app.UseMiddleware<ApiSessionGuardMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=App}/{action=Login}/{id?}");

var appUrl =
    Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
    ?? builder.Configuration["urls"]
    ?? "http://localhost:5277";

app.Run(appUrl);
