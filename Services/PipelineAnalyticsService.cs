using DirectorComercialIA.Data;
using DirectorComercialIA.Data;
using DirectorComercialIA.Models;
using DirectorComercialIA.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DirectorComercialIA.Services;

public class PipelineAnalyticsService
{
    private const decimal MaxReasonableEstimatedValue = 100_000_000m;

    private static readonly string[] StageOrder =
    [
        "Nuevo",
        "Contactado",
        "Seguimiento",
        "Oferta enviada",
        "Negociación",
        "Pendiente aceptación",
        "Ganado",
        "Perdido",
        "Dormido"
    ];

    private static readonly string[] FunnelOrder =
    [
        "Nuevo",
        "Contactado",
        "Seguimiento",
        "Oferta enviada",
        "Negociación",
        "Pendiente aceptación"
    ];

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public PipelineAnalyticsService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<PipelineDashboardVm> GetDashboardAsync(PipelineFilter filter)
    {
        using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.CommercialTasks.AsNoTracking()
            .Where(t => t.ProjectGid == filter.ProjectGid)
            .Where(t => !t.IsArchivedLocally);

        if (!string.IsNullOrWhiteSpace(filter.Responsible))
            query = query.Where(t => (t.CommercialOwner ?? t.AssigneeName ?? "").Contains(filter.Responsible));

        if (!string.IsNullOrWhiteSpace(filter.Country))
            query = query.Where(t => (t.Country ?? "").Contains(filter.Country));

        if (!string.IsNullOrWhiteSpace(filter.ClientType))
            query = query.Where(t => t.ClientType == filter.ClientType);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(t => t.LocalStatus == filter.Status);

        if (filter.DateFrom.HasValue)
            query = query.Where(t => t.ModifiedAt.Date >= filter.DateFrom.Value.Date);

        if (filter.DateTo.HasValue)
            query = query.Where(t => t.ModifiedAt.Date <= filter.DateTo.Value.Date);

        var tasks = await query.ToListAsync();

        var vm = new PipelineDashboardVm
        {
            Kpis = BuildKpis(tasks),
            StageStats = BuildStageStats(tasks),
            SectionStats = BuildSectionStats(tasks),
            Funnel = BuildFunnel(tasks),
            SectionFunnel = BuildSectionFunnel(tasks),
            Bottlenecks = BuildBottlenecks(tasks),
            ByResponsible = BuildByResponsible(tasks),
            ByCountry = BuildByCountry(tasks),
            Opportunities = BuildOpportunities(tasks),
            Critical = BuildCritical(tasks)
        };

        return vm;
    }

    private static PipelineKpi BuildKpis(List<CommercialTask> tasks)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var totalValue = tasks.Sum(t => NormalizeEstimatedValue(t.EstimatedValue) ?? 0m);
        var weighted = tasks.Sum(t => (NormalizeEstimatedValue(t.EstimatedValue) ?? 0m) * ((t.Probability ?? 0) / 100m));
        var won = tasks.Count(t => t.LocalStatus == "Ganado" && t.ModifiedAt >= monthStart);
        var lost = tasks.Count(t => t.LocalStatus == "Perdido" && t.ModifiedAt >= monthStart);

        var totalForConversion = tasks.Count(t => t.LocalStatus != "Dormido");
        var conversion = totalForConversion == 0 ? 0 : (decimal)tasks.Count(t => t.LocalStatus == "Ganado") / totalForConversion * 100m;

        var avgPipeline = tasks.Count == 0
            ? 0
            : tasks.Average(t => (now.Date - (t.CreatedAt?.Date ?? t.ModifiedAt.Date)).TotalDays);

        var wonWithDates = tasks.Where(t => t.LocalStatus == "Ganado" && t.CreatedAt.HasValue).ToList();
        var avgToWin = wonWithDates.Count == 0
            ? 0
            : wonWithDates.Average(t => ((t.CompletedAt ?? t.ModifiedAt) - t.CreatedAt!.Value).TotalDays);

