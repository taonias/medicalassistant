using AutoMapper;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Features.Patient.Queries.GetPatientById;
using MedicalAssistant.Application.Features.Patient.Queries.GetPatientHistory;
using MedicalAssistant.Domain.Enums;
using System.Text.Json;

namespace MedicalAssistant.Application.Services;

public class PatientHistoryAssembler
{
    private readonly IPatientRepository _patientRepository;
    private readonly IConsultationRepository _consultationRepository;
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly IMedicalStructuredDataRepository _structuredDataRepository;
    private readonly IMapper _mapper;

    public PatientHistoryAssembler(
        IPatientRepository patientRepository,
        IConsultationRepository consultationRepository,
        ITranscriptRepository transcriptRepository,
        IMedicalStructuredDataRepository structuredDataRepository,
        IMapper mapper)
    {
        _patientRepository = patientRepository;
        _consultationRepository = consultationRepository;
        _transcriptRepository = transcriptRepository;
        _structuredDataRepository = structuredDataRepository;
        _mapper = mapper;
    }

    public async Task<PatientHistoryDto> AssembleAsync(
        GetPatientHistoryQuery request,
        string doctorId,
        CancellationToken cancellationToken)
    {
        var patient = await _patientRepository.GetPatientForDoctorAsync(request.PatientId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Patient), request.PatientId);

        var consultations = await _consultationRepository.GetConsultationsByPatientForDoctorAsync(request.PatientId, doctorId);

        if (request.FromDate.HasValue)
            consultations = consultations.Where(c => c.ConsultationDate >= request.FromDate.Value).ToList();
        if (request.ToDate.HasValue)
            consultations = consultations.Where(c => c.ConsultationDate <= request.ToDate.Value).ToList();

        var items = new List<ConsultationHistoryItemDto>();

        foreach (var consultation in consultations.OrderByDescending(c => c.ConsultationDate))
        {
            var item = new ConsultationHistoryItemDto
            {
                Id = consultation.Id,
                ConsultationDate = consultation.ConsultationDate,
                Status = consultation.Status.ToString()
            };

            if (request.IncludeTranscripts)
            {
                var transcript = await _transcriptRepository.GetByConsultationIdAsync(consultation.Id);
                if (transcript?.Status == TranscriptStatus.Completed && !string.IsNullOrEmpty(transcript.RawText))
                {
                    item.TranscriptSnippet = transcript.RawText.Length > 200
                        ? transcript.RawText[..200] + "..."
                        : transcript.RawText;
                }
            }

            if (request.IncludeStructuredData)
            {
                var structured = await _structuredDataRepository.GetLatestByConsultationIdAsync(consultation.Id);
                if (structured != null)
                {
                    item.StructuredSummary = ExtractSummary(structured.StructuredPayload);
                }
            }

            items.Add(item);
        }

        return new PatientHistoryDto
        {
            Patient = _mapper.Map<PatientDto>(patient),
            Consultations = items
        };
    }

    private static string? ExtractSummary(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("summary", out var summary))
                return summary.GetString();
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
