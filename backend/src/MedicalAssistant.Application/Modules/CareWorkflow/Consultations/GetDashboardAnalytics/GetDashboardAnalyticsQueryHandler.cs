using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Domain.Enums;
using MediatR;

namespace MedicalAssistant.Application.Features.Consultation.Queries.GetDashboardAnalytics;

public class GetDashboardAnalyticsQueryHandler
    : IRequestHandler<GetDashboardAnalyticsQuery, DashboardAnalyticsDto>
{
    private static readonly ConsultationStatus[] ProcessingStatuses =
    [
        ConsultationStatus.AudioUploaded,
        ConsultationStatus.DocumentProcessingPending,
        ConsultationStatus.Transcribing,
        ConsultationStatus.Transcribed,
        ConsultationStatus.StructuredDataPending,
    ];

    private readonly IConsultationListing _consultationRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IUserService _userService;

    public GetDashboardAnalyticsQueryHandler(
        IConsultationListing consultationRepository,
        IPatientRepository patientRepository,
        IUserService userService)
    {
        _consultationRepository = consultationRepository;
        _patientRepository = patientRepository;
        _userService = userService;
    }

    public async Task<DashboardAnalyticsDto> Handle(
        GetDashboardAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var consultations = await _consultationRepository.GetConsultationsForDoctorAsync(doctorId);
        var patients = await _patientRepository.GetPatientListForDoctorAsync(doctorId);

        var patientsWithConsultations = patients.Count(p => p.ConsultationCount > 0);
        var statusGroups = consultations
            .GroupBy(c => c.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var statusBreakdown = Enum.GetValues<ConsultationStatus>()
            .Select(status => new StatusCountDto
            {
                Status = status.ToString(),
                Count = statusGroups.GetValueOrDefault(status),
            })
            .ToList();

        var today = DateTime.UtcNow.Date;
        var daily = Enumerable.Range(0, 14)
            .Select(offset => today.AddDays(-13 + offset))
            .Select(day => new DailyVolumeDto
            {
                Date = day.ToString("yyyy-MM-dd"),
                Count = consultations.Count(c => c.ConsultationDate.Date == day),
            })
            .ToList();

        var durations = consultations
            .Where(c => c.DurationSeconds is > 0)
            .Select(c => c.DurationSeconds!.Value)
            .ToList();

        return new DashboardAnalyticsDto
        {
            TotalPatients = patients.Count,
            PatientsWithConsultations = patientsWithConsultations,
            PatientsWithoutConsultations = patients.Count - patientsWithConsultations,
            TotalConsultations = consultations.Count,
            UnassignedRecordingCount = consultations.Count(c => c.PatientId == null),
            ProcessingCount = consultations.Count(c => ProcessingStatuses.Contains(c.Status)),
            CompletedCount = statusGroups.GetValueOrDefault(ConsultationStatus.Completed),
            FailedCount = statusGroups.GetValueOrDefault(ConsultationStatus.Failed),
            AverageDurationSeconds = durations.Count == 0 ? null : durations.Average(),
            StatusBreakdown = statusBreakdown,
            ConsultationsLast14Days = daily,
        };
    }
}
