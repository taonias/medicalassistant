using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Domain;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace MedicalAssistant.Persistence.Logging;

public class AuditLogger : IAuditLogger
{
    private readonly MedicalAssistantDatabaseContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogger(MedicalAssistantDatabaseContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string action, string? entityType = null, string? entityId = null, string? details = null, string? overrideUserId = null, string? overrideUserName = null)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var userId = overrideUserId ?? user?.FindFirst("uid")?.Value ?? "Anonymous";
        var userName = overrideUserName
            ?? user?.FindFirst("username")?.Value
            ?? user?.FindFirst(ClaimTypes.Name)?.Value
            ?? user?.FindFirst("sub")?.Value;
        var ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        var entry = new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details?.Length > 2000 ? details[..2000] : details,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        };

        _context.AuditLogs.Add(entry);
        await _context.SaveChangesAsync();
    }
}
