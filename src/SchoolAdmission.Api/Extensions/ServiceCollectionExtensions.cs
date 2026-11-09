using System.Text.Json;
using System.Text.Json.Serialization;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IClassService, ClassService>();
        services.AddScoped<IHoldService, HoldService>();
        services.AddHostedService<HoldCleanupBackgroundService>();

        return services;
    }

    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        return services;
    }

    public static IServiceCollection AddApplicationCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}
