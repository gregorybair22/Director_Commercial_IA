using DirectorComercialIA.Models;
using Microsoft.EntityFrameworkCore;

namespace DirectorComercialIA.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<CommercialTask> CommercialTasks => Set<CommercialTask>();
    public DbSet<OpportunityMoveHistory> OpportunityMoveHistories => Set<OpportunityMoveHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommercialTask>()
            .Property(t => t.EstimatedValue)
            .HasPrecision(18, 2);

        modelBuilder.Entity<CommercialTask>()
            .HasIndex(t => t.AsanaTaskGid);

        modelBuilder.Entity<OpportunityMoveHistory>()
            .HasIndex(h => h.MovedAtUtc);
    }
}
