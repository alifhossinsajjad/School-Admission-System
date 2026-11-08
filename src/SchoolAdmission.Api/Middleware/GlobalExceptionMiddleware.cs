using System.Net;
using System.Text.Json;
using SchoolAdmission.Api.Exceptions;

namespace SchoolAdmission.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException appEx)
        {
            _logger.LogWarning("Application error ({StatusCode}): {Message}", appEx.StatusCode, appEx.Message);
            await HandleExceptionAsync(context, appEx.StatusCode, appEx.Message);
        }
        catch (BadHttpRequestException badHttpEx)
        {
            _logger.LogWarning("Bad HTTP request: {Message}", badHttpEx.Message);
            await HandleExceptionAsync(context, (int)HttpStatusCode.BadRequest, badHttpEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled server error occurred.");
            await HandleExceptionAsync(context, (int)HttpStatusCode.InternalServerError, "An unexpected server error occurred.");
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new
        {
            status = statusCode,
            error = message
        };

        var json = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(json);
    }
}

