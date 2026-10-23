using Microsoft.EntityFrameworkCore;

namespace Requests;

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> options) : base(options) { }

    public DbSet<ServiceRequest> Requests => Set<ServiceRequest>();
    public DbSet<StatusChange> StatusChanges => Set<StatusChange>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ServiceRequest>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.OrganizationName).HasMaxLength(200).IsRequired();
            e.Property(x => x.AssignedTo).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Priority);
            e.HasIndex(x => x.CreatedAt);
        });

        b.Entity<StatusChange>(e =>
        {
            e.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ChangedBy).HasMaxLength(100);
            e.HasOne(x => x.Request)
             .WithMany(r => r.StatusChanges)
             .HasForeignKey(x => x.RequestId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RequestId, x.ChangedAt });
        });
    }
}