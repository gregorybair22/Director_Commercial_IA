using DirectorComercialIA.Mail.Models;

namespace DirectorComercialIA.Mail.Abstractions;

public class MailContactListFilterDto
{
    public int Phase { get; set; }
    public string? Search { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? CustomerType { get; set; }
    public string? AssignedUserId { get; set; }
    public bool? Replied { get; set; }
    public bool? Bounced { get; set; }
    public bool? UnknownUserBounce { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class MailContactListItemDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }
    public string? LatestComment { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CurrentPhase { get; set; }
    public bool IsResponded { get; set; }
    public bool IsBounced { get; set; }
    public BounceType BounceType { get; set; }
    public string? BounceReason { get; set; }
    public bool PhaseEmailSent { get; set; }
}

public class MailPagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class MailPhaseMetricsDto
{
    public int Phase { get; set; }
    public int TotalInPhase { get; set; }
    public int Replied { get; set; }
    public int NotReplied { get; set; }
    public int Bounced { get; set; }
}

public interface IMailContactQueryService
{
    Task<MailPagedResultDto<MailContactListItemDto>> ListAsync(MailContactListFilterDto filter, CancellationToken ct = default);
    Task<MailPhaseMetricsDto> GetPhaseMetricsAsync(int phase, CancellationToken ct = default);
}
