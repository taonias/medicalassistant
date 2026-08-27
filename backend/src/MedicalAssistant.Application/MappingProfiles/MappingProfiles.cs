using AutoMapper;
using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.MappingProfiles;

public class PatientProfile : Profile
{
    public PatientProfile()
    {
        CreateMap<Patient, Modules.CareWorkflow.Patients.GetPatientById.PatientDto>().ReverseMap();
        CreateMap<Modules.CareWorkflow.Patients.CreatePatient.CreatePatientCommand, Patient>();
        CreateMap<Modules.CareWorkflow.Patients.UpdatePatient.UpdatePatientCommand, Patient>();
    }
}

public class ConsultationProfile : Profile
{
    public ConsultationProfile()
    {
        CreateMap<Consultation, Modules.CareWorkflow.Consultations.GetConsultationDetails.ConsultationDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
        CreateMap<Consultation, Modules.CareWorkflow.Consultations.GetConsultationsByPatient.ConsultationSummaryDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.HasAudio, o => o.MapFrom(s => !string.IsNullOrEmpty(s.AudioBlobUri)));
        CreateMap<Modules.CareWorkflow.Consultations.CreateConsultation.CreateConsultationCommand, Consultation>();
    }
}

public class TranscriptProfile : Profile
{
    public TranscriptProfile()
    {
        CreateMap<Transcript, Modules.CareWorkflow.Transcripts.GetTranscript.TranscriptDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Transcript, o => o.MapFrom(s => s.TranscriptText));
    }
}

public class MedicalStructuredDataProfile : Profile
{
    public MedicalStructuredDataProfile()
    {
        CreateMap<MedicalStructuredData, Modules.CareWorkflow.StructuredMedicalData.GetStructuredData.MedicalStructuredDataDto>();
    }
}

public class ActionRequestProfile : Profile
{
    public ActionRequestProfile()
    {
        CreateMap<ActionRequest, Modules.Integrations.LegacyAiModule.GetActionRequestStatus.ActionRequestDto>();
    }
}
