using MedicalAssistant.Application.Features.Chat.Command.CreateConversation;
using MedicalAssistant.Application.Features.Chat.Command.DeleteConversation;
using MedicalAssistant.Application.Features.Chat.Command.RenameConversation;
using MedicalAssistant.Application.Features.Chat.Command.RetryChatTurn;
using MedicalAssistant.Application.Features.Chat.Common;
using MedicalAssistant.Application.Features.Chat.Query.GetConversationMessages;
using MedicalAssistant.Application.Features.Chat.Query.ListConversations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalAssistant.Api.Controllers;

[Route("api/conversations")]
[ApiController]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ConversationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Explicitly starts a new conversation ("New conversation" button).</summary>
    [HttpPost]
    public async Task<ActionResult<ConversationSummaryDto>> Create(CreateConversationCommand command)
    {
        var created = await _mediator.Send(command);
        return Ok(created);
    }

    /// <summary>A doctor's active conversations about one patient (history list).</summary>
    [HttpGet("/api/patients/{patientId:int}/conversations")]
    public async Task<ActionResult<IReadOnlyList<ConversationSummaryDto>>> ListForPatient(int patientId)
    {
        var conversations = await _mediator.Send(new ListConversationsQuery(patientId));
        return Ok(conversations);
    }

    /// <summary>Rehydrate a conversation thread (header + messages + structured citations).</summary>
    [HttpGet("{conversationId:int}/messages")]
    public async Task<ActionResult<ConversationThreadDto>> GetMessages(int conversationId)
    {
        var thread = await _mediator.Send(new GetConversationMessagesQuery(conversationId));
        return Ok(thread);
    }

    /// <summary>Rename a conversation.</summary>
    [HttpPatch("{conversationId:int}")]
    public async Task<ActionResult<ConversationSummaryDto>> Rename(int conversationId, [FromBody] RenameConversationRequest request)
    {
        var updated = await _mediator.Send(new RenameConversationCommand
        {
            ConversationId = conversationId,
            Title = request.Title,
        });
        return Ok(updated);
    }

    /// <summary>Soft-delete (archive) a conversation.</summary>
    [HttpDelete("{conversationId:int}")]
    public async Task<IActionResult> Delete(int conversationId)
    {
        await _mediator.Send(new DeleteConversationCommand { ConversationId = conversationId });
        return NoContent();
    }

    /// <summary>Doctor-triggered manual retry of a failed turn (reuses the askId).</summary>
    [HttpPost("{conversationId:int}/messages/{messageId:int}/retry")]
    public async Task<ActionResult<AskChatResponse>> Retry(int conversationId, int messageId)
    {
        var response = await _mediator.Send(new RetryChatTurnCommand
        {
            ConversationId = conversationId,
            MessageId = messageId,
        });
        return Ok(response);
    }
}

public sealed class RenameConversationRequest
{
    public required string Title { get; set; }
}
