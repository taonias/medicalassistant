using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

/// <summary>
/// The persistence capability behind creating a new draft consultation (R28):
/// the idempotency-key lookup that lets a retried request return the original
/// consultation instead of creating a duplicate, and the durable write that
/// creates it. See R28 for the rest of the sibling ports.
/// </summary>
public interface IConsultationCreation
{
    Task<Consultation?> GetByIdempotencyKeyAsync(string idempotencyKey, string doctorId);

    Task<Consultation> CreateAsync(Consultation consultation);
}