        return new PipelineKpi
        {
            TotalOpportunities = tasks.Count,
            TotalValue = totalValue,
            WeightedValue = weighted,
            WonThisMonth = won,
            LostThisMonth = lost,
            ConversionRate = Math.Round(conversion, 2),
            AvgDaysInPipeline = Math.Round(avgPipeline, 1),
            AvgDaysToWin = Math.Round(avgToWin, 1)
        };
    }

    private static List<PipelineStageStat> BuildStageStats(List<CommercialTask> tasks)
    {
        var total = tasks.Count;

        return StageOrder.Select(stage =>
        {
            var inStage = tasks.Where(t => t.LocalStatus == stage).ToList();
            var count = inStage.Count;
            var stageValue = inStage.Sum(t => NormalizeEstimatedValue(t.EstimatedValue) ?? 0m);
            var weighted = inStage.Sum(t => (NormalizeEstimatedValue(t.EstimatedValue) ?? 0m) * ((t.Probability ?? 0) / 100m));

            return new PipelineStageStat
            {
                Stage = stage,
                Count = count,
                TotalValue = stageValue,
                WeightedValue = weighted,
                Percentage = total == 0 ? 0 : Math.Round((decimal)count / total * 100m, 2)
            };
        }).ToList();
    }

    private static List<PipelineStageStat> BuildSectionStats(List<CommercialTask> tasks)
    {
        var total = tasks.Count;

        return tasks
            .GroupBy(t => string.IsNullOrWhiteSpace(t.SectionName) ? "Sin sección" : t.SectionName)
            .Select(g => new PipelineStageStat
            {
                Stage = g.Key,
                Count = g.Count(),
                TotalValue = g.Sum(t => NormalizeEstimatedValue(t.EstimatedValue) ?? 0m),
                WeightedValue = g.Sum(t => (NormalizeEstimatedValue(t.EstimatedValue) ?? 0m) * ((t.Probability ?? 0) / 100m)),
                Percentage = total == 0 ? 0 : Math.Round((decimal)g.Count() / total * 100m, 2)
            })
            .OrderByDescending(x => x.TotalValue)
            .ThenByDescending(x => x.Count)
            .ToList();
    }

    private static List<FunnelStep> BuildFunnel(List<CommercialTask> tasks)
    {
        var counts = FunnelOrder
            .Select(stage => new
            {
                Stage = stage,
                Count = tasks.Count(t => string.Equals(t.LocalStatus, stage, StringComparison.OrdinalIgnoreCase))
            })
            .ToList();

        var totalFunnel = counts.Sum(c => c.Count);

        var result = new List<FunnelStep>();
        for (var i = 0; i < counts.Count; i++)
        {
            var percentOfTotal = totalFunnel == 0
                ? 0
                : Math.Round((decimal)counts[i].Count / totalFunnel * 100m, 2);

            result.Add(new FunnelStep
            {
                Stage = counts[i].Stage,
                Count = counts[i].Count,
                WidthPercent = percentOfTotal,
                ConversionFromPreviousPercent = percentOfTotal
            });
        }

        return result;
    }

    private static List<FunnelStep> BuildSectionFunnel(List<CommercialTask> tasks)
    {
        var sections = tasks
            .GroupBy(t => string.IsNullOrWhiteSpace(t.SectionName) ? "Sin sección" : t.SectionName)
            .Select(g => new { Section = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var total = sections.Sum(s => s.Count);
        var result = new List<FunnelStep>();

        foreach (var section in sections)
        {
            var percent = total == 0 ? 0 : Math.Round((decimal)section.Count / total * 100m, 2);
            result.Add(new FunnelStep
            {
                Stage = section.Section,
                Count = section.Count,
                WidthPercent = percent,
                ConversionFromPreviousPercent = percent
            });
        }

        return result;
    }

    private static List<BottleneckStat> BuildBottlenecks(List<CommercialTask> tasks)
    {
        return tasks
            .GroupBy(t => t.LocalStatus)
            .Select(g => new BottleneckStat
            {
                Stage = g.Key,
                AvgDays = Math.Round(g.Average(t => (double)t.DaysWithoutActivity), 1)
            })
            .OrderByDescending(x => x.AvgDays)
            .ToList();
    }

    private static List<ResponsibleStat> BuildByResponsible(List<CommercialTask> tasks)
    {
        return tasks
            .GroupBy(t => string.IsNullOrWhiteSpace(t.CommercialOwner) ? (t.AssigneeName ?? "Sin responsable") : t.CommercialOwner!)
            .Select(g => new ResponsibleStat
            {
                Responsible = g.Key,
                Count = g.Count(),
                TotalValue = g.Sum(x => NormalizeEstimatedValue(x.EstimatedValue) ?? 0m)
            })
            .OrderByDescending(x => x.TotalValue)
            .ToList();
    }

    private static List<CountryStat> BuildByCountry(List<CommercialTask> tasks)
    {
        return tasks
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Country) ? "Sin país" : t.Country!)
            .Select(g => new CountryStat
            {
                Country = g.Key,
                Count = g.Count(),
                TotalValue = g.Sum(x => NormalizeEstimatedValue(x.EstimatedValue) ?? 0m)
            })
            .OrderByDescending(x => x.TotalValue)
            .ToList();
    }

    private static List<CriticalOpportunityVm> BuildCritical(List<CommercialTask> tasks)
    {
        return tasks
            .Select(t => new
            {
                Task = t,
                Score =
                    (t.IsOverdue ? 1000 : 0) +
                    (string.IsNullOrWhiteSpace(t.NextAction) ? 500 : 0) +
                    (t.DaysWithoutActivity > 90 ? 250 : 0) +
                    (t.Priority == "Crítica" ? 100 : 0) +
                    t.DaysWithoutActivity
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => NormalizeEstimatedValue(x.Task.EstimatedValue) ?? 0)
            .Take(20)
            .Select(x => new CriticalOpportunityVm
            {
                Id = x.Task.Id,
                Name = x.Task.Name,
                Status = x.Task.LocalStatus,
                Responsible = x.Task.CommercialOwner ?? x.Task.AssigneeName ?? "Sin responsable",
                DaysWithoutActivity = x.Task.DaysWithoutActivity,
                EstimatedValue = NormalizeEstimatedValue(x.Task.EstimatedValue),
                IsOverdue = x.Task.IsOverdue,
                HasNoNextAction = string.IsNullOrWhiteSpace(x.Task.NextAction),
                IsCriticalPriority = x.Task.Priority == "Crítica"
            })
            .ToList();
    }

    private static List<PipelineOpportunityVm> BuildOpportunities(List<CommercialTask> tasks)
    {
        return tasks
            .Select(t => new PipelineOpportunityVm
            {
                Id = t.Id,
                Name = t.Name,
                Section = t.SectionName,
                Country = string.IsNullOrWhiteSpace(t.Country) ? "Sin país" : t.Country!,
                Status = t.LocalStatus,
                Responsible = t.CommercialOwner ?? t.AssigneeName ?? "Sin responsable",
                DaysWithoutActivity = t.DaysWithoutActivity,
                EstimatedValue = NormalizeEstimatedValue(t.EstimatedValue),
                RiskLevel = t.RiskLevel
            })
            .OrderByDescending(t => t.DaysWithoutActivity)
            .ThenByDescending(t => t.EstimatedValue ?? 0)
            .ToList();
    }

    private static decimal? NormalizeEstimatedValue(decimal? value)
    {
        if (!value.HasValue)
            return null;

        var v = value.Value;
        if (v <= 0 || v > MaxReasonableEstimatedValue)
            return null;

        return v;
    }
}
