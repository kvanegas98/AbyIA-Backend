namespace VariedadesAby.Infrastructure.Helpers;

/// <summary>
/// Resuelve zonas horarias de forma cross-platform (.NET 6+).
/// Windows usa IDs propios ("Central America Standard Time").
/// Linux/Docker/Azure usan IDs IANA ("America/Managua").
/// Este helper intenta el ID configurado y si falla, convierte automáticamente.
/// </summary>
public static class TimeZoneHelper
{
    /// <summary>
    /// Retorna el <see cref="TimeZoneInfo"/> para el ID dado, funcionando tanto en
    /// Windows como en Linux sin cambiar la configuración al desplegar.
    /// </summary>
    public static TimeZoneInfo Resolve(string tzId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Si el ID es Windows → intentar convertir a IANA (para Linux)
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tzId, out var ianaId))
                return TimeZoneInfo.FindSystemTimeZoneById(ianaId);

            // Si el ID es IANA → intentar convertir a Windows (para servidores Windows sin tzdata)
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(tzId, out var windowsId))
                return TimeZoneInfo.FindSystemTimeZoneById(windowsId);

            throw new TimeZoneNotFoundException(
                $"No se encontró la zona horaria '{tzId}' ni su equivalente. " +
                $"Usa 'America/Managua' (IANA) o 'Central America Standard Time' (Windows).");
        }
    }

    /// <summary>Convierte DateTime.UtcNow a la hora local según el ID de zona configurado.</summary>
    public static DateTime NowIn(string tzId) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Resolve(tzId));
}
