using MedicalAssistant.Application.Contracts.Documents;
using MedicalAssistant.Application.Exceptions;
using UglyToad.PdfPig;

namespace MedicalAssistant.Infrastructure.Documents;

public class PdfPigTextExtractor : IPdfTextExtractor
{
    public Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var document = PdfDocument.Open(pdfStream);
        var pages = new List<string>();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = page.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
                pages.Add(text);
        }

        var combined = string.Join("\n\n", pages).Trim();
        if (string.IsNullOrWhiteSpace(combined))
            throw new BadRequestException("Could not extract any text from the PDF.");

        return Task.FromResult(combined);
    }
}
