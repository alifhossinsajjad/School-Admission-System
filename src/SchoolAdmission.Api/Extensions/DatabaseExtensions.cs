using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ResolveConnectionString(configuration);

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            });
        });

        return services;
    }

    public static async Task EnsureDatabaseSchemaAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (db.Database.IsRelational())
            {
                logger.LogInformation("Ensuring database schema exists...");
                await db.Database.EnsureCreatedAsync();
                logger.LogInformation("Database schema ready.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure database created at startup. If DB is starting up, it will connect upon first request.");
        }
    }

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var raw = Environment.GetEnvironmentVariable("DATABASE_URL")
               ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
               ?? configuration.GetConnectionString("DefaultConnection")
               ?? "Host=localhost;Port=5432;Database=school_admission_db;Username=postgres;Password=postgres";

        if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(raw);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
            var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var db = uri.AbsolutePath.TrimStart('/');

            var ssl = (raw.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase) || host.Contains("neon.tech"))
                ? ";SSL Mode=Require;Trust Server Certificate=true"
                : "";

            return $"Host={host};Port={port};Database={db};Username={user};Password={pass}{ssl}";
        }

        return raw;
    }
}
