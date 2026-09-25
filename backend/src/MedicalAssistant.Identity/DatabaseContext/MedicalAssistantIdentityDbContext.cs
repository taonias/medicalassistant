using MedicalAssistant.Identity.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Identity.DatabaseContext;

public class MedicalAssistantIdentityDbContext : IdentityDbContext<ApplicationUser>
{
    public MedicalAssistantIdentityDbContext(DbContextOptions<MedicalAssistantIdentityDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>()
            .Property(u => u.IsApproved)
            .HasDefaultValue(true);
    }
}
