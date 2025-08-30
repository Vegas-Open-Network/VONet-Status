using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace VONet_Stats.Data;

public class StatusDbContext : DbContext
{
    public StatusDbContext(DbContextOptions<StatusDbContext> options) : base(options) { }

    public DbSet<ServiceStatus> ServiceStatuses { get; set; }
    public DbSet<StatusHistory> StatusHistory { get; set; }
    public DbSet<Incident> Incidents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ServiceId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.ServiceId).IsUnique();
        });

        modelBuilder.Entity<StatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ServiceId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => new { e.ServiceId, e.Timestamp });
        });

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.AffectedServices).IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}

public class ServiceStatus
{
    public int Id { get; set; }
    public string ServiceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ServiceStatusType Status { get; set; }
    public double ResponseTimeMs { get; set; }
    public DateTime LastChecked { get; set; }
    public DateTime LastStatusChange { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class StatusHistory
{
    public int Id { get; set; }
    public string ServiceId { get; set; } = string.Empty;
    public ServiceStatusType Status { get; set; }
    public double ResponseTimeMs { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ErrorMessage { get; set; }
}

public class Incident
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public IncidentStatus Status { get; set; }
    public string AffectedServices { get; set; } = string.Empty; // JSON array of service IDs
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum ServiceStatusType
{
    Operational = 0,
    PartialOutage = 1,
    MajorOutage = 2,
    UnderMaintenance = 3
}

public enum IncidentStatus
{
    Investigating = 0,
    Identified = 1,
    Monitoring = 2,
    Resolved = 3
}