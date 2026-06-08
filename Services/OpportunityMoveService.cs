using DirectorComercialIA.Models;
using DirectorComercialIA.Repositories;
using DirectorComercialIA.ViewModels;
using System.Text;
using DirectorComercialIA.Models;

namespace DirectorComercialIA.Services;

public class OpportunityMoveService
{
    private readonly IOpportunityRepository _opportunityRepository;
    private readonly IMoveHistoryRepository _historyRepository;
    private readonly AsanaService _asanaService;

    public OpportunityMoveService(
        IOpportunityRepository opportunityRepository,
        IMoveHistoryRepository historyRepository,
        AsanaService asanaService)
    {
        _opportunityRepository = opportunityRepository;
        _historyRepository = historyRepository;
        _asanaService = asanaService;
    }

    public async Task<List<OpportunityMoveItemVm>> SearchAsync(OpportunityMoveFilter filter)
    {
        var tasks = await _opportunityRepository.SearchAsync(filter);
        return tasks.Select(MapItem).ToList();
    }

    public Task<List<string>> GetCountriesByProjectAsync(string? projectGid)
    {
        return _opportunityRepository.GetCountriesByProjectAsync(projectGid);
    }

    public async Task<MovePreviewVm> BuildPreviewAsync(
        IEnumerable<int> selectedIds,
        string destinationProjectGid,
        string destinationProjectName,
        string destinationSectionGid,
        string destinationSectionName)
    {
        var tasks = await _opportunityRepository.GetByIdsAsync(selectedIds);
        var alreadyInDestination = tasks.Count(t => t.ProjectGid == destinationProjectGid);

        return new MovePreviewVm
        {
            DestinationProjectGid = destinationProjectGid,
            DestinationProjectName = destinationProjectName,
            DestinationSectionGid = destinationSectionGid,
            DestinationSectionName = destinationSectionName,
            TotalSelected = tasks.Count,
            AlreadyInDestination = alreadyInDestination,
            ReadyToMove = tasks.Count - alreadyInDestination,
            Items = tasks.Select(MapItem).ToList()
        };
    }

    public async Task<List<OpportunityMoveHistoryItemVm>> GetHistoryAsync(OpportunityMoveHistoryFilterVm filter)
    {
        var fromUtc = filter.FromDate?.Date;
        var toUtc = filter.ToDate?.Date.AddDays(1).AddTicks(-1);

        var rows = await _historyRepository.GetAsync(fromUtc, toUtc, filter.MovedBy, filter.FromProjectGid, filter.ToProjectGid, filter.Success, filter.SearchText);
        return rows.Select(h => new OpportunityMoveHistoryItemVm
        {
            Id = h.Id,
            MovedAtUtc = h.MovedAtUtc,
            MovedBy = h.MovedBy,
            TaskName = h.TaskName,
            FromProjectName = h.FromProjectName,
            ToProjectName = h.ToProjectName,
            Success = h.Success,
            ErrorMessage = h.ErrorMessage
        }).ToList();
    }

