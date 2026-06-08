namespace DirectorComercialIA.Mail.Models;

public class MailContact
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public string? AssignedUserId { get; set; }

    public int CurrentPhase { get; set; } = 1;
    public ContactWorkflowStatus CurrentStatus { get; set; } = ContactWorkflowStatus.Active;

    public bool IsResponded { get; set; }
    public bool IsDoNotContact { get; set; }
    public bool IsExcluded { get; set; }
    public bool IsBounced { get; set; }
    public BounceType BounceType { get; set; } = BounceType.None;
    public string? BounceReason { get; set; }
    public bool IsHardBounce { get; set; }
    public bool AdminOverrideAllowNextSend { get; set; }

    public int? ImportBatchId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastReplyCheckAt { get; set; }
    public DateTime? LastReplyReceivedAt { get; set; }
    public DateTime? Send1At { get; set; }
    public DateTime? Send2At { get; set; }
    public DateTime? Send3At { get; set; }
    public DateTime? Send4At { get; set; }
    public DateTime? Send5At { get; set; }

    public MailImportBatch? ImportBatch { get; set; }
    public ICollection<MailContactComment> Comments { get; set; } = new List<MailContactComment>();
    public ICollection<MailEmailSendLog> SendLogs { get; set; } = new List<MailEmailSendLog>();
    public ICollection<MailEmailReplyLog> ReplyLogs { get; set; } = new List<MailEmailReplyLog>();
    public ICollection<MailContactPhaseHistory> PhaseHistory { get; set; } = new List<MailContactPhaseHistory>();
}
