namespace DirectorComercialIA.ViewModels;

public sealed class OpportunityMoveFilter
{
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? Status { get; set; }
    public string? ProjectGid { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Commercial { get; set; }
    public string? SearchText { get; set; }
}

public sealed class OpportunityMoveItemVm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AsanaTaskGid { get; set; }
    public string ProjectGid { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? CommercialOwner { get; set; }
    public DateTime ModifiedAt { get; set; }
}

public sealed class MovePreviewVm
{
    public string DestinationProjectGid { get; set; } = string.Empty;
    public string DestinationProjectName { get; set; } = string.Empty;
    public string DestinationSectionGid { get; set; } = string.Empty;
    public string DestinationSectionName { get; set; } = string.Empty;
    public int TotalSelected { get; set; }
    public int AlreadyInDestination { get; set; }
    public int ReadyToMove { get; set; }
    public List<OpportunityMoveItemVm> Items { get; set; } = [];
}

public sealed class MoveTaskResultVm
{
    public int TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class MoveExecutionVm
{
    public int Total { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<MoveTaskResultVm> Results { get; set; } = [];
}

public sealed class OpportunityMoveHistoryFilterVm
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? MovedBy { get; set; }
    public string? FromProjectGid { get; set; }
    public string? ToProjectGid { get; set; }
    public bool? Success { get; set; }
    public string? SearchText { get; set; }
}

public sealed class OpportunityMoveHistoryItemVm
{
    public int Id { get; set; }
    public DateTime MovedAtUtc { get; set; }
    public string MovedBy { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string FromProjectName { get; set; } = string.Empty;
    public string ToProjectName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
