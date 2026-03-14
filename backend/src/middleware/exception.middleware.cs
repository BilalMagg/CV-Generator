using System.Net;
using System.Text.Json;
using backend.src.shared.responses;
using backend.src.shared.exceptions;

namespace backend.src.middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleException(context, ex);
        }
    }

    private static Task HandleException(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        int statusCode = StatusCodes.Status500InternalServerError;
        string message = "Internal server error";
        object? errors = null;

        if (exception is BaseApiException apiException)
        {
            statusCode = (int)apiException.StatusCode;
            message = apiException.Message;
            errors = apiException.Errors;
        }

        context.Response.StatusCode = statusCode;

        var response = ApiResponse<object>.ErrorResponse(
            message,
            errors
        );

        var json = JsonSerializer.Serialize(response);

        return context.Response.WriteAsync(json);
    }
}