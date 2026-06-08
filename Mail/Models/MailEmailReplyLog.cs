namespace DirectorComercialIA.Mail.Models;

public class MailEmailReplyLog
{
    public int Id { get; set; }
    public int ContactId { get; set; }
    public string? GmailMessageId { get; set; }
    public string? GmailThreadId { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodySnippet { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    public MailContact? Contact { get; set; }
}
