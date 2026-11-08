using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Middleware;
using SchoolAdmission.Api.Services;

// Load .env file from root or current folder if exists (Node.js style developer convenience)
LoadDotEnvFiles();

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel port: supports PORT env var, ASPNETCORE_URLS, or defaults to 8080 (per spec)
builder.WebHost.ConfigureKestrel(options =>
{
    var portEnv = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out var customPort))
    {
        options.ListenAnyIP(customPort);
    }
    else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    {
        options.ListenAnyIP(8080);
    }
});

// Configure Database Connection
var connectionString = ResolveConnectionString(builder.Configuration);
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
    });
});

// Add Controllers with strict camelCase JSON serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Swagger / OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for Frontend (React on Vite)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Application Services
builder.Services.AddScoped<IAdmissionService, AdmissionService>();
builder.Services.AddHostedService<HoldCleanupBackgroundService>();

var app = builder.Build();

// Auto-create database tables on startup if running against relational database
using (var scope = app.Services.CreateScope())
{
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

// Middleware Pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();

static void LoadDotEnvFiles()
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
            if (File.Exists(path))
            {
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
    }
    catch
    {
        // Ignore .env read errors if file is locked or unavailable
    }
}

static string ResolveConnectionString(IConfiguration configuration)
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

// Expose Program for WebApplicationFactory in Integration / Concurrency tests
public partial class Program { }
