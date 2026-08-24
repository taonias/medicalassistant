export interface DashboardStatusCount {
  status: string;
  count: number;
}

export interface DashboardDailyVolume {
  date: string;
  count: number;
}

export interface DashboardAnalytics {
  totalPatients: number;
  patientsWithConsultations: number;
  patientsWithoutConsultations: number;
  totalConsultations: number;
  unassignedRecordingCount: number;
  processingCount: number;
  completedCount: number;
  failedCount: number;
  averageDurationSeconds?: number;
  statusBreakdown: DashboardStatusCount[];
  consultationsLast14Days: DashboardDailyVolume[];
}
