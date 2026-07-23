using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalAssistant.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(AuthRequest request)
    {
        var response = await _authService.Login(request);
        return Ok(response);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegistrationResponse>> Register(RegistrationRequest request)
    {
        var response = await _authService.Register(request);
        return Ok(response);
    }

    [HttpGet("session")]
    [Authorize]
    public async Task<ActionResult<UserSessionDto>> GetSession()
    {
        var userId = User.FindFirst("uid")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var session = await _authService.GetUserSessionAsync(userId);
        return Ok(session);
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<UserSessionDto>> UpdateProfile(UpdateUserProfileRequest request)
    {
        var userId = User.FindFirst("uid")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var session = await _authService.UpdateProfileAsync(userId, request);
        return Ok(session);
    }

    [HttpPut("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = User.FindFirst("uid")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _authService.ChangePasswordAsync(userId, request);
        return NoContent();
    }
}
