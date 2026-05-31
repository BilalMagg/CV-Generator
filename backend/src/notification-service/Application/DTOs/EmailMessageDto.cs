namespace NotificationService.Application.DTOs;

public class EmailMessageDto
{
    public Guid Id { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string Provider { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendEmailDto
{
    public Guid UserId { get; set; }
    public List<string> ToEmails { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? CvPdfUrl { get; set; }
}

public class EmailHistoryResponse
{
    public List<EmailMessageDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int DraftCount { get; set; }
}

public class MailboxStatsDto
{
    public int TotalSent { get; set; }
    public int TotalFailed { get; set; }
    public int TotalContacts { get; set; }
    public int TotalSchedules { get; set; }
    public double SuccessRate { get; set; }
}
