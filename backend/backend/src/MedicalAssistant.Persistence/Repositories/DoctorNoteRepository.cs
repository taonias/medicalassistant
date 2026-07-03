using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;

namespace MedicalAssistant.Persistence.Repositories;

public class DoctorNoteRepository : GenericRepository<DoctorNote>, IDoctorNoteRepository
{
    public DoctorNoteRepository(MedicalAssistantDatabaseContext context, IHttpContextAccessor httpContextAccessor)
        : base(context, httpContextAccessor)
    {
    }

    public async Task<IReadOnlyList<DoctorNote>> GetByConsultationIdForDoctorAsync(int consultationId, string doctorId)
    {
        return await _context.DoctorNotes
            .AsNoTracking()
            .Where(n => n.ConsultationId == consultationId && n.DoctorId == doctorId)
            .OrderByDescending(n => n.Id)
            .ToListAsync();
    }
}
