using DirectorComercialIA.Data;
using DirectorComercialIA.Data;
using DirectorComercialIA.Models;
using Microsoft.EntityFrameworkCore;

namespace DirectorComercialIA.Repositories;

public class MoveHistoryRepository : IMoveHistoryRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public MoveHistoryRepository(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task AddRangeAsync(IEnumerable<OpportunityMoveHistory> entries)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        await db.OpportunityMoveHistories.AddRangeAsync(entries);
        await db.SaveChangesAsync();
    }

    public async Task<List<OpportunityMoveHistory>> GetAsync(DateTime? fromUtc, DateTime? toUtc, string? movedBy, string? fromProjectGid, string? toProjectGid, bool? success, string? searchText)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.OpportunityMoveHistories.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
            query = query.Where(h => h.MovedAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(h => h.MovedAtUtc <= toUtc.Value);

        if (!string.IsNullOrWhiteSpace(movedBy))
            query = query.Where(h => h.MovedBy.Contains(movedBy));

        if (!string.IsNullOrWhiteSpace(fromProjectGid))
            query = query.Where(h => h.FromProjectGid == fromProjectGid);

        if (!string.IsNullOrWhiteSpace(toProjectGid))
            query = query.Where(h => h.ToProjectGid == toProjectGid);

        if (success.HasValue)
            query = query.Where(h => h.Success == success.Value);

        if (!string.IsNullOrWhiteSpace(searchText))
            query = query.Where(h => h.TaskName.Contains(searchText) || (h.ErrorMessage ?? "").Contains(searchText));

        return await query
            .OrderByDescending(h => h.MovedAtUtc)
            .Take(2000)
            .ToListAsync();
    }
}
