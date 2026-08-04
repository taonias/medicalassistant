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
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 50;

    private readonly IPatientRepository _patientRepository;
    private readonly IConsultationRepository _consultationRepository;
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly IMedicalStructuredDataRepository _structuredDataRepository;
    private readonly IDoctorNoteRepository _doctorNoteRepository;
    private readonly IMapper _mapper;

    public PatientHistoryAssembler(
        IPatientRepository patientRepository,
        IConsultationRepository consultationRepository,
        ITranscriptRepository transcriptRepository,
        IMedicalStructuredDataRepository structuredDataRepository,
        IDoctorNoteRepository doctorNoteRepository,
        IMapper mapper)
    {
        _patientRepository = patientRepository;
        _consultationRepository = consultationRepository;
        _transcriptRepository = transcriptRepository;
        _structuredDataRepository = structuredDataRepository;
        _doctorNoteRepository = doctorNoteRepository;
        _mapper = mapper;
    }

    public async Task<PatientHistoryDto> AssembleAsync(
        GetPatientHistoryQuery request,
        string doctorId,
        CancellationToken cancellationToken)
    {
        var patient = await _patientRepository.GetPatientForDoctorAsync(request.PatientId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Patient), request.PatientId);

        if (request.FromDate.HasValue && request.ToDate.HasValue)
        {
            var fromDay = request.FromDate.Value.Date;
            var toDay = request.ToDate.Value.Date;
            if (toDay < fromDay)
                throw new BadRequestException("To date must be on or after from date.");
            if (toDay > fromDay.AddMonths(1))
                throw new BadRequestException("History range cannot exceed one month.");
        }

        IReadOnlyList<Domain.Consultation> consultations;
        int totalCount;
        int page;
        int pageSize;

        // List views never include transcript snippets (detail page loads transcripts separately).
        var includeTranscripts = request.Page is > 0 ? false : request.IncludeTranscripts;

        if (request.Page is > 0)
        {
            page = request.Page.Value;
            pageSize = Math.Clamp(request.PageSize ?? DefaultPageSize, 1, MaxPageSize);
            var pageResult = await _consultationRepository.GetConsultationsByPatientForDoctorPageAsync(
                request.PatientId,
                doctorId,
                request.FromDate,
                request.ToDate,
                request.Source,
                page,
                pageSize);
            consultations = pageResult.Items;
            totalCount = pageResult.TotalCount;
        }
        else
        {
            page = 1;
            var all = await _consultationRepository.GetConsultationsByPatientForDoctorAsync(
                request.PatientId, doctorId);

            IEnumerable<Domain.Consultation> filtered = all;
            if (request.FromDate.HasValue)
            {
                var fromInclusive = request.FromDate.Value.Date;
                filtered = filtered.Where(c => c.ConsultationDate >= fromInclusive);
            }

            if (request.ToDate.HasValue)
            {
                var toExclusive = request.ToDate.Value.Date.AddDays(1);
                filtered = filtered.Where(c => c.ConsultationDate < toExclusive);
            }

            filtered = ApplySourceFilter(filtered, request.Source);
            consultations = filtered.OrderByDescending(c => c.ConsultationDate).ToList();
            totalCount = consultations.Count;
            pageSize = totalCount;
        }

        var items = new List<ConsultationHistoryItemDto>();

        foreach (var consultation in consultations)
        {
            var hasAudio = !string.IsNullOrEmpty(consultation.AudioBlobUri);
            var hasDocument = !string.IsNullOrEmpty(consultation.DocumentBlobUri);
            var item = new ConsultationHistoryItemDto
            {
                Id = consultation.Id,
                ConsultationDate = consultation.ConsultationDate,
                Status = consultation.Status.ToString(),
                DurationSeconds = consultation.DurationSeconds,
                HasAudio = hasAudio,
                HasDocument = hasDocument,
                Source = ResolveSource(hasAudio, hasDocument, consultation.Status),
            };

            if (includeTranscripts)
            {
                var transcript = await _transcriptRepository.GetByConsultationIdAsync(consultation.Id);
                if (transcript?.Status == TranscriptStatus.Completed && !string.IsNullOrEmpty(transcript.TranscriptText))
                {
                    item.TranscriptSnippet = transcript.TranscriptText.Length > 200
                        ? transcript.TranscriptText[..200] + "..."
                        : transcript.TranscriptText;
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

        var doctorNotes = await _doctorNoteRepository.GetPatientLevelNotesForDoctorAsync(request.PatientId, doctorId);

        return new PatientHistoryDto
        {
            Patient = _mapper.Map<PatientDto>(patient),
            Consultations = items,
            TotalConsultations = totalCount,
            Page = page,
            PageSize = pageSize,
            DoctorNotes = doctorNotes
                .Select(note => new PatientDoctorNoteDto
                {
                    Id = note.Id,
                    PatientId = note.PatientId,
                    ConsultationId = note.ConsultationId,
                    Content = note.Content,
                    DateCreated = note.DateCreated,
                    DateModified = note.DateModified,
                })
                .ToList(),
        };
    }

    private static IEnumerable<Domain.Consultation> ApplySourceFilter(
        IEnumerable<Domain.Consultation> consultations,
        string? source)
    {
        var normalized = source?.Trim().ToLowerInvariant();
        if (normalized == "audio")
            return consultations.Where(c => !string.IsNullOrEmpty(c.AudioBlobUri));
        if (normalized == "pdf")
            return consultations.Where(c =>
                !string.IsNullOrEmpty(c.DocumentBlobUri) && string.IsNullOrEmpty(c.AudioBlobUri));
        return consultations;
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

    private static string ResolveSource(bool hasAudio, bool hasDocument, ConsultationStatus status)
    {
        if (hasDocument && !hasAudio)
            return "Pdf";
        if (hasAudio)
            return "Audio";
        if (status is ConsultationStatus.DocumentUploaded or ConsultationStatus.DocumentProcessingPending)
            return "Pdf";
        if (status == ConsultationStatus.AudioUploaded)
            return "Audio";
        return "Unknown";
    }
}
