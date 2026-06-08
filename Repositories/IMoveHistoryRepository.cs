using DirectorComercialIA.Models;

using DirectorComercialIA.Models;

namespace DirectorComercialIA.Repositories;

public interface IMoveHistoryRepository
{
    Task AddRangeAsync(IEnumerable<OpportunityMoveHistory> entries);
    Task<List<OpportunityMoveHistory>> GetAsync(DateTime? fromUtc, DateTime? toUtc, string? movedBy, string? fromProjectGid, string? toProjectGid, bool? success, string? searchText);
}
