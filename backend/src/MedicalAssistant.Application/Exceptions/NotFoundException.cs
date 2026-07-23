namespace MedicalAssistant.Application.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string name, object key)
        : base($"{name} ({key}) was not found.")
    {
    }

    public NotFoundException(string message) : base(message)
    {
    }
}
