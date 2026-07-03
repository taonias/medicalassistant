namespace MedicalAssistant.Api.Models;

public class CustomProblemDetails : Microsoft.AspNetCore.Mvc.ProblemDetails
{
    public IDictionary<string, string[]>? Errors { get; set; }
}
