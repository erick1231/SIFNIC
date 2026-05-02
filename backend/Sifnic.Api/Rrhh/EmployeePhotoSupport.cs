using System.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace Sifnic.Api.Rrhh;

public static class EmployeePhotoSupport
{
    private const long MaxPhotoSizeBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
    };

    public static string? ValidateUpload(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length <= 0)
        {
            return "Selecciona una imagen para subir.";
        }

        if (archivo.Length > MaxPhotoSizeBytes)
        {
            return "La imagen supera el limite de 5 MB.";
        }

        var extension = Path.GetExtension(archivo.FileName)?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return "La imagen debe estar en formato PNG, JPG, JPEG o WEBP.";
        }

        return null;
    }

    public static string SavePhotoFile(
        IWebHostEnvironment environment,
        IFormFile archivo,
        string employeeCode)
    {
        var extension = Path.GetExtension(archivo.FileName)?.Trim().ToLowerInvariant();
        var safeCode = string.Concat(
            (employeeCode ?? "empleado")
                .Trim()
                .ToUpperInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));

        var fileName = $"{safeCode}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}";
        var relativeFolder = Path.Combine("uploads", "profile");
        var physicalFolder = Path.Combine(environment.WebRootPath, relativeFolder);
        Directory.CreateDirectory(physicalFolder);

        var physicalPath = Path.Combine(physicalFolder, fileName);
        using (var stream = File.Create(physicalPath))
        {
            archivo.CopyTo(stream);
        }

        return "/" + Path.Combine(relativeFolder, fileName).Replace("\\", "/");
    }

    public static string? GetPhotoUrl(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idEmpleado)
    {
        const string sql = """
            SELECT TOP (1) foto_perfil_url
            FROM rrhh.empleado
            WHERE id_empleado = @id_empleado;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;

        var result = command.ExecuteScalar();
        return result is DBNull or null ? null : Convert.ToString(result);
    }

    public static void UpdatePhotoUrl(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idEmpleado,
        string? photoUrl)
    {
        const string sql = """
            UPDATE rrhh.empleado
            SET
                foto_perfil_url = @foto_perfil_url,
                fecha_actualizacion = SYSDATETIME()
            WHERE id_empleado = @id_empleado;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        command.Parameters.Add("@foto_perfil_url", SqlDbType.NVarChar, 1000).Value =
            string.IsNullOrWhiteSpace(photoUrl) ? DBNull.Value : photoUrl;
        command.ExecuteNonQuery();
    }

    public static void DeleteManagedPhoto(
        IWebHostEnvironment environment,
        string? currentPhotoUrl,
        string? replacementPhotoUrl = null)
    {
        if (string.IsNullOrWhiteSpace(currentPhotoUrl) ||
            !currentPhotoUrl.StartsWith("/uploads/profile/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentPhotoUrl, replacementPhotoUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relativePath = currentPhotoUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
        var physicalPath = Path.Combine(environment.WebRootPath, relativePath);

        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }
    }
}
