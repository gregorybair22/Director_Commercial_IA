using DirectorComercialIA.Models;
using DirectorComercialIA.ViewModels;

namespace DirectorComercialIA.Repositories;

public interface IOpportunityRepository
{
    Task<List<CommercialTask>> SearchAsync(OpportunityMoveFilter filter);
    Task<List<CommercialTask>> GetByIdsAsync(IEnumerable<int> ids);
    Task<List<string>> GetCountriesByProjectAsync(string? projectGid);
    Task SaveChangesAsync();
}
