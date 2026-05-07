using System.Net;
namespace backend.src.shared.exceptions;

public class BaseApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public object? Errors { get; }

    public BaseApiException(
        string message,
        HttpStatusCode statusCode,
        object? errors = null
    ) : base(message)
    {
        StatusCode = statusCode;
        Errors = errors;
    }
}