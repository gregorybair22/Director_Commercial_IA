using DirectorComercialIA.Data;
using DirectorComercialIA.Models;
using Microsoft.EntityFrameworkCore;

namespace DirectorComercialIA.Services;

public class CommercialTaskService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly CommercialTaskEnrichmentService _enrichmentService;

    public CommercialTaskService(IDbContextFactory<AppDbContext> dbFactory, CommercialTaskEnrichmentService enrichmentService)
    {
        _dbFactory = dbFactory;
        _enrichmentService = enrichmentService;
    }

    public async Task<List<CommercialTask>> GetByProjectAsync(string projectGid, bool includeArchived = false)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.CommercialTasks.Where(t => t.ProjectGid == projectGid);

        if (!includeArchived)
        {
            query = query.Where(t => !t.IsArchivedLocally);
        }

        return await query.OrderByDescending(t => t.ModifiedAt).ToListAsync();
    }

    public async Task<CommercialTask?> GetByIdAsync(int id)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        return await db.CommercialTasks.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<CommercialTask> CreateLocalAsync(CommercialTask model)
    {
        model.Source = "Local";
        model.AsanaTaskGid = null;
        model.LastSyncAt = DateTime.UtcNow;
        model.CreatedAt ??= DateTime.UtcNow;
        model.ModifiedAt = DateTime.UtcNow;
        model.IsLocalStatusManual = true;
        model.IsPriorityManual = true;
        model.IsCountryManual = true;
        model.IsLanguageManual = true;
        model.IsClientTypeManual = true;
        model.IsClientNameManual = true;
        model.IsContactNameManual = true;
        model.IsContactPhoneManual = true;
        model.IsContactMobileManual = true;
        model.IsContactRoleManual = true;
        model.IsContactEmailManual = true;
        model.IsNextActionManual = true;
        model.IsCommercialOwnerManual = true;
        model.IsProbabilityManual = true;
        model.IsEstimatedValueManual = true;

        using var db = await _dbFactory.CreateDbContextAsync();
        db.CommercialTasks.Add(model);
        await db.SaveChangesAsync();
        return model;
    }

    public async Task UpdateLocalFieldsAsync(CommercialTask model)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.CommercialTasks.FirstOrDefaultAsync(t => t.Id == model.Id);
        if (existing is null)
        {
            return;
        }

        existing.LocalStatus = model.LocalStatus;
        existing.Priority = model.Priority;
        existing.EstimatedValue = model.EstimatedValue;
        existing.Probability = model.Probability;
        existing.Country = model.Country;
        existing.Language = model.Language;
        existing.ClientType = model.ClientType;
        existing.ClientName = model.ClientName;
        existing.ContactName = model.ContactName;
        existing.ContactPhone = model.ContactPhone;
        existing.ContactMobile = model.ContactMobile;
        existing.ContactRole = model.ContactRole;
        existing.ContactEmail = model.ContactEmail;
        existing.ContactsJson = model.ContactsJson;
        existing.NextAction = model.NextAction;
        existing.NextActionDate = model.NextActionDate;
        existing.InternalNotes = model.InternalNotes;
        existing.CommercialOwner = model.CommercialOwner;
        existing.IsArchivedLocally = model.IsArchivedLocally;

        existing.IsLocalStatusManual = true;
        existing.IsPriorityManual = true;
        existing.IsCountryManual = true;
        existing.IsLanguageManual = true;
        existing.IsClientTypeManual = true;
        existing.IsClientNameManual = true;
        existing.IsContactNameManual = true;
        existing.IsContactPhoneManual = true;
        existing.IsContactMobileManual = true;
        existing.IsContactRoleManual = true;
        existing.IsContactEmailManual = true;
        existing.IsNextActionManual = true;
        existing.IsCommercialOwnerManual = true;
        existing.IsProbabilityManual = true;
        existing.IsEstimatedValueManual = true;

        await db.SaveChangesAsync();
    }

    public async Task RecalculateAutoFieldsAsync(int id)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.CommercialTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (existing is null)
        {
            return;
        }

        _enrichmentService.EnrichFromAsana(existing);
        await db.SaveChangesAsync();
    }

    public async Task ConvertAllToManualAsync(int id)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.CommercialTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (existing is null)
        {
            return;
        }

        existing.IsLocalStatusManual = true;
        existing.IsPriorityManual = true;
        existing.IsCountryManual = true;
        existing.IsLanguageManual = true;
        existing.IsClientTypeManual = true;
        existing.IsClientNameManual = true;
        existing.IsContactNameManual = true;
        existing.IsContactPhoneManual = true;
        existing.IsContactMobileManual = true;
        existing.IsContactRoleManual = true;
        existing.IsContactEmailManual = true;
        existing.IsNextActionManual = true;
        existing.IsCommercialOwnerManual = true;
        existing.IsProbabilityManual = true;
        existing.IsEstimatedValueManual = true;

        await db.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.CommercialTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (existing is null)
        {
            return;
        }

        existing.IsArchivedLocally = true;
        await db.SaveChangesAsync();
    }
}
