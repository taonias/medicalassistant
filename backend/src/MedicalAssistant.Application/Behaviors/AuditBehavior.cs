using MedicalAssistant.Application.Contracts.Logging;
using MediatR;

namespace MedicalAssistant.Application.Behaviors;

/// <summary>
/// Central audit-logging behavior. Every business command that implements
/// <see cref="IAuditableRequest{TResponse}"/> is recorded in the audit log here - once, after
/// its handler succeeds - so audit emission lives in one place instead of being scattered
/// across controllers. Commands that do not implement the marker are ignored (e.g. queries and
/// non-audited commands). Registered after <see cref="ValidationBehavior{TRequest,TResponse}"/>
/// so invalid requests are rejected before they can be audited, and a handler that throws is
/// never audited.
/// </summary>
public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAuditLogger _auditLogger;

    public AuditBehavior(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        if (request is IAuditableRequest<TResponse> auditable)
        {
            var entry = auditable.ToAuditEntry(response);
            await _auditLogger.LogAsync(
                entry.Action,
                entry.EntityType,
                entry.EntityId,
                entry.Details,
                entry.ActorId,
                entry.ActorName);
        }

        return response;
    }
}
