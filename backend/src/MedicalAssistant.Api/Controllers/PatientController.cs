using MedicalAssistant.Application.Features.Patient.Command.CreatePatient;
using MedicalAssistant.Application.Features.Patient.Command.UpdatePatient;
using MedicalAssistant.Application.Features.Patient.Queries.GetPatientById;
using MedicalAssistant.Application.Features.Patient.Queries.GetPatientHistory;
using MedicalAssistant.Application.Features.Patient.Queries.GetPatientsList;
using MedicalAssistant.Application.Models.Patients;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalAssistant.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PatientController : ControllerBase
{
    private readonly IMediator _mediator;

    public PatientController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PatientListItem>>> List()
    {
        var patients = await _mediator.Send(new GetPatientsListQuery());
        return Ok(patients);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PatientDto>> Get(int id)
    {
        var patient = await _mediator.Send(new GetPatientByIdQuery(id));
        return Ok(patient);
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<PatientHistoryDto>> GetHistory(
        int id,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool includeTranscripts = false,
        [FromQuery] bool includeStructuredData = true,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? source = null)
    {
        var history = await _mediator.Send(new GetPatientHistoryQuery(
            id,
            fromDate,
            toDate,
            includeTranscripts,
            includeStructuredData,
            page,
            pageSize,
            source));
        return Ok(history);
    }

    [HttpPost]
    public async Task<ActionResult<PatientDto>> Post(CreatePatientCommand command)
    {
        var response = await _mediator.Send(command);
        return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
    }

    [HttpPut]
    public async Task<ActionResult<PatientDto>> Put(UpdatePatientCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }
}
