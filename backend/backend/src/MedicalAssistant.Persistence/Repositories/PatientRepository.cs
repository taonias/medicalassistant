using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Models.Patients;
using MedicalAssistant.Domain;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public class PatientRepository : GenericRepository<Patient>, IPatientRepository
{
    public PatientRepository(MedicalAssistantDatabaseContext context, IHttpContextAccessor httpContextAccessor)
        : base(context, httpContextAccessor)
    {
    }

    public async Task<IReadOnlyList<Patient>> GetPatientsForDoctorAsync(string doctorId)
    {
        return await _context.Patients
            .AsNoTracking()
            .Where(p => p.AssignedDoctorId == doctorId)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<PatientListItem>> GetPatientListForDoctorAsync(string doctorId)
    {
        return await _context.Patients
            .AsNoTracking()
            .Where(p => p.AssignedDoctorId == doctorId)
            .Select(p => new PatientListItem
            {
                Id = p.Id,
                FirstName = p.FirstName,
                LastName = p.LastName,
                DateOfBirth = p.DateOfBirth,
                DateCreated = p.DateCreated,
                ConsultationCount = _context.Consultations.Count(c => c.PatientId == p.Id && c.DoctorId == doctorId),
                LastConsultationDate = _context.Consultations
                    .Where(c => c.PatientId == p.Id && c.DoctorId == doctorId)
                    .Max(c => (DateTime?)c.ConsultationDate),
            })
            .OrderByDescending(p => p.DateCreated)
            .ThenByDescending(p => p.Id)
            .ToListAsync();
    }

    public async Task<Patient?> GetPatientForDoctorAsync(int id, string doctorId)
    {
        return await _context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.AssignedDoctorId == doctorId);
    }

    public async Task<bool> IsExternalPatientIdUniqueAsync(string externalPatientId, string doctorId, int? excludeId = null)
    {
        var query = _context.Patients
            .Where(p => p.AssignedDoctorId == doctorId && p.ExternalPatientId == externalPatientId);

        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);

        return !await query.AnyAsync();
    }
}
