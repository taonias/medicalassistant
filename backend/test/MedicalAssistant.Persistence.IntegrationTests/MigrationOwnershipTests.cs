namespace MedicalAssistant.Persistence.IntegrationTests;

public class MigrationOwnershipTests
{
    [Fact]
    public void Api_startup_does_not_apply_database_migrations()
    {
        var program = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "backend",
            "src",
            "MedicalAssistant.Api",
            "Program.cs"));

        Assert.DoesNotContain("PersistenceDbInitializer.MigrateAsync", program);
        Assert.DoesNotContain("Database.MigrateAsync", program);
    }

    [Fact]
    public void Role_seeding_does_not_apply_identity_migrations_as_a_side_effect()
    {
        var initializer = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "backend",
            "src",
            "MedicalAssistant.Identity",
            "IdentityDbInitializer.cs"));

        Assert.DoesNotContain("Database.MigrateAsync", initializer);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
