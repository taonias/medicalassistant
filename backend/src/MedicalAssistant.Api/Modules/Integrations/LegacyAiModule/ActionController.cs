using MedicalAssistant.Application.Modules.Integrations.LegacyAiModule.TriggerAiAction;
using MedicalAssistant.Application.Modules.Integrations.LegacyAiModule.GetActionRequestStatus;
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

    public ActionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("trigger")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ActionRequestDto>> Trigger(TriggerAiActionCommand command)
    {
        var response = await _mediator.Send(command);
        return Accepted(response);
    }

    [HttpGet("{correlationId}")]
    public async Task<ActionResult<ActionRequestDto>> GetStatus(string correlationId)
    {
        var response = await _mediator.Send(new GetActionRequestStatusQuery(correlationId));
        return Ok(response);
    }
}
