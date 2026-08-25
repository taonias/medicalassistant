using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

/// <summary>
/// The persistence capability behind the structured-data-extraction pipeline's
/// own writes to a consultation (R28): loading it by id and persisting it
/// after that pipeline's own lifecycle transition, as reported by a system
/// callback or notification rather than a doctor-scoped request. Sibling to
/// <see cref="ITranscriptionCompletion"/> — same shape, different pipeline; not
/// to be confused with it, or with <see cref="ITranscriptionCompletionUnitOfWork"/>.
/// See R28 for the rest.
/// </summary>
public interface IStructuredDataCompletion
{
    Task<Consultation> GetByIdAsync(int id);

    Task<Consultation> UpdateAsync(Consultation consultation);
}
