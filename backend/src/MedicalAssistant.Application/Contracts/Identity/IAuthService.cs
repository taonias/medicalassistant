using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Models.Identity;

namespace MedicalAssistant.Application.Contracts.Identity;

public interface IAuthService
{
    Task<AuthResponse> Login(AuthRequest request);
    Task<RegistrationResponse> Register(RegistrationRequest request);
    Task<UserSessionDto> GetUserSessionAsync(string userId);
    Task<UserSessionDto> UpdateProfileAsync(string userId, UpdateUserProfileRequest request);
    Task ChangePasswordAsync(string userId, ChangePasswordRequest request);
    Task<PagedResult<UserListItemDto>> GetUsersAsync(int? page, int? pageSize);
    Task SetUserApprovalAsync(string userId, bool isApproved, string actingUserId);
}
