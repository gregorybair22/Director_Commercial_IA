namespace DirectorComercialIA.Mail.Models;

public class MailImportBatch
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? CampaignName { get; set; }
    public string ImportedByUserId { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public int NewRows { get; set; }
    public int UpdatedRows { get; set; }
    public int IgnoredDuplicates { get; set; }

    public ICollection<MailContact> Contacts { get; set; } = new List<MailContact>();
}
