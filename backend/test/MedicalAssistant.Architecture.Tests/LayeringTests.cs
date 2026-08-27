using System.Reflection;
using NetArchTest.Rules;

namespace MedicalAssistant.Architecture.Tests;

/// <summary>
/// Locks in the backend's Clean Architecture layering (R38): a capstone on top of
/// everything R24-R37 built this session, not a redesign. Project references already
/// make most of these violations impossible to compile — <c>Domain</c> has none,
/// <c>Application</c> references only <c>Domain</c> — but a project reference only
/// bounds what's <em>possible</em> to reference; it says nothing about what a NuGet
/// package reference lets a type actually use. These tests inspect the real built
/// assemblies (via NetArchTest/Mono.Cecil), so a stray `using Microsoft.EntityFrameworkCore`
/// landing in <c>Application</c> — bypassing the capability ports this session built
/// specifically to keep persistence concrete types out of it — fails here, not in review.
///
/// Each assertion is anchored on a real type so `typeof(T).Assembly` always resolves the
/// project actually under test, never a stale hand-maintained assembly name.
/// </summary>
public class LayeringTests
{
    private static readonly Assembly DomainAssembly = typeof(Domain.Consultation).Assembly;
    private static readonly Assembly ApplicationAssembly =
        typeof(Application.Contracts.Persistence.IConsultationAccess).Assembly;
    private static readonly Assembly PersistenceAssembly =
        typeof(Persistence.Modules.CareWorkflow.Consultations.ConsultationRepository).Assembly;
    private static readonly Assembly InfrastructureAssembly =
        typeof(Infrastructure.InfrastructureServiceRegistration).Assembly;
    private static readonly Assembly IdentityAssembly = typeof(Identity.IdentityServicesRegistration).Assembly;

    [Fact]
    public void Domain_has_no_dependency_on_any_outer_layer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "MedicalAssistant.Application",
                "MedicalAssistant.Persistence",
                "MedicalAssistant.Infrastructure",
                "MedicalAssistant.Identity",
                "MedicalAssistant.Api")
            .GetResult();

        AssertPasses(result, "Domain must not depend on Application, Persistence, Infrastructure, Identity, or Api");
    }

    [Fact]
    public void Domain_stays_plain_CSharp_with_no_EFCore_or_AspNetCore_dependency()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        AssertPasses(result, "Domain must stay plain C# — no EF Core or ASP.NET Core dependency");
    }

    [Fact]
    public void Application_has_no_dependency_on_any_concrete_adapter_layer()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "MedicalAssistant.Persistence",
                "MedicalAssistant.Infrastructure",
                "MedicalAssistant.Identity",
                "MedicalAssistant.Api")
            .GetResult();

        AssertPasses(
            result,
            "Application must depend only on its own Contracts interfaces, never a concrete adapter "
                + "(Persistence/Infrastructure/Identity/Api)");
    }

    [Fact]
    public void Application_has_no_direct_EFCore_dependency()
    {
        // The whole reason IConsultationDeletion / IConsultationAccess / IConsultationListing /
        // etc. (this session's R28 slices) exist as narrow ports: a use-case handler asks the
        // capability port for what it needs, never EF Core directly. If this ever fails, some
        // handler regressed back to reaching past its port into the ORM.
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertPasses(result, "Application must not depend on Microsoft.EntityFrameworkCore directly");
    }

    [Fact]
    public void Persistence_has_no_dependency_on_Infrastructure_Identity_or_Api()
    {
        var result = Types.InAssembly(PersistenceAssembly)
            .Should()
            .NotHaveDependencyOnAny("MedicalAssistant.Infrastructure", "MedicalAssistant.Identity", "MedicalAssistant.Api")
            .GetResult();

        AssertPasses(result, "Persistence must not depend on Infrastructure, Identity, or Api");
    }

    [Fact]
    public void Infrastructure_has_no_dependency_on_Persistence_Identity_or_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOnAny("MedicalAssistant.Persistence", "MedicalAssistant.Identity", "MedicalAssistant.Api")
            .GetResult();

        AssertPasses(result, "Infrastructure must not depend on Persistence, Identity, or Api");
    }

    [Fact]
    public void Identity_has_no_dependency_on_Persistence_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(IdentityAssembly)
            .Should()
            .NotHaveDependencyOnAny("MedicalAssistant.Persistence", "MedicalAssistant.Infrastructure", "MedicalAssistant.Api")
            .GetResult();

        AssertPasses(result, "Identity must not depend on Persistence, Infrastructure, or Api");
    }

    /// <summary>
    /// Fails with the offending type names inline, so a violation is actionable from the test
    /// output alone — not just a red X someone has to go hunt down in a debugger.
    /// </summary>
    private static void AssertPasses(TestResult result, string rule)
    {
        if (result.IsSuccessful)
            return;

        var offenders = string.Join(
            ", ",
            (result.FailingTypes ?? Enumerable.Empty<Type>()).Select(t => t.FullName));
        Assert.Fail($"{rule}. Offending types: {offenders}");
    }
}
