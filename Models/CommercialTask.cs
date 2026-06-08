using System.ComponentModel.DataAnnotations;

using System.ComponentModel.DataAnnotations;

namespace DirectorComercialIA.Models;

public class CommercialTask
{
    [Key]
    public int Id { get; set; }

    public string? AsanaTaskGid { get; set; }
    public string ProjectGid { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string? SectionGid { get; set; }
    public string SectionName { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? AssigneeName { get; set; }
    public string? AssigneeGid { get; set; }
    public DateTime? DueOn { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsCompleted { get; set; }
    public string? PermalinkUrl { get; set; }
    public string? TagsJson { get; set; }
    public string? CustomFieldsJson { get; set; }
    public int? StoryCount { get; set; }
    public DateTime LastSyncAt { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = "Asana";

    public string LocalStatus { get; set; } = "Nuevo";
    public string Priority { get; set; } = "Media";
    public decimal? EstimatedValue { get; set; }
    public int? Probability { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string ClientType { get; set; } = "Otro";
    public string? ClientName { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactMobile { get; set; }
    public string? ContactRole { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactsJson { get; set; }
    public string? NextAction { get; set; }
    public DateTime? NextActionDate { get; set; }
    public string? InternalNotes { get; set; }
    public string? CommercialOwner { get; set; }
    public bool IsArchivedLocally { get; set; }

    public bool IsLocalStatusManual { get; set; }
    public bool IsPriorityManual { get; set; }
    public bool IsCountryManual { get; set; }
    public bool IsLanguageManual { get; set; }
    public bool IsClientTypeManual { get; set; }
    public bool IsClientNameManual { get; set; }
    public bool IsContactNameManual { get; set; }
    public bool IsContactPhoneManual { get; set; }
    public bool IsContactMobileManual { get; set; }
    public bool IsContactRoleManual { get; set; }
    public bool IsContactEmailManual { get; set; }
    public bool IsNextActionManual { get; set; }
    public bool IsCommercialOwnerManual { get; set; }
    public bool IsProbabilityManual { get; set; }
    public bool IsEstimatedValueManual { get; set; }

    public string? AutoEnrichmentNotes { get; set; }
    public DateTime? LastAutoEnrichedAt { get; set; }

    public int DaysWithoutActivity => (DateTime.UtcNow.Date - ModifiedAt.Date).Days;
    public bool IsOverdue => DueOn.HasValue && DueOn.Value.Date < DateTime.UtcNow.Date && !IsCompleted;
    public bool HasNoDueDate => !DueOn.HasValue && !IsCompleted;
    public bool HasNoAssignee => string.IsNullOrWhiteSpace(AssigneeName) && !IsCompleted;

    public string RiskLevel
    {
        get
        {
            if (IsOverdue || string.IsNullOrWhiteSpace(NextAction) || DaysWithoutActivity > 90) return "Crítico";
            if (DaysWithoutActivity > 60 || Priority == "Crítica") return "Alto";
            if (DaysWithoutActivity > 30 || Priority == "Alta") return "Medio";
            return "Bajo";
        }
    }
}
