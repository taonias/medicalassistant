using MedicalAssistant.Application.Features.Chat.Queries.ChatQuery;
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

    public ChatController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("query")]
    public async Task<ActionResult<ChatResponseDto>> Query(ChatQuery query)
    {
        var response = await _mediator.Send(query);
        return Ok(response);
    }
}
