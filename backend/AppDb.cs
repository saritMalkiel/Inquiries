using Microsoft.EntityFrameworkCore;

namespace Requests;

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> o) : base(o) { }

    public DbSet<Request> Requests => Set<Request>();
    public DbSet<StatusChange> StatusChanges => Set<StatusChange>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var r = b.Entity<Request>();
        r.Property(x => x.Title).HasMaxLength(200);
        r.Property(x => x.OrganizationName).HasMaxLength(200);
        r.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        r.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
        r.Property(x => x.RowVersion).IsRowVersion();
        r.HasIndex(x => new { x.Status, x.Priority, x.CreatedAt });
        r.HasIndex(x => x.OrganizationName);

        var h = b.Entity<StatusChange>();
        h.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(20);
        h.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(20);
        h.HasIndex(x => x.RequestId);
    }
}
