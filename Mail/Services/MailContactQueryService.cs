using DirectorComercialIA.Data;
using DirectorComercialIA.Mail.Abstractions;
using DirectorComercialIA.Mail.Models;
using Microsoft.EntityFrameworkCore;

namespace DirectorComercialIA.Mail.Services;

public class MailContactQueryService : IMailContactQueryService
{
    private readonly AppDbContext _db;

    public MailContactQueryService(AppDbContext db) => _db = db;

    public async Task<MailPagedResultDto<MailContactListItemDto>> ListAsync(MailContactListFilterDto filter, CancellationToken ct = default)
    {
        var q = _db.MailContacts.AsNoTracking().AsQueryable();
        q = q.Where(c => c.CurrentPhase == filter.Phase);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            q = q.Where(c =>
                c.Email.Contains(s) || c.Company.Contains(s) || c.FirstName.Contains(s) ||
                c.LastName.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(filter.Country))
            q = q.Where(c => c.Country == filter.Country);
        if (!string.IsNullOrWhiteSpace(filter.Language))
            q = q.Where(c => c.Language == filter.Language);
        if (!string.IsNullOrWhiteSpace(filter.CustomerType))
            q = q.Where(c => c.CustomerType == filter.CustomerType);
        if (!string.IsNullOrWhiteSpace(filter.AssignedUserId))
            q = q.Where(c => c.AssignedUserId == filter.AssignedUserId);
        if (filter.Replied == true)
            q = q.Where(c => c.IsResponded);
        if (filter.Replied == false)
            q = q.Where(c => !c.IsResponded);
        if (filter.Bounced == true)
            q = q.Where(c => c.IsBounced);
        if (filter.Bounced == false)
            q = q.Where(c => !c.IsBounced);
        if (filter.UnknownUserBounce == true)
            q = q.Where(c => c.BounceType == BounceType.UnknownUser || c.BounceType == BounceType.InvalidRecipient);

        var total = await q.CountAsync(ct);
        var skip = Math.Max(0, (filter.Page - 1) * filter.PageSize);

        var ids = await q.OrderByDescending(c => c.UpdatedAt)
            .Skip(skip)
            .Take(filter.PageSize)
            .Select(c => c.Id)
            .ToListAsync(ct);

        var contacts = await _db.MailContacts.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(ct);
        var order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        contacts.Sort((a, b) => order[a.Id].CompareTo(order[b.Id]));

        var sendLogs = await _db.MailEmailSendLogs.AsNoTracking()
            .Where(l => ids.Contains(l.ContactId) && l.PhaseNumber == filter.Phase && l.SendStatus == EmailSendStatus.Sent)
            .Select(l => l.ContactId)
            .Distinct()
            .ToListAsync(ct);
        var sentSet = sendLogs.ToHashSet();

        var commentRows = await _db.MailContactComments.AsNoTracking()
            .Where(c => ids.Contains(c.ContactId))
            .GroupBy(c => c.ContactId)
            .Select(g => new { ContactId = g.Key, Last = g.OrderByDescending(x => x.CreatedAt).First() })
            .ToListAsync(ct);
        var commentByContact = commentRows.ToDictionary(x => x.ContactId, x => x.Last.Comment);

        var list = contacts.Select(c => new MailContactListItemDto
        {
            Id = c.Id,
            FullName = ($"{c.FirstName} {c.LastName}").Trim(),
            Company = c.Company,
            Email = c.Email,
            Country = c.Country,
            Language = c.Language,
            CustomerType = c.CustomerType,
            LatestComment = commentByContact.GetValueOrDefault(c.Id),
            CreatedAt = c.CreatedAt,
            CurrentPhase = c.CurrentPhase,
            IsResponded = c.IsResponded,
            IsBounced = c.IsBounced,
            BounceType = c.BounceType,
            BounceReason = c.BounceReason,
            PhaseEmailSent = sentSet.Contains(c.Id)
        }).ToList();

        return new MailPagedResultDto<MailContactListItemDto>
        {
            Items = list,
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<MailPhaseMetricsDto> GetPhaseMetricsAsync(int phase, CancellationToken ct = default)
    {
        var q = _db.MailContacts.AsNoTracking().Where(c => c.CurrentPhase == phase);
        return new MailPhaseMetricsDto
        {
            Phase = phase,
            TotalInPhase = await q.CountAsync(ct),
            Replied = await q.CountAsync(c => c.IsResponded, ct),
            NotReplied = await q.CountAsync(c => !c.IsResponded, ct),
            Bounced = await q.CountAsync(c => c.IsBounced, ct)
        };
    }
}
