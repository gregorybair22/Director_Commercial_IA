using DirectorComercialIA.Data;
using DirectorComercialIA.Data;
using DirectorComercialIA.Models;
using Microsoft.EntityFrameworkCore;

namespace DirectorComercialIA.Services;

public class CommercialDashboardService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly AsanaService _asanaService;
    private readonly ILogger<CommercialDashboardService> _logger;
    private readonly SyncLogService _syncLog;
    private readonly CommercialTaskEnrichmentService _enrichmentService;

    public CommercialDashboardService(
        IDbContextFactory<AppDbContext> dbFactory,
        AsanaService asanaService,
        ILogger<CommercialDashboardService> logger,
        SyncLogService syncLog,
        CommercialTaskEnrichmentService enrichmentService)
    {
        _dbFactory = dbFactory;
        _asanaService = asanaService;
        _logger = logger;
        _syncLog = syncLog;
        _enrichmentService = enrichmentService;
    }

    public async Task<int> SyncFromAsanaAsync(string projectGid)
    {
        _syncLog.LogInfo($"CommercialDashboardService: Iniciando SyncFromAsanaAsync para proyecto {projectGid}...");

        var tasks = await _asanaService.GetTasksByProjectAsync(projectGid);
        var distinctTasks = tasks
            .Where(t => !string.IsNullOrWhiteSpace(t.AsanaTaskGid))
            .GroupBy(t => t.AsanaTaskGid)
            .Select(g => g.First())
            .ToList();

        _syncLog.LogInfo($"CommercialDashboardService: Recibidas {tasks.Count} tareas de Asana ({distinctTasks.Count} únicas)");

        using var db = await _dbFactory.CreateDbContextAsync();

        _logger.LogInformation("SyncFromAsanaAsync: imported {Count} tasks, updating database.", distinctTasks.Count);

        var incomingGids = new HashSet<string>(distinctTasks.Select(t => t.AsanaTaskGid!));

        var existingByGid = await db.CommercialTasks
            .Where(t => t.AsanaTaskGid != null && incomingGids.Contains(t.AsanaTaskGid))
            .ToDictionaryAsync(t => t.AsanaTaskGid!, t => t);

        foreach (var incoming in distinctTasks)
        {
            if (incoming.AsanaTaskGid is null)
            {
                continue;
            }

            if (existingByGid.TryGetValue(incoming.AsanaTaskGid, out var existing))
            {
                // Campos sincronizados desde Asana
                existing.AsanaTaskGid = incoming.AsanaTaskGid;
                existing.ProjectGid = incoming.ProjectGid;
                existing.ProjectName = incoming.ProjectName;
                existing.SectionGid = incoming.SectionGid;
                existing.SectionName = incoming.SectionName;
                existing.Name = incoming.Name;
                existing.Description = incoming.Description;
                existing.AssigneeName = incoming.AssigneeName;
                existing.AssigneeGid = incoming.AssigneeGid;
                existing.DueOn = incoming.DueOn;
                existing.CreatedAt = incoming.CreatedAt;
                existing.ModifiedAt = incoming.ModifiedAt;
                existing.CompletedAt = incoming.CompletedAt;
                existing.IsCompleted = incoming.IsCompleted;
                existing.PermalinkUrl = incoming.PermalinkUrl;
                existing.TagsJson = incoming.TagsJson;
                existing.CustomFieldsJson = incoming.CustomFieldsJson;
                existing.StoryCount = incoming.StoryCount;
                existing.LastSyncAt = DateTime.UtcNow;
                existing.Source = incoming.Source;

                _enrichmentService.EnrichFromAsana(existing);
            }
            else
            {
                _enrichmentService.EnrichFromAsana(incoming);
                db.CommercialTasks.Add(incoming);
            }
        }

        await db.SaveChangesAsync();

        _logger.LogInformation("SyncFromAsanaAsync: database updated.");
        _syncLog.LogInfo($"CommercialDashboardService: Base de datos actualizada con {distinctTasks.Count} tareas");

        return distinctTasks.Count;
    }

    public async Task<List<CommercialTask>> GetTasksAsync(string projectGid)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        return await db.CommercialTasks
            .Where(t => t.ProjectGid == projectGid)
            .OrderByDescending(t => t.ModifiedAt)
            .ToListAsync();
    }
}
