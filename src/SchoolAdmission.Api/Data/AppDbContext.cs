using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<SeatHold> Holds => Set<SeatHold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SchoolClass>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasMaxLength(64);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(255);
            entity.Property(c => c.TotalSeats).IsRequired();

            entity.HasMany(c => c.Holds)
                  .WithOne(h => h.SchoolClass)
                  .HasForeignKey(h => h.ClassId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SeatHold>(entity =>
        {
            entity.HasKey(h => h.HoldId);
            entity.Property(h => h.HoldId).HasMaxLength(64);
            entity.Property(h => h.ClassId).IsRequired().HasMaxLength(64);
            entity.Property(h => h.StudentId).IsRequired().HasMaxLength(32);
            entity.Property(h => h.Status).IsRequired().HasMaxLength(32);

            entity.HasIndex(h => h.ClassId);
            entity.HasIndex(h => h.StudentId);
            entity.HasIndex(h => new { h.ClassId, h.Status });
            entity.HasIndex(h => new { h.ClassId, h.StudentId, h.Status });
            entity.HasIndex(h => h.ExpiresAt);
        });
    }
}
