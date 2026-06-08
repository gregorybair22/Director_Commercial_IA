using DirectorComercialIA.Mail.Models;
using DirectorComercialIA.Models;
using Microsoft.EntityFrameworkCore;

namespace DirectorComercialIA.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CommercialTask> CommercialTasks => Set<CommercialTask>();
    public DbSet<OpportunityMoveHistory> OpportunityMoveHistories => Set<OpportunityMoveHistory>();

    public DbSet<MailContact> MailContacts => Set<MailContact>();
    public DbSet<MailContactComment> MailContactComments => Set<MailContactComment>();
    public DbSet<MailEmailTemplate> MailEmailTemplates => Set<MailEmailTemplate>();
    public DbSet<MailEmailSendLog> MailEmailSendLogs => Set<MailEmailSendLog>();
    public DbSet<MailEmailReplyLog> MailEmailReplyLogs => Set<MailEmailReplyLog>();
    public DbSet<MailContactPhaseHistory> MailContactPhaseHistories => Set<MailContactPhaseHistory>();
    public DbSet<MailImportBatch> MailImportBatches => Set<MailImportBatch>();
    public DbSet<MailGmailConnection> MailGmailConnections => Set<MailGmailConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommercialTask>()
            .Property(t => t.EstimatedValue)
            .HasPrecision(18, 2);

        modelBuilder.Entity<CommercialTask>()
            .HasIndex(t => t.AsanaTaskGid);

        modelBuilder.Entity<OpportunityMoveHistory>()
            .HasIndex(h => h.MovedAtUtc);

        modelBuilder.Entity<MailContact>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.CurrentPhase);
            e.HasIndex(x => x.ImportBatchId);
        });

        modelBuilder.Entity<MailEmailSendLog>(e =>
        {
            e.HasOne(x => x.Contact).WithMany(c => c.SendLogs).HasForeignKey(x => x.ContactId);
        });

        modelBuilder.Entity<MailEmailReplyLog>(e =>
        {
            e.HasOne(x => x.Contact).WithMany(c => c.ReplyLogs).HasForeignKey(x => x.ContactId);
        });

        modelBuilder.Entity<MailContactPhaseHistory>(e =>
        {
            e.HasOne(x => x.Contact).WithMany(c => c.PhaseHistory).HasForeignKey(x => x.ContactId);
        });

        modelBuilder.Entity<MailContactComment>(e =>
        {
            e.HasOne(x => x.Contact).WithMany(c => c.Comments).HasForeignKey(x => x.ContactId);
        });

        modelBuilder.Entity<MailImportBatch>(e =>
        {
            e.HasMany(x => x.Contacts).WithOne(c => c.ImportBatch).HasForeignKey(c => c.ImportBatchId);
        });
    }
}
