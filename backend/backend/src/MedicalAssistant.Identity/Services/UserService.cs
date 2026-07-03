using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Identity.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MedicalAssistant.Identity.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserService(UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string?> GetUserIdByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var normalized = _userManager.NormalizeEmail(email);
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized);
        return user?.Id;
    }

    public string? GetCurrentUserName()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public bool IsCurrentUserAdmin()
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole("Administrator") ?? false;
    }

    public string? GetCurrentUserEmail()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirst(ClaimTypes.Email)?.Value
            ?? user?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
    }

    public async Task<string?> GetCurrentUserIdAsync()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal == null) return null;

        var uid = principal.FindFirst("uid")?.Value;
        if (!string.IsNullOrEmpty(uid)) return uid;

        var email = GetCurrentUserEmail();
        return string.IsNullOrEmpty(email) ? null : await GetUserIdByEmailAsync(email);
    }
}
