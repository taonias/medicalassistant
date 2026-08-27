using MedicalAssistant.Application.Modules.Assistance.Chat.AskChat;
using MedicalAssistant.Application.Modules.Assistance.Chat;
using MedicalAssistant.Application.Modules.Assistance.Chat.ChatQuery;
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

    /// <summary>
    /// Asks one grounded turn. With a conversationId the turn is appended; without one a
    /// conversation is auto-created and auto-titled. The answer is returned whole (never streamed).
    /// </summary>
    [HttpPost("ask")]
    public async Task<ActionResult<AskChatResponse>> Ask(AskChatCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>Legacy stateless single-shot query (no history). Retained for back-compat.</summary>
    [HttpPost("query")]
    public async Task<ActionResult<ChatResponseDto>> Query(ChatQuery query)
    {
        var response = await _mediator.Send(query);
        return Ok(response);
    }
}
