using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Features.Consultation.Command.CreateConsultation;
using MedicalAssistant.Application.Features.Consultation.Command.UploadConsultationAudio;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationsByPatient;
using MedicalAssistant.Application.Features.Consultation.Queries.GetDraftConsultations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalAssistant.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ConsultationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuditLogger _auditLogger;

    public ConsultationController(IMediator mediator, IAuditLogger auditLogger)
    {
        _mediator = mediator;
        _auditLogger = auditLogger;
    }

    [HttpGet("drafts")]
    public async Task<ActionResult<List<DraftConsultationGroupDto>>> GetDrafts()
    {
        var groups = await _mediator.Send(new GetDraftConsultationsQuery());
        return Ok(groups);
    }

    [HttpGet("patient/{patientId}")]
    public async Task<ActionResult<List<ConsultationSummaryDto>>> GetByPatient(int patientId)
    {
        var consultations = await _mediator.Send(new GetConsultationsByPatientQuery(patientId));
        return Ok(consultations);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ConsultationDto>> Get(int id)
    {
        var consultation = await _mediator.Send(new GetConsultationDetailsQuery(id));
        return Ok(consultation);
    }

    [HttpPost]
    public async Task<ActionResult<ConsultationDto>> Post(
        CreateConsultationCommand command,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            command.IdempotencyKey = idempotencyKey;

        var existingKey = command.IdempotencyKey;
        var response = await _mediator.Send(command);

        await _auditLogger.LogAsync("CreateConsultation", "Consultation", response.Id.ToString());

        if (!string.IsNullOrWhiteSpace(existingKey) && response.IdempotencyKey == existingKey)
        {
            var check = await _mediator.Send(new GetConsultationDetailsQuery(response.Id));
            if (check != null)
                return Ok(response);
        }

        return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
    }

    [HttpPost("{id}/audio")]
    [RequestSizeLimit(104_857_600)]
    public async Task<ActionResult<ConsultationDto>> UploadAudio(int id, IFormFile audioFile, [FromForm] int? durationSeconds)
    {
        var command = new UploadConsultationAudioCommand
        {
            ConsultationId = id,
            AudioFile = audioFile,
            DurationSeconds = durationSeconds
        };

        var response = await _mediator.Send(command);
        await _auditLogger.LogAsync("UploadAudio", "Consultation", id.ToString());
        return Ok(response);
    }

    [HttpGet("{id}/structured-data")]
    public async Task<ActionResult<
        MedicalAssistant.Application.Features.MedicalStructuredData.Queries.GetStructuredData.MedicalStructuredDataDto?>>
        GetStructuredData(int id)
    {
        var data = await _mediator.Send(
            new MedicalAssistant.Application.Features.MedicalStructuredData.Queries.GetStructuredData.GetStructuredDataQuery(id));
        return Ok(data);
    }

    [HttpPost("{id}/structured-data/approve")]
    public async Task<IActionResult> ApproveStructuredData(int id)
    {
        await _mediator.Send(
            new MedicalAssistant.Application.Features.MedicalStructuredData.Command.ApproveStructuredData.ApproveStructuredDataCommand(id));

        return NoContent();
    }
}

