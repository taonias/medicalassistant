using System.Reflection;
using MedicalAssistance.Ingestion.Api.GroundedChat;
using NetArchTest.Rules;

namespace MedicalAssistance.Ingestion.Api.Tests;

/// <summary>
/// Locks in the AI service's real module boundaries (R38), verified rather than assumed:
/// aligning <c>Modules/DocumentLifecycle</c>, <c>Modules/GroundedChat</c>, and
/// <c>Modules/Ingestion</c> onto their own namespaces (they previously shared flat
/// <c>Ingestions</c>/<c>Controllers</c>/<c>Chat</c>/<c>Realtime</c> namespaces across module
/// boundaries) turned out to reveal, not assume, the graph these tests assert:
///
/// <list type="bullet">
/// <item><description><c>Ingestions</c> and <c>DocumentLifecycle</c> are genuinely, bidirectionally
/// coupled — <c>IngestionStore</c>/<c>IngestionDocumentCatalogStore</c> return
/// <c>PatientDocument</c>, while <c>PatientDocument</c>/<c>PatientSummaryService</c> use
/// <c>IngestionStore</c>/<c>DocumentIdentity</c>/<c>AgentInstructionProvider</c> right back. That
/// coupling pre-dates this alignment; it was only invisible because both modules shared one
/// namespace. Not asserted against here — fixing it is a real design change (a separate,
/// deliberately scoped decision), not a namespace move.</description></item>
/// <item><description><c>GroundedChat</c> has no dependency on <c>DocumentLifecycle</c> at all,
/// and reaches <c>Ingestions</c> for exactly one shared thing: <c>AgentInstructionProvider</c>
/// (the same prompt-instruction lookup the ingestion strategies use), from exactly
/// <c>GroundedAnswerGenerator</c> and <c>ConversationSummarizer</c>. First attempt at this test
/// asserted zero dependency on <c>Ingestions</c> outright and failed — a real, useful catch: it
/// turned out these two files already had that dependency before this alignment, just invisible
/// under the old shared namespace. The test below locks in today's boundary exactly — this
/// specific, narrow exception and nothing wider — so a new file quietly reaching into Ingestion's
/// persistence internals from GroundedChat fails here, not in review.</description></item>
/// </list>
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly Assembly IngestionApiAssembly = typeof(ChatController).Assembly;

    [Fact]
    public void GroundedChat_has_no_dependency_on_DocumentLifecycle()
    {
        var result = Types.InAssembly(IngestionApiAssembly)
            .That()
            .ResideInNamespace("MedicalAssistance.Ingestion.Api.GroundedChat")
            .Should()
            .NotHaveDependencyOn("MedicalAssistance.Ingestion.Api.DocumentLifecycle")
            .GetResult();

        AssertPasses(result, "GroundedChat must reach document data only through Retrieval, never DocumentLifecycle directly");
    }

    [Fact]
    public void GroundedChats_dependency_on_Ingestions_is_limited_to_the_known_shared_AgentInstructionProvider()
    {
        var result = Types.InAssembly(IngestionApiAssembly)
            .That()
            .ResideInNamespace("MedicalAssistance.Ingestion.Api.GroundedChat")
            .And()
            .DoNotHaveName("GroundedAnswerGenerator")
            .And()
            .DoNotHaveName("ConversationSummarizer")
            .Should()
            .NotHaveDependencyOn("MedicalAssistance.Ingestion.Api.Ingestions")
            .GetResult();

        AssertPasses(
            result,
            "Only GroundedAnswerGenerator and ConversationSummarizer may depend on Ingestions (for the shared "
                + "AgentInstructionProvider) — no other GroundedChat type should reach into Ingestion's internals");
    }

    [Fact]
    public void Retrieval_has_no_dependency_on_GroundedChat_or_DocumentLifecycle()
    {
        var result = Types.InAssembly(IngestionApiAssembly)
            .That()
            .ResideInNamespace("MedicalAssistance.Ingestion.Api.Retrieval")
            .Should()
            .NotHaveDependencyOnAny(
                "MedicalAssistance.Ingestion.Api.GroundedChat",
                "MedicalAssistance.Ingestion.Api.DocumentLifecycle")
            .GetResult();

        AssertPasses(result, "Retrieval is a lower-layer pipeline; it must not depend on GroundedChat or DocumentLifecycle");
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
