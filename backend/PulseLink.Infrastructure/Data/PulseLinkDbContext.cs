using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PulseLink.Core.Entities;
using PulseLink.Infrastructure.Identity;

namespace PulseLink.Infrastructure.Data;

public class PulseLinkDbContext : IdentityDbContext<AppUser>
{
    public PulseLinkDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<VitalSign> VitalSigns => Set<VitalSign>();
    public DbSet<Intervention> Interventions => Set<Intervention>();
    public DbSet<InterventionOperation> InterventionOperations => Set<InterventionOperation>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Agency>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Region).HasMaxLength(100);
        });

        builder.Entity<Hospital>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.City).HasMaxLength(100);
        });

        builder.Entity<Incident>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.IncidentNumber).IsUnique();
            e.Property(x => x.IncidentNumber).HasMaxLength(40).IsRequired();
            e.Property(x => x.ChiefComplaint).HasMaxLength(500).IsRequired();
            e.Property(x => x.PatientAgeRange).HasMaxLength(40);
            e.Property(x => x.PatientSex).HasMaxLength(20);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
            e.Property(x => x.UpdatedAtUtc).IsRequired();
            e.HasIndex(x => x.DestinationHospitalId);
            e.HasIndex(x => new { x.DestinationHospitalId, x.UpdatedAtUtc, x.Id })
                .HasDatabaseName("IX_Incidents_HospitalList")
                .IsDescending(false, true, true)
                .HasFilter("Status IN (3, 4, 5)");
            e.HasIndex(x => new { x.AgencyId, x.UpdatedAtUtc, x.Id })
                .HasDatabaseName("IX_Incidents_Agency_UpdatedAtUtc")
                .IsDescending(false, true, true);
            e.HasIndex(x => new { x.CreatedByUserId, x.UpdatedAtUtc, x.Id })
                .HasDatabaseName("IX_Incidents_CreatedBy_UpdatedAtUtc")
                .IsDescending(false, true, true);
            e.HasOne(x => x.Agency).WithMany(a => a.Incidents).HasForeignKey(x => x.AgencyId);
            e.HasOne(x => x.DestinationHospital).WithMany(h => h.Incidents)
                .HasForeignKey(x => x.DestinationHospitalId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<VitalSign>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.GlasgowComaScale).HasMaxLength(20);
            e.Property(x => x.SpO2).HasPrecision(5, 2);
            e.Property(x => x.TemperatureC).HasPrecision(4, 1);
            e.HasOne(x => x.Incident).WithMany(i => i.VitalSigns).HasForeignKey(x => x.IncidentId);
        });

        builder.Entity<Intervention>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Medication).HasMaxLength(200);
            e.Property(x => x.Dose).HasMaxLength(100);
            e.Property(x => x.Route).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasOne(x => x.Incident).WithMany(i => i.Interventions).HasForeignKey(x => x.IncidentId);
        });

        builder.Entity<InterventionOperation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
            e.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.ActorUserId, x.IncidentId, x.Key }).IsUnique();
            e.HasOne<Incident>().WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Intervention>().WithMany().HasForeignKey(x => x.InterventionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.Details).HasMaxLength(2000).IsRequired();
            e.HasOne(x => x.Incident).WithMany(i => i.AuditEvents).HasForeignKey(x => x.IncidentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AppUser>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareIncidentWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareIncidentWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareIncidentWrites()
    {
        foreach (var entry in ChangeTracker.Entries<Incident>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = entry.Entity.UpdatedAt.UtcDateTime;
                // Child append endpoints also update their parent incident. Export
                // auditing alone does not track a modified incident or change its version.
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.Version = Guid.NewGuid();
                }
            }
        }
    }
}
