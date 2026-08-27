using MedicalAssistant.Application.Modules.CareWorkflow.Transcripts.UpdateTranscript;
using MedicalAssistant.Application.Modules.CareWorkflow.Transcripts.GetTranscript;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalAssistant.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TranscriptController : ControllerBase
{
    private readonly IMediator _mediator;

    public TranscriptController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{consultationId}")]
    public async Task<ActionResult<TranscriptDto?>> Get(int consultationId)
    {
        var transcript = await _mediator.Send(new GetTranscriptQuery(consultationId));
        return Ok(transcript);
    }

    [HttpPut("{consultationId}")]
    public async Task<ActionResult<TranscriptDto>> Update(
        int consultationId,
        [FromBody] UpdateTranscriptRequest request)
    {
        var transcript = await _mediator.Send(new UpdateTranscriptCommand
        {
            ConsultationId = consultationId,
            Transcript = request.Transcript,
        });
        return Ok(transcript);
    }
}

public class UpdateTranscriptRequest
{
    public required string Transcript { get; set; }
}
