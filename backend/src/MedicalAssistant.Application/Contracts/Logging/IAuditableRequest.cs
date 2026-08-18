namespace MedicalAssistant.Application.Contracts.Logging;

/// <summary>
/// A single audit-trail entry describing a business action. Produced by an
/// <see cref="IAuditableRequest{TResponse}"/> command and written centrally by the audit
/// pipeline behavior after the command handler succeeds.
/// </summary>
/// <remarks>
/// <see cref="ActorId"/>/<see cref="ActorName"/> are normally left null so the audit logger
/// records the authenticated doctor from the current request. Set them for system events that
/// run without a doctor identity (e.g. AI-module callbacks) to record a fixed actor instead.
/// </remarks>
public sealed record AuditEntry(
    string Action,
    string? EntityType = null,
    string? EntityId = null,
    string? Details = null,
    string? ActorId = null,
    string? ActorName = null);

/// <summary>
/// Marks a MediatR command as a business-valuable action that should be recorded in the audit
/// log. The command declares its own audit metadata (co-located with the command), and the
/// central <c>AuditBehavior</c> emits it once, after the handler completes successfully. This
/// is the single place audit entries are written for command-driven actions; there is no need
/// to call <see cref="IAuditLogger"/> from controllers.
/// </summary>
/// <typeparam name="TResponse">The command's response type, so the entry can use values that
/// are only known after handling (e.g. a newly created entity's id).</typeparam>
public interface IAuditableRequest<in TResponse>
{
    AuditEntry ToAuditEntry(TResponse response);
}

/// <summary>
/// Fixed actor identities for audited system events that run without a doctor identity.
/// </summary>
public static class SystemAuditActors
{
    public const string AiModuleId = "ai-module";
    public const string AiModuleName = "AI Module (callback)";
}
