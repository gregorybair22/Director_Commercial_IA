using DirectorComercialIA.Data;
using DirectorComercialIA.Models;
using DirectorComercialIA.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DirectorComercialIA.Repositories;

public class OpportunityRepository : IOpportunityRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private AppDbContext? _db;

    public OpportunityRepository(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<CommercialTask>> SearchAsync(OpportunityMoveFilter filter)
    {
        var db = await GetDbAsync();
        var query = db.CommercialTasks.AsQueryable().Where(t => !t.IsArchivedLocally);

        if (!string.IsNullOrWhiteSpace(filter.ProjectGid))
            query = query.Where(t => t.ProjectGid == filter.ProjectGid);

        if (!string.IsNullOrWhiteSpace(filter.Country))
            query = query.Where(t => (t.Country ?? "").Contains(filter.Country));

        if (!string.IsNullOrWhiteSpace(filter.Language))
            query = query.Where(t => (t.Language ?? "").Contains(filter.Language));

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(t => t.LocalStatus == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.Commercial))
            query = query.Where(t => (t.CommercialOwner ?? t.AssigneeName ?? "").Contains(filter.Commercial));

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
            query = query.Where(t => t.Name.Contains(filter.SearchText) || (t.Description ?? "").Contains(filter.SearchText));

        if (filter.DateFrom.HasValue)
            query = query.Where(t => t.ModifiedAt.Date >= filter.DateFrom.Value.Date);

        if (filter.DateTo.HasValue)
            query = query.Where(t => t.ModifiedAt.Date <= filter.DateTo.Value.Date);

        return await query
            .OrderByDescending(t => t.ModifiedAt)
            .ToListAsync();
    }

    public async Task<List<CommercialTask>> GetByIdsAsync(IEnumerable<int> ids)
    {
        var db = await GetDbAsync();
        var idsList = ids.Distinct().ToList();
        return await db.CommercialTasks.Where(t => idsList.Contains(t.Id)).ToListAsync();
    }

    public async Task<List<string>> GetCountriesByProjectAsync(string? projectGid)
    {
        var db = await GetDbAsync();
        var query = db.CommercialTasks.AsQueryable().Where(t => !t.IsArchivedLocally);

        if (!string.IsNullOrWhiteSpace(projectGid))
            query = query.Where(t => t.ProjectGid == projectGid);

        return await query
            .Select(t => t.Country)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        if (_db is not null)
            await _db.SaveChangesAsync();
    }

    private async Task<AppDbContext> GetDbAsync()
    {
        _db ??= await _dbFactory.CreateDbContextAsync();
        return _db;
    }
}
