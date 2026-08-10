using System.Linq;
using MedicalAssistant.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Identity;

public static class IdentityDbInitializer
{
    private const string DoctorRole = "Doctor";
    private const string AdministratorRole = "Administrator";
    private const string DefaultDevelopmentDoctorUserName = "admin";
    private const string DefaultDevelopmentDoctorPassword = "admin";
    private const string DefaultDevelopmentDoctorEmail = "admin@medicalassistant.local";

    public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("MedicalAssistant.Identity.IdentityDbInitializer");

        string[] roles = [DoctorRole, AdministratorRole];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (result.Succeeded)
                    logger.LogInformation("Created role {Role}", role);
            }
        }
    }

    public static async Task SeedDevelopmentDoctorAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var hostEnvironment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!hostEnvironment.IsDevelopment())
        {
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("MedicalAssistant.Identity.IdentityDbInitializer");

        var user = await userManager.FindByNameAsync(DefaultDevelopmentDoctorUserName);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = DefaultDevelopmentDoctorUserName,
                Email = DefaultDevelopmentDoctorEmail,
                EmailConfirmed = true,
                FirstName = "Admin",
                LastName = "Doctor"
            };

            var createResult = await userManager.CreateAsync(user, DefaultDevelopmentDoctorPassword);
            if (!createResult.Succeeded)
            {
                throw CreateSeedFailure("create default development doctor user", createResult.Errors);
            }

            logger.LogInformation("Created default development doctor user {UserName}", DefaultDevelopmentDoctorUserName);
        }
        else if (!await userManager.CheckPasswordAsync(user, DefaultDevelopmentDoctorPassword))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await userManager.ResetPasswordAsync(user, resetToken, DefaultDevelopmentDoctorPassword);
            if (!resetResult.Succeeded)
            {
                throw CreateSeedFailure("reset default development doctor password", resetResult.Errors);
            }

            logger.LogInformation("Reset password for default development doctor user {UserName}", DefaultDevelopmentDoctorUserName);
        }

        if (!await userManager.IsInRoleAsync(user, DoctorRole))
        {
            var addRoleResult = await userManager.AddToRoleAsync(user, DoctorRole);
            if (!addRoleResult.Succeeded)
            {
                throw CreateSeedFailure("assign Doctor role to default development doctor user", addRoleResult.Errors);
            }

            logger.LogInformation(
                "Assigned role {Role} to default development doctor user {UserName}",
                DoctorRole,
                DefaultDevelopmentDoctorUserName);
        }
    }

    private static InvalidOperationException CreateSeedFailure(string operation, IEnumerable<IdentityError> errors)
    {
        var message = string.Join(' ', errors.Select(static error => error.Description));
        return new InvalidOperationException($"Failed to {operation}. {message}".Trim());
    }
}
