using MedicalAssistant.Application.Modules.CareWorkflow.DoctorNotes.CreateDoctorNote;
using MedicalAssistant.Application.Modules.CareWorkflow.DoctorNotes.GetDoctorNotesByConsultation;
using MedicalAssistant.Application.Modules.CareWorkflow.DoctorNotes.GetPatientLevelDoctorNotes;
using MedicalAssistant.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalAssistant.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DoctorNotesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DoctorNotesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("consultations/{consultationId}")]
    public async Task<ActionResult<IReadOnlyList<DoctorNote>>> GetByConsultation(int consultationId)
    {
        var notes = await _mediator.Send(new GetDoctorNotesByConsultationQuery(consultationId));
        return Ok(notes);
    }

    [HttpGet("patients/{patientId}")]
    public async Task<ActionResult<IReadOnlyList<DoctorNote>>> GetPatientLevelNotes(int patientId)
    {
        var notes = await _mediator.Send(new GetPatientLevelDoctorNotesQuery(patientId));
        return Ok(notes);
    }

    [HttpPost]
    public async Task<ActionResult<DoctorNote>> Post(CreateDoctorNoteCommand command)
    {
        var note = await _mediator.Send(command);
        return Ok(note);
    }
}
