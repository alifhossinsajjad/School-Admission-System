namespace SchoolAdmission.Api.Common;

public static class EnvLoader
{
    /// <summary>
    /// Loads key-value pairs from .env files into System.Environment if present.
    /// Supports root directory, app base directory, and parent project paths.
    /// </summary>
    public static void Load()
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                Path.Combine(AppContext.BaseDirectory, ".env"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", ".env")
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;

                foreach (var line in File.ReadAllLines(path))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;

                    var parts = trimmed.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim().Trim('"', '\'');
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, value);
                        }
                    }
                }
                break;
            }
        }
        catch
        {
            // Suppress non-critical file read errors during startup
        }
    }
}
