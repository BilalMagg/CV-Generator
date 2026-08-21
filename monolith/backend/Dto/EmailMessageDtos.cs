namespace CV_Generator.Dto;

public class EmailMessageDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ContactId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string Provider { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendEmailDto
{
    public List<Guid> RecipientIds { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
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
    public int EmailsSent { get; set; }
    public int ScheduledEmails { get; set; }
    public int Contacts { get; set; }
    public double SuccessRate { get; set; }
}

public class ContactHistoryResponse
{
    public ContactDto Contact { get; set; } = null!;
    public List<EmailMessageDto> Emails { get; set; } = [];
    public int TotalEmails { get; set; }
}
