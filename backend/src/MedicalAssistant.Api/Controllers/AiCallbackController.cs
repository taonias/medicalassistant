using MedicalAssistant.Application.Features.ActionRequest.Command.ProcessActionCallback;
using MedicalAssistant.Application.Features.MedicalStructuredData.Command.ProcessStructuredDataCallback;
using MedicalAssistant.Application.Features.Transcript.Command.ProcessTranscriptionCallback;
using MedicalAssistant.Application.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Api.Controllers;

[Route("api/ai-callback")]
[ApiController]
public class AiCallbackController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly AiCallbackSettings _settings;

    public AiCallbackController(IMediator mediator, IOptions<AiCallbackSettings> settings)
    {
        _mediator = mediator;
        _settings = settings.Value;
    }

    [HttpPost("transcription")]
    public async Task<IActionResult> Transcription([FromBody] TranscriptionCallbackDto dto)
    {
        if (!IsAuthorized())
            return Unauthorized();

        await _mediator.Send(new ProcessTranscriptionCallbackCommand
        {
            JobId = dto.JobId,
            CorrelationId = dto.CorrelationId,
            ConsultationId = dto.ConsultationId,
            Status = dto.Status,
            Transcript = dto.Transcript,
            FailureReason = dto.FailureReason
        });

        return Ok();
    }

    [HttpPost("structured-data")]
    public async Task<IActionResult> StructuredData([FromBody] StructuredDataCallbackDto dto)
    {
        if (!IsAuthorized())
            return Unauthorized();

        await _mediator.Send(new ProcessStructuredDataCallbackCommand
        {
            JobId = dto.JobId,
            CorrelationId = dto.CorrelationId,
            ConsultationId = dto.ConsultationId,
            TranscriptId = dto.TranscriptId,
            SchemaVersion = dto.SchemaVersion ?? "v1",
            StructuredPayload = dto.StructuredPayload ?? "{}",
            Status = dto.Status,
            FailureReason = dto.FailureReason
        });

        return Ok();
    }

    [HttpPost("action")]
    public async Task<IActionResult> Action([FromBody] ActionCallbackDto dto)
    {
        if (!IsAuthorized())
            return Unauthorized();

        await _mediator.Send(new ProcessActionCallbackCommand
        {
            JobId = dto.JobId,
            CorrelationId = dto.CorrelationId,
            Status = dto.Status,
            ResponsePayload = dto.ResponsePayload,
            FailureReason = dto.FailureReason
        });

        return Ok();
    }

    private bool IsAuthorized()
    {
        if (Request.Headers.TryGetValue("X-Api-Key", out var apiKey))
            return string.Equals(apiKey, _settings.ApiKey, StringComparison.Ordinal);

        return false;
    }
}

public class TranscriptionCallbackDto
{
    public required string JobId { get; set; }
    public required string CorrelationId { get; set; }
    public int ConsultationId { get; set; }
    public required string Status { get; set; }
    public string? Transcript { get; set; }
    public string? FailureReason { get; set; }
}

public class StructuredDataCallbackDto
{
    public required string JobId { get; set; }
    public required string CorrelationId { get; set; }
    public int ConsultationId { get; set; }
    public int? TranscriptId { get; set; }
    public string? SchemaVersion { get; set; }
    public string? StructuredPayload { get; set; }
    public required string Status { get; set; }
    public string? FailureReason { get; set; }
}

public class ActionCallbackDto
{
    public required string JobId { get; set; }
    public required string CorrelationId { get; set; }
    public required string Status { get; set; }
    public string? ResponsePayload { get; set; }
    public string? FailureReason { get; set; }
}
