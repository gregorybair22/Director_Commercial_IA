namespace DirectorComercialIA.Mail.Models;

public class MailEmailSendLog
{
    public int Id { get; set; }
    public int ContactId { get; set; }
    public int PhaseNumber { get; set; }
    public string? GmailMessageId { get; set; }
    public string? GmailThreadId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string BodySnapshot { get; set; } = string.Empty;
    public string SentByUserId { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public EmailSendStatus SendStatus { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DeliveryStatus { get; set; }
    public bool BounceDetected { get; set; }
    public BounceType BounceType { get; set; } = BounceType.None;
    public string? BounceReason { get; set; }
    public bool IsHardBounce { get; set; }

    public MailContact? Contact { get; set; }
}
