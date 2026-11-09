using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Extensions;
using SchoolAdmission.Api.Middleware;

// 1. Load .env environment variables (if present)
EnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);

// 2. Configure Server Port (supports PORT env var or defaults to 8080)
builder.WebHost.ConfigureKestrel(options =>
{
    var portEnv = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out var customPort))
    {
        options.ListenAnyIP(customPort);
    }
    else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    {
        options.ListenAnyIP(5000);
    }
});

// 3. Register Application Services & Infrastructure
builder.Services
    .AddDatabaseConfiguration(builder.Configuration)
    .AddApplicationServices()
    .AddApplicationCors()
    .AddApiControllers()
    .AddSwaggerDocumentation();

var app = builder.Build();

// 4. Ensure Database Schema Exists
await app.EnsureDatabaseSchemaAsync();

// 5. Middleware Pipeline
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

// Expose Program for WebApplicationFactory in Integration / Concurrency tests
public partial class Program { }
