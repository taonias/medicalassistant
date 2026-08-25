using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

/// <summary>
/// The persistence capability behind the transcription pipeline's own writes to
/// a consultation (R28): loading it by id and persisting it after a lifecycle
/// transition — transcribed, failed, or structured-data-pending — as reported
/// by a system callback rather than a doctor-scoped request. A narrow slice of
/// the broader <see cref="IConsultationRepository"/>, sibling to
/// <see cref="IConsultationDeletion"/> and <see cref="IConsultationFileRegistration"/>
/// — see R28 for the rest.
///
/// Not to be confused with <see cref="ITranscriptionCompletionUnitOfWork"/>: that
/// interface is the newer, event-bus-driven completion path with its own
/// idempotency handling; this one is the plain consultation read/write behind
/// the older <c>LegacyAiModule</c> HTTP callback, which is still live.
/// </summary>
public interface ITranscriptionCompletion
{
    Task<Consultation> GetByIdAsync(int id);

    Task<Consultation> UpdateAsync(Consultation consultation);
}
