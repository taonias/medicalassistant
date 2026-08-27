using MedicalAssistant.Persistence.Repositories;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Modules.CareWorkflow.Consultations;

public class ConsultationRepository :
    GenericRepository<Consultation>, IConsultationRepository, IConsultationDeletion,
    IConsultationFileRegistration, ITranscriptionCompletion, IConsultationAccess,
    IConsultationListing, IConsultationCreation, IConsultationPatientAssignment,
    IConsultationStructuredDataApproval, IStructuredDataCompletion
{
    public ConsultationRepository(MedicalAssistantDatabaseContext context, IHttpContextAccessor httpContextAccessor)
        : base(context, httpContextAccessor)
    {
    }

    public async Task<Consultation?> GetConsultationForDoctorAsync(int id, string doctorId)
    {
        return await _context.Consultations
            .FirstOrDefaultAsync(c => c.Id == id && c.DoctorId == doctorId);
    }

    public async Task<IReadOnlyList<Consultation>> GetConsultationsByPatientForDoctorAsync(int patientId, string doctorId)
    {
        return await _context.Consultations
            .AsNoTracking()
            .Where(c => c.PatientId == patientId && c.DoctorId == doctorId)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<Consultation> Items, int TotalCount)> GetConsultationsByPatientForDoctorPageAsync(
        int patientId,
        string doctorId,
        DateTime? fromDate,
        DateTime? toDate,
        string? source,
        int page,
        int pageSize)
    {
        var query = _context.Consultations
            .AsNoTracking()
            .Where(c => c.PatientId == patientId && c.DoctorId == doctorId);

        if (fromDate.HasValue)
        {
            // Npgsql rejects Unspecified DateTime for timestamptz parameters.
            var fromInclusive = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(c => c.ConsultationDate >= fromInclusive);
        }

        if (toDate.HasValue)
        {
            var toExclusive = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(c => c.ConsultationDate < toExclusive);
        }

        var normalizedSource = source?.Trim().ToLowerInvariant();
        if (normalizedSource == "audio")
        {
            query = query.Where(c => c.AudioBlobUri != null && c.AudioBlobUri != string.Empty);
        }
        else if (normalizedSource == "pdf")
        {
            query = query.Where(c =>
                c.DocumentBlobUri != null &&
                c.DocumentBlobUri != string.Empty &&
                (c.AudioBlobUri == null || c.AudioBlobUri == string.Empty));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.ConsultationDate)
            .Skip(Math.Max(0, (page - 1) * pageSize))
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<DraftConsultationListItem>> GetDraftConsultationsForDoctorAsync(string doctorId)
    {
        return await _context.Consultations
            .AsNoTracking()
            .Where(c => c.DoctorId == doctorId && c.Status == ConsultationStatus.Draft && c.PatientId != null)
            .Join(
                _context.Patients.AsNoTracking(),
                consultation => consultation.PatientId!.Value,
                patient => patient.Id,
                (consultation, patient) => new { consultation, patient })
            .Where(x => x.patient.AssignedDoctorId == doctorId)
            .OrderBy(x => x.patient.LastName)
            .ThenBy(x => x.patient.FirstName)
            .ThenByDescending(x => x.consultation.ConsultationDate)
            .Select(x => new DraftConsultationListItem
            {
                PatientId = x.patient.Id,
                PatientFirstName = x.patient.FirstName,
                PatientLastName = x.patient.LastName,
                ConsultationId = x.consultation.Id,
                ConsultationDate = x.consultation.ConsultationDate,
                DurationSeconds = x.consultation.DurationSeconds,
                HasAudio = x.consultation.AudioBlobUri != null,
            })
            .ToListAsync();
    }

    public async Task<Consultation?> GetByIdempotencyKeyAsync(string idempotencyKey, string doctorId)
    {
        return await _context.Consultations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdempotencyKey == idempotencyKey && c.DoctorId == doctorId);
    }

    public async Task<bool> IdempotencyKeyExistsAsync(string idempotencyKey, string doctorId)
    {
        return await _context.Consultations
            .AnyAsync(c => c.IdempotencyKey == idempotencyKey && c.DoctorId == doctorId);
    }

    public async Task<Consultation> UpdateWithOutboxAsync(
        Consultation consultation,
        ConsultationOutboxMessage outboxMessage,
        CancellationToken cancellationToken = default)
    {
        _context.Entry(consultation).State = EntityState.Modified;
        await _context.ConsultationOutboxMessages.AddAsync(outboxMessage, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return consultation;
    }

    public async Task<Consultation> RecordDeletionAsync(
        Consultation consultation,
        ConsultationDeletionCleanup cleanup,
        ConsultationOutboxMessage outboxMessage,
        CancellationToken cancellationToken = default)
    {
        _context.Entry(consultation).State = EntityState.Modified;
        await _context.ConsultationDeletionCleanups.AddAsync(cleanup, cancellationToken);
        await _context.ConsultationOutboxMessages.AddAsync(outboxMessage, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return consultation;
    }

    public override async Task<Consultation> GetByIdAsync(int id)
    {
        return await _context.Consultations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new Application.Exceptions.NotFoundException(nameof(Consultation), id);
    }

    public async Task<IReadOnlyList<Consultation>> GetUnattachedDraftConsultationsForDoctorAsync(string doctorId)
    {
        return await _context.Consultations
            .AsNoTracking()
            .Where(c =>
                c.DoctorId == doctorId &&
                c.PatientId == null)
            .OrderByDescending(c => c.ConsultationDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Consultation>> GetConsultationsForDoctorAsync(string doctorId)
    {
        return await _context.Consultations
            .AsNoTracking()
            .Where(c => c.DoctorId == doctorId)
            .OrderByDescending(c => c.ConsultationDate)
            .ToListAsync();
    }

    public async Task DeleteForDoctorAsync(Consultation consultation)
    {
        var consultationId = consultation.Id;

        var structuredData = await _context.MedicalStructuredData
            .Where(m => m.ConsultationId == consultationId)
            .ToListAsync();
        _context.MedicalStructuredData.RemoveRange(structuredData);

        var transcripts = await _context.Transcripts
            .Where(t => t.ConsultationId == consultationId)
            .ToListAsync();
        _context.Transcripts.RemoveRange(transcripts);

        var notes = await _context.DoctorNotes
            .Where(n => n.ConsultationId == consultationId)
            .ToListAsync();
        foreach (var note in notes)
        {
            note.ConsultationId = null;
        }

        var actions = await _context.ActionRequests
            .Where(a => a.ConsultationId == consultationId)
            .ToListAsync();
        foreach (var action in actions)
        {
            action.ConsultationId = null;
        }

        _context.Consultations.Remove(consultation);
        await _context.SaveChangesAsync();
    }
}
