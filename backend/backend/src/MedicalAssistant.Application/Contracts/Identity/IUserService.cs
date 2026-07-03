namespace MedicalAssistant.Application.Contracts.Identity;

public interface IUserService
{
    Task<string?> GetUserIdByEmailAsync(string email);
    string? GetCurrentUserName();
    bool IsCurrentUserAdmin();
    string? GetCurrentUserEmail();
    Task<string?> GetCurrentUserIdAsync();
}
