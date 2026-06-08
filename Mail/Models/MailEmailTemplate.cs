namespace DirectorComercialIA.Mail.Models;

public class MailEmailTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PhaseNumber { get; set; }
    public string Language { get; set; } = "en";
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
