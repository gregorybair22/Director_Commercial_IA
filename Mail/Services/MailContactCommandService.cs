using DirectorComercialIA.Data;
using DirectorComercialIA.Mail.Abstractions;
using DirectorComercialIA.Mail.Models;
using Microsoft.EntityFrameworkCore;

namespace DirectorComercialIA.Mail.Services;

public class MailContactCommandService : IMailContactCommandService
{
    private readonly AppDbContext _db;

    public MailContactCommandService(AppDbContext db) => _db = db;

    public async Task AddCommentAsync(int contactId, string userId, string comment, CancellationToken ct = default)
    {
        _db.MailContactComments.Add(new MailContactComment
        {
            ContactId = contactId,
            UserId = userId,
            Comment = comment,
            CreatedAt = DateTime.UtcNow
        });
        await TouchContact(contactId, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AssignAsync(int contactId, string? assignUserId, CancellationToken ct = default)
    {
        var c = await _db.MailContacts.FirstOrDefaultAsync(x => x.Id == contactId, ct);
        if (c == null) return;
        c.AssignedUserId = assignUserId;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MovePhaseAsync(int contactId, int toPhase, string userId, string reason, CancellationToken ct = default)
    {
        var c = await _db.MailContacts.FirstOrDefaultAsync(x => x.Id == contactId, ct);
        if (c == null) return;
        var from = c.CurrentPhase;
        c.CurrentPhase = toPhase;
        c.UpdatedAt = DateTime.UtcNow;
        _db.MailContactPhaseHistories.Add(new MailContactPhaseHistory
        {
            ContactId = contactId,
            FromPhase = from,
            ToPhase = toPhase,
            Reason = reason,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetRespondedAsync(int contactId, bool responded, CancellationToken ct = default)
    {
        var c = await _db.MailContacts.FirstOrDefaultAsync(x => x.Id == contactId, ct);
        if (c == null) return;
        c.IsResponded = responded;
        c.UpdatedAt = DateTime.UtcNow;
        if (responded)
            c.LastReplyReceivedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetDoNotContactAsync(int contactId, bool value, CancellationToken ct = default)
    {
        var c = await _db.MailContacts.FirstOrDefaultAsync(x => x.Id == contactId, ct);
        if (c == null) return;
        c.IsDoNotContact = value;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetAdminOverrideAsync(int contactId, bool allow, CancellationToken ct = default)
    {
        var c = await _db.MailContacts.FirstOrDefaultAsync(x => x.Id == contactId, ct);
        if (c == null) return;
        c.AdminOverrideAllowNextSend = allow;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task TouchContact(int contactId, CancellationToken ct)
    {
        var c = await _db.MailContacts.FirstOrDefaultAsync(x => x.Id == contactId, ct);
        if (c != null)
            c.UpdatedAt = DateTime.UtcNow;
    }
}
