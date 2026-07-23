using MedicalAssistant.Identity.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Identity.DatabaseContext;

public class MedicalAssistantIdentityDbContext : IdentityDbContext<ApplicationUser>
{
    public MedicalAssistantIdentityDbContext(DbContextOptions<MedicalAssistantIdentityDbContext> options) : base(options)
    {
    }
}
