namespace MedicalAssistant.Application.Models.Identity;

public class AuthRequest
{
    public required string UserName { get; set; }
    public required string Password { get; set; }
}

public class RegistrationRequest
{
    public required string Email { get; set; }
    public required string UserName { get; set; }
    public required string Password { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}

public class AuthResponse
{
    public required string Id { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public required string Token { get; set; }
    public List<string> Roles { get; set; } = [];
}

public class RegistrationResponse
{
    public required string UserId { get; set; }
}

public class UserSessionDto
{
    public required string Id { get; set; }
    public required string UserName { get; set; }
    public required string Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}

public class UpdateUserProfileRequest
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
}

public class ChangePasswordRequest
{
    public required string CurrentPassword { get; set; }
    public required string NewPassword { get; set; }
}
