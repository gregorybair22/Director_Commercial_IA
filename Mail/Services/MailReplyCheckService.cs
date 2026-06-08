using DirectorComercialIA.Data;
using DirectorComercialIA.Mail.Abstractions;
using DirectorComercialIA.Mail.Helpers;
using DirectorComercialIA.Mail.Models;
using Google.Apis.Gmail.v1;
using Microsoft.EntityFrameworkCore;

namespace DirectorComercialIA.Mail.Services;

public class MailReplyCheckService : IMailReplyCheckService
{
    private readonly AppDbContext _db;
    private readonly MailGmailOAuthService _gmail;

    public MailReplyCheckService(AppDbContext db, MailGmailOAuthService gmail)
    {
        _db = db;
        _gmail = gmail;
    }

    public async Task<MailReplyCheckSummaryDto> CheckRepliesForPhaseAsync(int phase, CancellationToken ct = default)
    {
        var summary = new MailReplyCheckSummaryDto();
        var svc = await _gmail.GetGmailServiceAsync(ct);
        if (svc == null) return summary;

        var contacts = await _db.MailContacts
            .Where(c => c.CurrentPhase == phase && !c.IsResponded && !c.IsBounced)
            .ToListAsync(ct);

        var prevPhase = phase - 1;
        if (prevPhase < 1) return summary;

        foreach (var c in contacts)
        {
            summary.Checked++;
            try
            {
                var log = await _db.MailEmailSendLogs.AsNoTracking()
                    .Where(l => l.ContactId == c.Id && l.PhaseNumber == prevPhase &&
                                l.SendStatus == EmailSendStatus.Sent)
                    .OrderByDescending(l => l.SentAt)
                    .FirstOrDefaultAsync(ct);

                if (log == null)
                {
                    summary.NoReply++;
                    continue;
                }

                var threadId = log.GmailThreadId;
                if (string.IsNullOrEmpty(threadId) && !string.IsNullOrEmpty(log.GmailMessageId))
                {
                    var m = await svc.Users.Messages.Get("me", log.GmailMessageId).ExecuteAsync(ct);
                    threadId = m.ThreadId;
                }

                if (string.IsNullOrEmpty(threadId))
                {
                    summary.NoReply++;
                    continue;
                }

                var threadReq = svc.Users.Threads.Get("me", threadId);
                threadReq.Format = UsersResource.ThreadsResource.GetRequest.FormatEnum.Full;
                var thread = await threadReq.ExecuteAsync(ct);

                var contactEmail = c.Email.Trim().ToLowerInvariant();
                var replied = false;
                DateTime? replyDate = null;
                var replySubject = "";
                var replySnippet = "";

                if (thread.Messages != null)
                {
                    foreach (var msg in thread.Messages)
                    {
                        var from = GmailMessageHelper.GetHeader(msg, "From");
                        var addr = GmailMessageHelper.ParseEmailAddress(from);
                        if (addr != null && addr == contactEmail)
                        {
                            var internalDate = msg.InternalDate;
                            if (internalDate.HasValue)
                            {
                                var dt = DateTimeOffset.FromUnixTimeMilliseconds(internalDate.Value).UtcDateTime;
                                if (dt > log.SentAt)
                                {
                                    replied = true;
                                    replyDate = dt;
                                    replySubject = GmailMessageHelper.GetHeader(msg, "Subject") ?? "";
                                    replySnippet = msg.Snippet ?? "";
                                    break;
                                }
                            }
                        }
                    }
                }

                if (replied)
                {
                    summary.RepliesFound++;
                    c.IsResponded = true;
                    c.LastReplyReceivedAt = replyDate ?? DateTime.UtcNow;
                    c.LastReplyCheckAt = DateTime.UtcNow;
                    c.UpdatedAt = DateTime.UtcNow;

                    _db.MailEmailReplyLogs.Add(new MailEmailReplyLog
                    {
                        ContactId = c.Id,
                        GmailThreadId = threadId,
                        FromEmail = contactEmail,
                        Subject = replySubject,
                        BodySnippet = replySnippet,
                        ReceivedAt = replyDate ?? DateTime.UtcNow,
                        DetectedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    summary.NoReply++;
                    c.LastReplyCheckAt = DateTime.UtcNow;
                    c.UpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync(ct);
            }
            catch
            {
                summary.Errors++;
            }
        }

        return summary;
    }
}
