namespace DirectorComercialIA.Mail.Models;

public class MailContactComment
{
    public int Id { get; set; }
    public int ContactId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public MailContact? Contact { get; set; }
}
