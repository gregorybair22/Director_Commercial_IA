using DirectorComercialIA.Data;
using DirectorComercialIA.Mail.Models;
using Microsoft.EntityFrameworkCore;

namespace DirectorComercialIA.Mail;

public static class MailSeedData
{
    public static async Task EnsureDefaultTemplatesAsync(AppDbContext db)
    {
        if (await db.MailEmailTemplates.AnyAsync()) return;

        for (var p = 1; p <= 5; p++)
        {
            db.MailEmailTemplates.Add(new MailEmailTemplate
            {
                Name = $"Default phase {p}",
                PhaseNumber = p,
                Language = "en",
                IsDefault = true,
                SubjectTemplate = p == 1 ? "Hello {{FirstName}}" : $"Follow-up {p} — {{Company}}",
                BodyTemplate =
                    "<p>Hi {{FirstName}},</p><p>This is an automated message regarding {{Company}}.</p>",
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
