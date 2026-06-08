namespace DirectorComercialIA.Mail.Models;

public class MailGmailConnection
{
    public int Id { get; set; }
    public string ConnectedEmail { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string RefreshTokenProtected { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
