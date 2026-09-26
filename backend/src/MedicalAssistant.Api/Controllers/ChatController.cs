using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Features.Chat.Command.SubmitChatQuery;
using MedicalAssistant.Application.Features.Chat.Queries.GetChatRequestStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalAssistant.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuditLogger _auditLogger;

    public ChatController(IMediator mediator, IAuditLogger auditLogger)
    {
        _mediator = mediator;
        _auditLogger = auditLogger;
    }

    [HttpPost("query")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ChatJobDto>> Query(SubmitChatQueryCommand command)
    {
        var response = await _mediator.Send(command);
        await _auditLogger.LogAsync("SubmitChatQuery", "ChatRequest", response.CorrelationId, null);
        return Accepted(response);
    }

    [HttpGet("{correlationId}")]
    public async Task<ActionResult<ChatJobDto>> GetStatus(string correlationId)
    {
        var response = await _mediator.Send(new GetChatRequestStatusQuery(correlationId));
        return Ok(response);
    }
}
