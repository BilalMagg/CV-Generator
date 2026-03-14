using System.Net;

namespace backend.src.shared.exceptions;

public class NotFoundException : BaseApiException
{
    public NotFoundException(string message)
        : base(message, HttpStatusCode.NotFound)
    {
    }
}