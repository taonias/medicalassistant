using FluentValidation;
using MedicalAssistant.Application.Configuration;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Models.Identity;
using MedicalAssistant.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MedicalAssistant.Application.Models;

namespace MedicalAssistant.Identity.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IValidator<RegistrationRequest> _registrationValidator;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IValidator<RegistrationRequest> registrationValidator,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _registrationValidator = registrationValidator;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    private const string InvalidCredentialsMessage = "Invalid username or password.";

    public async Task<AuthResponse> Login(AuthRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.UserName)
            ?? (request.UserName.Contains('@') ? await _userManager.FindByEmailAsync(request.UserName) : null);

        if (user == null)
            throw new BadRequestException(InvalidCredentialsMessage);

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!result.Succeeded)
            throw new BadRequestException(InvalidCredentialsMessage);

        if (!user.IsApproved)
            throw new BadRequestException("Your account is pending administrator approval.");

        return await CreateAuthResponseAsync(user);
    }

    public async Task<RegistrationResponse> Register(RegistrationRequest request)
    {
        var validationResult = await _registrationValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new BadRequestException("Validation failed", validationResult);

        var user = new ApplicationUser
        {
            Email = request.Email.Trim(),
            UserName = request.UserName.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmailConfirmed = true,
            IsApproved = false
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var message = string.Join(' ', result.Errors.Select(e => e.Description));
            throw new BadRequestException(message);
        }

        await _userManager.AddToRoleAsync(user, "Doctor");
        return new RegistrationResponse { UserId = user.Id, IsApproved = user.IsApproved };
    }

    public async Task<UserSessionDto> GetUserSessionAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("User not found.", userId);

        return new UserSessionDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            FirstName = user.FirstName ?? string.Empty,
            LastName = user.LastName ?? string.Empty
        };
    }

    public async Task<UserSessionDto> UpdateProfileAsync(string userId, UpdateUserProfileRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("User not found.", userId);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = request.Email.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var message = string.Join(' ', result.Errors.Select(e => e.Description));
            throw new BadRequestException(message);
        }

        return await GetUserSessionAsync(userId);
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("User not found.", userId);

        var validCurrent = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
        if (!validCurrent)
            throw new BadRequestException("Current password is incorrect.");

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var message = string.Join(' ', result.Errors.Select(e => e.Description));
            throw new BadRequestException(message);
        }
    }

    public async Task<PagedResult<UserListItemDto>> GetUsersAsync(int? page, int? pageSize)
    {
        var (normalizedPage, normalizedSize) = Paging.Normalize(page, pageSize);
        var query = _userManager.Users.AsNoTracking().OrderBy(u => u.UserName);
        var totalCount = await query.CountAsync();
        normalizedPage = Paging.ClampPage(normalizedPage, normalizedSize, totalCount);

        var users = await query
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync();

        var items = new List<UserListItemDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new UserListItemDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                IsApproved = user.IsApproved,
                Roles = roles.ToList()
            });
        }

        return new PagedResult<UserListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = normalizedPage,
            PageSize = normalizedSize
        };
    }

    public async Task SetUserApprovalAsync(string userId, bool isApproved, string actingUserId)
    {
        if (string.Equals(userId, actingUserId, StringComparison.Ordinal))
            throw new BadRequestException("You cannot change approval for your own account.");

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("User not found.", userId);

        user.IsApproved = isApproved;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var message = string.Join(' ', result.Errors.Select(e => e.Description));
            throw new BadRequestException(message);
        }
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(ApplicationUser user)
    {
        var token = await GenerateJwtToken(user);
        var roles = await _userManager.GetRolesAsync(user);

        return new AuthResponse
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Roles = roles.ToList()
        };
    }

    private async Task<JwtSecurityToken> GenerateJwtToken(ApplicationUser user)
    {
        var userClaims = await _userManager.GetClaimsAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        var roleClaims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserName ?? user.Id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim("uid", user.Id)
        }.Union(userClaims).Union(roleClaims);

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key)),
            SecurityAlgorithms.HmacSha256);

        return new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
            signingCredentials: signingCredentials);
    }
}
