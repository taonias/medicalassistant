using AutoMapper;
using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.MappingProfiles;

public class PatientProfile : Profile
{
    public PatientProfile()
    {
        CreateMap<Patient, Features.Patient.Queries.GetPatientById.PatientDto>().ReverseMap();
        CreateMap<Features.Patient.Command.CreatePatient.CreatePatientCommand, Patient>();
        CreateMap<Features.Patient.Command.UpdatePatient.UpdatePatientCommand, Patient>();
    }
}

public class ConsultationProfile : Profile
{
    public ConsultationProfile()
    {
        CreateMap<Consultation, Features.Consultation.Queries.GetConsultationDetails.ConsultationDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
        CreateMap<Consultation, Features.Consultation.Queries.GetConsultationsByPatient.ConsultationSummaryDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.HasAudio, o => o.MapFrom(s => !string.IsNullOrEmpty(s.AudioBlobUri)));
        CreateMap<Features.Consultation.Command.CreateConsultation.CreateConsultationCommand, Consultation>();
    }
}

public class TranscriptProfile : Profile
{
    public TranscriptProfile()
    {
        CreateMap<Transcript, Features.Transcript.Queries.GetTranscript.TranscriptDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Transcript, o => o.MapFrom(s => s.TranscriptText));
    }
}

public class MedicalStructuredDataProfile : Profile
{
    public MedicalStructuredDataProfile()
    {
        CreateMap<MedicalStructuredData, Features.MedicalStructuredData.Queries.GetStructuredData.MedicalStructuredDataDto>();
    }
}

public class ActionRequestProfile : Profile
{
    public ActionRequestProfile()
    {
        CreateMap<ActionRequest, Features.ActionRequest.Queries.GetActionRequestStatus.ActionRequestDto>();
    }
}
