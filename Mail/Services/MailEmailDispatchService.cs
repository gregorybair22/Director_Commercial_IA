using System.Text.RegularExpressions;
using DirectorComercialIA.Data;
using DirectorComercialIA.Mail.Abstractions;
using DirectorComercialIA.Mail.Models;
using Google.Apis.Gmail.v1.Data;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace DirectorComercialIA.Mail.Services;

public class MailEmailDispatchService : IMailEmailDispatchService
{
    private readonly AppDbContext _db;
    private readonly MailGmailOAuthService _gmail;

    public MailEmailDispatchService(AppDbContext db, MailGmailOAuthService gmail)
    {
        _db = db;
        _gmail = gmail;
    }

    public string MergeTemplate(string template, MailContactMergeData d)
    {
        if (string.IsNullOrEmpty(template)) return template;
        string R(string t, string key, string value) =>
            Regex.Replace(t, @"\{\{\s*" + Regex.Escape(key) + @"\s*\}\}", value, RegexOptions.IgnoreCase);

        var s = template;
        s = R(s, "FirstName", d.FirstName);
        s = R(s, "LastName", d.LastName);
        s = R(s, "Company", d.Company);
        s = R(s, "Country", d.Country);
        s = R(s, "Language", d.Language);
        s = R(s, "CustomerType", d.CustomerType);
        return s;
    }

    public async Task<MailSendEmailForPhaseResultDto> SendForPhaseAsync(
        int phaseNumber,
        IReadOnlyList<int> contactIds,
        string subject,
        string htmlBody,
        string sentByUserId,
        CancellationToken ct = default)
    {
        var result = new MailSendEmailForPhaseResultDto();
        var svc = await _gmail.GetGmailServiceAsync(ct);
        if (svc == null)
        {
            result.Errors.Add("Gmail is not connected.");
            result.Failed = contactIds.Count;
            return result;
        }

        var status = await _gmail.GetStatusAsync(ct);
        var fromEmail = status.Email ?? throw new InvalidOperationException("Missing sender email.");

        var conn = await _db.MailGmailConnections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct);
        var displayName = conn?.DisplayName;

        foreach (var id in contactIds)
        {
            var c = await _db.MailContacts.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c == null)
            {
                result.Failed++;
                result.Errors.Add($"Contact {id} not found.");
                continue;
            }

            if (c.CurrentPhase != phaseNumber)
            {
                result.Failed++;
                result.Errors.Add($"{c.Email}: wrong phase (contact phase {c.CurrentPhase}, expected {phaseNumber}).");
                continue;
            }

            if (c.IsDoNotContact || c.IsExcluded)
            {
                result.Failed++;
                result.Errors.Add($"{c.Email}: excluded or do-not-contact.");
                continue;
            }

            if ((c.IsResponded || c.IsHardBounce) && !c.AdminOverrideAllowNextSend)
            {
                result.Failed++;
                result.Errors.Add($"{c.Email}: replied or hard bounce (admin override required).");
                continue;
            }

            var merge = new MailContactMergeData
            {
                FirstName = c.FirstName,
                LastName = c.LastName,
                Company = c.Company,
                Country = c.Country,
                Language = c.Language,
                CustomerType = c.CustomerType
            };
            var subj = MergeTemplate(subject, merge);
            var body = MergeTemplate(htmlBody, merge);

            string? messageId = null;
            string? threadId = null;
            try
            {
                var mime = new MimeMessage();
                mime.From.Add(new MailboxAddress(displayName ?? "Commercial Mail", fromEmail));
                mime.To.Add(MailboxAddress.Parse(c.Email));
                mime.Subject = subj;
                mime.Body = new TextPart("html") { Text = body };

                await using var ms = new MemoryStream();
                await mime.WriteToAsync(ms, ct);
                var raw = UrlSafeBase64(ms.ToArray());

                var sent = await svc.Users.Messages.Send(new Message { Raw = raw }, "me").ExecuteAsync(ct);
                messageId = sent.Id;
                threadId = sent.ThreadId;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"{c.Email}: {ex.Message}");
                _db.MailEmailSendLogs.Add(new MailEmailSendLog
                {
                    ContactId = c.Id,
                    PhaseNumber = phaseNumber,
                    Subject = subj,
                    BodySnapshot = body,
                    SentByUserId = sentByUserId,
                    SentAt = DateTime.UtcNow,
                    SendStatus = EmailSendStatus.Failed,
                    ErrorMessage = ex.Message
                });
                await _db.SaveChangesAsync(ct);
                continue;
            }

            _db.MailEmailSendLogs.Add(new MailEmailSendLog
            {
                ContactId = c.Id,
                PhaseNumber = phaseNumber,
                GmailMessageId = messageId,
                GmailThreadId = threadId,
                Subject = subj,
                BodySnapshot = body,
                SentByUserId = sentByUserId,
                SentAt = DateTime.UtcNow,
                SendStatus = EmailSendStatus.Sent
            });

            ApplySendSideEffects(c, phaseNumber, sentByUserId);
            result.Sent++;
            await _db.SaveChangesAsync(ct);
        }

        return result;
    }

    private void ApplySendSideEffects(MailContact c, int phaseNumber, string userId)
    {
        var now = DateTime.UtcNow;
        switch (phaseNumber)
        {
            case 1: c.Send1At = now; break;
            case 2: c.Send2At = now; break;
            case 3: c.Send3At = now; break;
            case 4: c.Send4At = now; break;
            case 5: c.Send5At = now; break;
        }

        var history = new MailContactPhaseHistory
        {
            ContactId = c.Id,
            FromPhase = c.CurrentPhase,
            ToPhase = phaseNumber < 5 ? phaseNumber + 1 : 5,
            Reason = $"Sent phase {phaseNumber} email",
            UserId = userId,
            CreatedAt = now
        };

        if (phaseNumber >= 5)
        {
            c.CurrentStatus = ContactWorkflowStatus.SequenceCompletedNoReply;
            c.CurrentPhase = 5;
        }
        else
        {
            c.CurrentPhase = phaseNumber + 1;
        }

        c.UpdatedAt = now;
        _db.MailContactPhaseHistories.Add(history);
    }

    private static string UrlSafeBase64(byte[] data) =>
        Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
