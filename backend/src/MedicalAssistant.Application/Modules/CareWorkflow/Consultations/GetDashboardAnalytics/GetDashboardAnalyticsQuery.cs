using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetDashboardAnalytics;

public record GetDashboardAnalyticsQuery : IRequest<DashboardAnalyticsDto>;

public class DashboardAnalyticsDto
{
    public int TotalPatients { get; set; }
    public int PatientsWithConsultations { get; set; }
    public int PatientsWithoutConsultations { get; set; }
    public int TotalConsultations { get; set; }
    public int UnassignedRecordingCount { get; set; }
    public int ProcessingCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public double? AverageDurationSeconds { get; set; }
    public List<StatusCountDto> StatusBreakdown { get; set; } = [];
    public List<DailyVolumeDto> ConsultationsLast14Days { get; set; } = [];
}

public class StatusCountDto
{
    public required string Status { get; set; }
    public int Count { get; set; }
}

public class DailyVolumeDto
{
    public required string Date { get; set; }
    public int Count { get; set; }
}
