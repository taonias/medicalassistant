using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.DatabaseContext;

public class MedicalAssistantDatabaseContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MedicalAssistantDatabaseContext(
        DbContextOptions<MedicalAssistantDatabaseContext> options,
        IHttpContextAccessor httpContextAccessor) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public virtual DbSet<Patient> Patients { get; set; }
    public virtual DbSet<Consultation> Consultations { get; set; }
    public virtual DbSet<Transcript> Transcripts { get; set; }
    public virtual DbSet<MedicalStructuredData> MedicalStructuredData { get; set; }
    public virtual DbSet<DoctorNote> DoctorNotes { get; set; }
    public virtual DbSet<ActionRequest> ActionRequests { get; set; }
    public virtual DbSet<AuditLog> AuditLogs { get; set; }
    public virtual DbSet<ErrorLog> ErrorLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MedicalAssistantDatabaseContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var userId = user?.FindFirst("uid")?.Value ?? "Unknown";

        foreach (var entry in ChangeTracker.Entries<BaseEntity>()
                     .Where(q => q.State == EntityState.Added || q.State == EntityState.Modified))
        {
            var now = DateTime.UtcNow;
            entry.Entity.DateModified = now;
            entry.Entity.ModifiedBy = userId;
            if (entry.State == EntityState.Added)
            {
                entry.Entity.DateCreated = now;
                entry.Entity.CreatedBy = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(BaseEntity.DateCreated)).IsModified = false;
                entry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
            }
        }

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
