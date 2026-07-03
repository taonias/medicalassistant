using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Features.ActionRequest.Command.TriggerAiAction;
using MedicalAssistant.Application.Features.ActionRequest.Queries.GetActionRequestStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalAssistant.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ActionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuditLogger _auditLogger;

    public ActionController(IMediator mediator, IAuditLogger auditLogger)
    {
        _mediator = mediator;
        _auditLogger = auditLogger;
    }

    [HttpPost("trigger")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ActionRequestDto>> Trigger(TriggerAiActionCommand command)
    {
        var response = await _mediator.Send(command);
        await _auditLogger.LogAsync("TriggerAction", "ActionRequest", response.CorrelationId,
            $"ActionType: {response.ActionType}");
        return Accepted(response);
    }

    [HttpGet("{correlationId}")]
    public async Task<ActionResult<ActionRequestDto>> GetStatus(string correlationId)
    {
        var response = await _mediator.Send(new GetActionRequestStatusQuery(correlationId));
        return Ok(response);
    }
}
