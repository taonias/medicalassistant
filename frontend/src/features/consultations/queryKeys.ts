export const consultationKeys = {
  consultationsByPatient: (patientId: number) => ['patient', patientId, 'consultations'] as const,
  draftConsultations: ['consultations', 'drafts'] as const,
  unattachedDraftConsultations: ['consultations', 'drafts', 'unattached'] as const,
  dashboardAnalytics: ['consultations', 'analytics'] as const,
  consultation: (id: number) => ['consultation', id] as const,
  consultationAudio: (id: number) => ['consultation', id, 'audio'] as const,
};
