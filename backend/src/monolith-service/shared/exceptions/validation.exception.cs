using System.Net;

namespace backend.src.shared.exceptions;

public class ValidationException : BaseApiException
{
    public ValidationException(string message, object? errors = null)
        : base(message, HttpStatusCode.BadRequest, errors)
    {
    }
}