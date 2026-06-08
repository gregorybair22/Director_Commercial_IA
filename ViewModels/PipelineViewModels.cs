namespace DirectorComercialIA.ViewModels;

public sealed class PipelineFilter
{
    public string ProjectGid { get; set; } = "";
    public string? Responsible { get; set; }
    public string? Country { get; set; }
    public string? ClientType { get; set; }
    public string? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

public sealed class PipelineKpi
{
    public int TotalOpportunities { get; set; }
    public decimal TotalValue { get; set; }
    public decimal WeightedValue { get; set; }
    public int WonThisMonth { get; set; }
    public int LostThisMonth { get; set; }
    public decimal ConversionRate { get; set; }
    public double AvgDaysInPipeline { get; set; }
    public double AvgDaysToWin { get; set; }
}

public sealed class PipelineStageStat
{
    public string Stage { get; set; } = "";
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
    public decimal WeightedValue { get; set; }
    public decimal Percentage { get; set; }
}

public sealed class FunnelStep
{
    public string Stage { get; set; } = "";
    public int Count { get; set; }
    public decimal WidthPercent { get; set; }
    public decimal? ConversionFromPreviousPercent { get; set; }
}

public sealed class BottleneckStat
{
    public string Stage { get; set; } = "";
    public double AvgDays { get; set; }
}

public sealed class ResponsibleStat
{
    public string Responsible { get; set; } = "Sin responsable";
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
}

public sealed class CountryStat
{
    public string Country { get; set; } = "Sin país";
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
}

public sealed class CriticalOpportunityVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public string Responsible { get; set; } = "Sin responsable";
    public int DaysWithoutActivity { get; set; }
    public decimal? EstimatedValue { get; set; }
    public bool IsOverdue { get; set; }
    public bool HasNoNextAction { get; set; }
    public bool IsCriticalPriority { get; set; }
}

public sealed class PipelineOpportunityVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Section { get; set; } = "";
    public string Country { get; set; } = "Sin país";
    public string Status { get; set; } = "";
    public string Responsible { get; set; } = "Sin responsable";
    public int DaysWithoutActivity { get; set; }
    public decimal? EstimatedValue { get; set; }
    public string RiskLevel { get; set; } = "Bajo";
}

public sealed class PipelineDashboardVm
{
    public PipelineKpi Kpis { get; set; } = new();
    public List<PipelineStageStat> StageStats { get; set; } = [];
    public List<PipelineStageStat> SectionStats { get; set; } = [];
    public List<FunnelStep> Funnel { get; set; } = [];
    public List<FunnelStep> SectionFunnel { get; set; } = [];
    public List<BottleneckStat> Bottlenecks { get; set; } = [];
    public List<ResponsibleStat> ByResponsible { get; set; } = [];
    public List<CountryStat> ByCountry { get; set; } = [];
    public List<PipelineOpportunityVm> Opportunities { get; set; } = [];
    public List<CriticalOpportunityVm> Critical { get; set; } = [];
}
