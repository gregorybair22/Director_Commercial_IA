using System.ComponentModel.DataAnnotations;

namespace DirectorComercialIA.Models;

public class OpportunityMoveHistory
{
    [Key]
    public int Id { get; set; }

    public int? CommercialTaskId { get; set; }
    public string? AsanaTaskGid { get; set; }
    public string TaskName { get; set; } = string.Empty;

    public string FromProjectGid { get; set; } = string.Empty;
    public string FromProjectName { get; set; } = string.Empty;
    public string ToProjectGid { get; set; } = string.Empty;
    public string ToProjectName { get; set; } = string.Empty;

    public string MovedBy { get; set; } = "local-user";
    public DateTime MovedAtUtc { get; set; } = DateTime.UtcNow;

    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
