namespace Sifnic.Api;

public static class ConexionDb
{
    private static string? configuredConnectionString;

    public static string Cadena =>
        Environment.GetEnvironmentVariable("SIFNIC_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__Credito")
        ?? configuredConnectionString
        ?? throw new InvalidOperationException("No se configuro la cadena de conexion Credito.");

    public static void Configure(string? connectionString)
    {
        configuredConnectionString = string.IsNullOrWhiteSpace(connectionString)
            ? null
            : connectionString.Trim();
    }
}
