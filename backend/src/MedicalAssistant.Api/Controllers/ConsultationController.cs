using MedicalAssistant.Application.Features.Consultation.Command.AssignConsultationPatient;
using MedicalAssistant.Application.Features.Consultation.Command.CreateConsultation;
using MedicalAssistant.Application.Features.Consultation.Command.DeleteConsultation;
using MedicalAssistant.Application.Features.Consultation.Command.RetryConsultationProcessing;
using MedicalAssistant.Application.Features.Consultation.Command.UploadConsultationAudio;
using MedicalAssistant.Application.Features.Consultation.Command.UploadConsultationDocument;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationAudio;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDocument;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationsByPatient;
using MedicalAssistant.Application.Features.Consultation.Queries.GetDashboardAnalytics;
using MedicalAssistant.Application.Features.Consultation.Queries.GetDraftConsultations;
using MedicalAssistant.Application.Features.Consultation.Queries.GetUnattachedDraftConsultations;
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

    public ConsultationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("drafts")]
    public async Task<ActionResult<List<DraftConsultationGroupDto>>> GetDrafts()
    {
        var groups = await _mediator.Send(new GetDraftConsultationsQuery());
        return Ok(groups);
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<DashboardAnalyticsDto>> GetAnalytics()
    {
        var analytics = await _mediator.Send(new GetDashboardAnalyticsQuery());
        return Ok(analytics);
    }

    [HttpGet("drafts/unattached")]
    public async Task<ActionResult<List<ConsultationSummaryDto>>> GetUnattachedDrafts()
    {
        var drafts = await _mediator.Send(new GetUnattachedDraftConsultationsQuery());
        return Ok(drafts);
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

    [HttpPut("{id:int}/patient")]
    public async Task<ActionResult<ConsultationDto>> AssignPatient(int id, [FromBody] AssignPatientBody body)
    {
        var response = await _mediator.Send(new AssignConsultationPatientCommand
        {
            ConsultationId = id,
            PatientId = body.PatientId,
        });
        return Ok(response);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new DeleteConsultationCommand(id));
        return NoContent();
    }

    public record AssignPatientBody(int PatientId);

    [HttpPost]
    public async Task<ActionResult<ConsultationDto>> Post(
        CreateConsultationCommand command,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            command.IdempotencyKey = idempotencyKey;

        var existingKey = command.IdempotencyKey;
        var response = await _mediator.Send(command);

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
        return Ok(response);
    }

    [HttpPost("{id}/document")]
    [RequestSizeLimit(104_857_600)]
    public async Task<ActionResult<ConsultationDto>> UploadDocument(int id, IFormFile documentFile)
    {
        var command = new UploadConsultationDocumentCommand
        {
            ConsultationId = id,
            DocumentFile = documentFile,
        };

        var response = await _mediator.Send(command);
        return Ok(response);
    }

    [HttpPost("{id:int}/retry")]
    public async Task<ActionResult<ConsultationDto>> Retry(int id)
    {
        var response = await _mediator.Send(new RetryConsultationProcessingCommand
        {
            ConsultationId = id,
        });
        return Ok(response);
    }

    [HttpGet("{id:int}/audio")]
    public async Task<IActionResult> GetAudio(int id)
    {
        var audio = await _mediator.Send(new GetConsultationAudioQuery(id));
        if (audio == null)
            return NotFound();

        return File(audio.Content, audio.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("{id:int}/document")]
    public async Task<IActionResult> GetDocument(int id)
    {
        var document = await _mediator.Send(new GetConsultationDocumentQuery(id));
        if (document == null)
            return NotFound();

        return File(document.Content, document.ContentType, document.FileName);
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

