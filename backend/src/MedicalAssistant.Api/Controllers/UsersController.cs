using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalAssistant.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Administrator")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;

    public UsersController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> Get(
        [FromQuery] int? page,
        [FromQuery] int? pageSize)
    {
        var users = await _authService.GetUsersAsync(page, pageSize);
        return Ok(users);
    }

    [HttpPut("{id}/approval")]
    public async Task<IActionResult> SetApproval(string id, SetUserApprovalRequest request)
    {
        var actingUserId = User.FindFirst("uid")?.Value;
        if (string.IsNullOrEmpty(actingUserId))
            return Unauthorized();

        await _authService.SetUserApprovalAsync(id, request.IsApproved, actingUserId);
        return NoContent();
    }
}
