using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public class ConsultationRepository : GenericRepository<Consultation>, IConsultationRepository
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

    public override async Task<Consultation> GetByIdAsync(int id)
    {
        return await _context.Consultations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new Application.Exceptions.NotFoundException(nameof(Consultation), id);
    }
}
