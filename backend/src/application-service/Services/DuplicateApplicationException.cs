using ApplicationService.DTOs;

namespace ApplicationService.Services;

/// <summary>
/// Thrown when creating an application that matches existing ones and AllowDuplicate was not set.
/// Carries the matching applications so callers can surface them to the user.
/// </summary>
public class DuplicateApplicationException : Exception
{
    public DuplicateCheckResponseDto Payload { get; }

    public DuplicateApplicationException(DuplicateCheckResponseDto payload)
        : base($"Found {payload.Matches.Count} existing application(s) matching this company and position")
    {
        Payload = payload;
    }
}
