using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Domain;
using MedicalAssistant.Persistence.DatabaseContext;

namespace MedicalAssistant.Persistence.Logging;

public class ErrorLogger : IErrorLogger
{
    private readonly MedicalAssistantDatabaseContext _context;

    public ErrorLogger(MedicalAssistantDatabaseContext context)
    {
        _context = context;
    }

    public async Task LogAsync(Exception exception, string path, string method)
    {
        var entry = new ErrorLog
        {
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            Path = path,
            Method = method,
            Timestamp = DateTime.UtcNow
        };

        _context.ErrorLogs.Add(entry);
        await _context.SaveChangesAsync();
    }
}