    public async Task<string> ExportHistoryCsvAsync(OpportunityMoveHistoryFilterVm filter)
    {
        var rows = await GetHistoryAsync(filter);
        var sb = new StringBuilder();
        sb.AppendLine("Id,FechaUTC,Usuario,Tarea,ProyectoOrigen,ProyectoDestino,Resultado,Error");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                Csv(r.Id.ToString()),
                Csv(r.MovedAtUtc.ToString("yyyy-MM-dd HH:mm:ss")),
                Csv(r.MovedBy),
                Csv(r.TaskName),
                Csv(r.FromProjectName),
                Csv(r.ToProjectName),
                Csv(r.Success ? "OK" : "ERROR"),
                Csv(r.ErrorMessage ?? string.Empty)));
        }

        return sb.ToString();
    }

    public async Task<MoveExecutionVm> MoveAsync(
        IEnumerable<int> selectedIds,
        string destinationProjectGid,
        string destinationProjectName,
        string destinationSectionGid,
        string destinationSectionName,
        string movedBy)
    {
        var tasks = await _opportunityRepository.GetByIdsAsync(selectedIds);
        var results = new List<MoveTaskResultVm>();
        var histories = new List<OpportunityMoveHistory>();

        foreach (var task in tasks)
        {
            var fromProjectGid = task.ProjectGid;
            var fromProjectName = task.ProjectName;

            if (task.ProjectGid == destinationProjectGid)
            {
                results.Add(new MoveTaskResultVm
                {
                    TaskId = task.Id,
                    TaskName = task.Name,
                    Success = false,
                    Message = "La oportunidad ya pertenece al proyecto destino."
                });

                histories.Add(CreateHistory(task, movedBy, fromProjectGid, fromProjectName, destinationProjectGid, destinationProjectName, false, "Ya está en el proyecto destino"));
                continue;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(task.AsanaTaskGid))
                {
                    await _asanaService.AddTaskToProjectAsync(task.AsanaTaskGid, destinationProjectGid);
                    if (!string.IsNullOrWhiteSpace(destinationSectionGid))
                    {
                        await _asanaService.AddTaskToSectionAsync(task.AsanaTaskGid, destinationSectionGid);
                    }
                    await _asanaService.RemoveTaskFromProjectAsync(task.AsanaTaskGid, fromProjectGid);
                }

                task.ProjectGid = destinationProjectGid;
                task.ProjectName = destinationProjectName;
                if (!string.IsNullOrWhiteSpace(destinationSectionGid))
                {
                    task.SectionGid = destinationSectionGid;
                    task.SectionName = destinationSectionName;
                }
                task.LastSyncAt = DateTime.UtcNow;

                results.Add(new MoveTaskResultVm
                {
                    TaskId = task.Id,
                    TaskName = task.Name,
                    Success = true,
                    Message = "Movida correctamente."
                });

                histories.Add(CreateHistory(task, movedBy, fromProjectGid, fromProjectName, destinationProjectGid, destinationProjectName, true, null));
            }
            catch (Exception ex)
            {
                results.Add(new MoveTaskResultVm
                {
                    TaskId = task.Id,
                    TaskName = task.Name,
                    Success = false,
                    Message = ex.Message
                });

                histories.Add(CreateHistory(task, movedBy, fromProjectGid, fromProjectName, destinationProjectGid, destinationProjectName, false, ex.Message));
            }
        }

        await _opportunityRepository.SaveChangesAsync();
        await _historyRepository.AddRangeAsync(histories);

        return new MoveExecutionVm
        {
            Total = results.Count,
            SuccessCount = results.Count(r => r.Success),
            FailedCount = results.Count(r => !r.Success),
            Results = results
        };
    }

    public Task<List<AsanaSectionOption>> GetSectionsByProjectAsync(string projectGid)
    {
        return _asanaService.GetProjectSectionsAsync(projectGid);
    }

    private static OpportunityMoveItemVm MapItem(CommercialTask task)
    {
        return new OpportunityMoveItemVm
        {
            Id = task.Id,
            Name = task.Name,
            AsanaTaskGid = task.AsanaTaskGid,
            ProjectGid = task.ProjectGid,
            ProjectName = task.ProjectName,
            Status = task.LocalStatus,
            Country = task.Country,
            Language = task.Language,
            CommercialOwner = task.CommercialOwner ?? task.AssigneeName,
            ModifiedAt = task.ModifiedAt
        };
    }

    private static OpportunityMoveHistory CreateHistory(
        CommercialTask task,
        string movedBy,
        string fromProjectGid,
        string fromProjectName,
        string toProjectGid,
        string toProjectName,
        bool success,
        string? error)
    {
        return new OpportunityMoveHistory
        {
            CommercialTaskId = task.Id,
            AsanaTaskGid = task.AsanaTaskGid,
            TaskName = task.Name,
            FromProjectGid = fromProjectGid,
            FromProjectName = fromProjectName,
            ToProjectGid = toProjectGid,
            ToProjectName = toProjectName,
            MovedBy = string.IsNullOrWhiteSpace(movedBy) ? "local-user" : movedBy,
            MovedAtUtc = DateTime.UtcNow,
            Success = success,
            ErrorMessage = error
        };
    }

    private static string Csv(string value)
    {
        var safe = value.Replace("\"", "\"\"");
        return $"\"{safe}\"";
    }
}
