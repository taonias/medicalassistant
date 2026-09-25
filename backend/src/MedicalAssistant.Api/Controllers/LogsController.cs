using MedicalAssistant.Application.Features.Logs.Queries.GetAuditLogs;
using MedicalAssistant.Application.Features.Logs.Queries.GetErrorLogs;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Models.Logging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalAssistant.Api.Controllers;

[Route("api/logs")]
[ApiController]
[Authorize(Roles = "Administrator")]
public class LogsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LogsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("audit")]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAuditLogs(
        [FromQuery] int? page,
        [FromQuery] int? pageSize)
    {
        var logs = await _mediator.Send(new GetAuditLogsQuery(page, pageSize));
        return Ok(logs);
    }

    [HttpGet("errors")]
    public async Task<ActionResult<PagedResult<ErrorLogDto>>> GetErrorLogs(
        [FromQuery] int? page,
        [FromQuery] int? pageSize)
    {
        var logs = await _mediator.Send(new GetErrorLogsQuery(page, pageSize));
        return Ok(logs);
    }
}
