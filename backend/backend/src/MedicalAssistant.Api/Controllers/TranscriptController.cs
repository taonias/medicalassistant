using MedicalAssistant.Application.Features.Transcript.Queries.GetTranscript;
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
    public async Task<ActionResult<TranscriptDto>> Get(int consultationId)
    {
        var transcript = await _mediator.Send(new GetTranscriptQuery(consultationId));
        if (transcript == null)
            return NotFound();

        return Ok(transcript);
    }
}
