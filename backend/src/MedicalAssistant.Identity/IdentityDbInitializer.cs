using System.Linq;
using MedicalAssistant.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
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

    /// <summary>
    /// Seeds a doctor account from the "SeedDoctor" configuration section. Unlike
    /// <see cref="SeedDevelopmentDoctorAsync"/>, this runs in every environment (including
    /// Production) so the first account can be provisioned without relying on an open
    /// registration endpoint. Credentials come from config/environment variables — nothing
    /// is hardcoded.
    ///
    /// Idempotent and safe to run on every startup:
    ///   - No SeedDoctor:UserName / SeedDoctor:Password configured -> no-op.
    ///   - User missing -> created with the Doctor role.
    ///   - User already exists -> the Doctor role is ensured, but the password is left
    ///     untouched (so an in-app password change is never clobbered on the next deploy).
    ///     Set SeedDoctor:ResetPassword=true to force the configured password back on.
    /// </summary>
    public static async Task SeedConfiguredDoctorAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var section = configuration.GetSection("SeedDoctor");
        var userName = section["UserName"];
        var password = section["Password"];

        using var scope = serviceProvider.CreateScope();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("MedicalAssistant.Identity.IdentityDbInitializer");

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation(
                "SeedDoctor not configured (UserName/Password missing); skipping configured doctor seeding.");
            return;
        }

        var email = string.IsNullOrWhiteSpace(section["Email"]) ? $"{userName}@medicalassistant.local" : section["Email"]!;
        var firstName = string.IsNullOrWhiteSpace(section["FirstName"]) ? "Doctor" : section["FirstName"]!;
        var lastName = string.IsNullOrWhiteSpace(section["LastName"]) ? userName : section["LastName"]!;
        var resetPassword = section.GetValue("ResetPassword", false);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw CreateSeedFailure($"create configured doctor user '{userName}'", createResult.Errors);
            }

            logger.LogInformation("Created configured doctor user {UserName}", userName);
        }
        else if (resetPassword && !await userManager.CheckPasswordAsync(user, password))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await userManager.ResetPasswordAsync(user, resetToken, password);
            if (!resetResult.Succeeded)
            {
                throw CreateSeedFailure($"reset configured doctor password for '{userName}'", resetResult.Errors);
            }

            logger.LogInformation("Reset password for configured doctor user {UserName}", userName);
        }

        if (!await userManager.IsInRoleAsync(user, DoctorRole))
        {
            var addRoleResult = await userManager.AddToRoleAsync(user, DoctorRole);
            if (!addRoleResult.Succeeded)
            {
                throw CreateSeedFailure($"assign Doctor role to configured doctor user '{userName}'", addRoleResult.Errors);
            }

            logger.LogInformation("Assigned role {Role} to configured doctor user {UserName}", DoctorRole, userName);
        }
    }

    private static InvalidOperationException CreateSeedFailure(string operation, IEnumerable<IdentityError> errors)
    {
        var message = string.Join(' ', errors.Select(static error => error.Description));
        return new InvalidOperationException($"Failed to {operation}. {message}".Trim());
    }
}
