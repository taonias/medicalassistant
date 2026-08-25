using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

/// <summary>
/// The persistence capability behind proving a doctor may see a consultation
/// (R28): the doctor-scoped lookup, and nothing else. Shared by every use case
/// that only ever needed that proof — chat, doctor notes, structured-data and
/// transcript reads, the audio/document downloads, retry, and triggering an AI
/// action — none of which has any business depending on the other nine members
/// of the broader <see cref="IConsultationRepository"/>. Sibling to
/// <see cref="IConsultationDeletion"/>, <see cref="IConsultationFileRegistration"/>,
/// and <see cref="ITranscriptionCompletion"/> — see R28 for the rest.
/// </summary>
public interface IConsultationAccess
{
    Task<Consultation?> GetConsultationForDoctorAsync(int id, string doctorId);
}
