namespace MedicalAssistant.Application.Contracts.Logging;

public interface IErrorLogger
{
    Task LogAsync(Exception exception, string path, string method);
}
