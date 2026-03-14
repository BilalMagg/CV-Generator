namespace backend.src.shared.exceptions;

using System.Net;
public class ConflictException : BaseApiException
{
    public ConflictException(string message)
        : base(message, HttpStatusCode.Conflict)
    {
    }
}