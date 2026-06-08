namespace DirectorComercialIA.Mail.Models;

public class MailContactPhaseHistory
{
    public int Id { get; set; }
    public int ContactId { get; set; }
    public int FromPhase { get; set; }
    public int ToPhase { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public MailContact? Contact { get; set; }
}
