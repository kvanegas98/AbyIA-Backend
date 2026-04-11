namespace VariedadesAby.Api.Helpers;

public static class TimeZoneHelper
{
    public static TimeZoneInfo GetNicaraguaTimeZone()
    {
        try
        {
            // Windows
            return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                // Linux
                return TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
            }
            catch
            {
                // Fallback: UTC-6
                return TimeZoneInfo.CreateCustomTimeZone(
                    "Nicaragua Standard Time",
                    new TimeSpan(-6, 0, 0),
                    "Nicaragua Standard Time",
                    "Nicaragua Standard Time");
            }
        }
    }

    public static DateTime GetNicaraguaTime()
    {
        var nicaraguaZone = GetNicaraguaTimeZone();
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, nicaraguaZone);
    }
}
