export const queryKeys = {
  session: ["auth", "session"] as const,
  patient: (id: number) => ["patient", id] as const,
  patients: ["patients"] as const,
  patientHistory: (id: number) => ["patient", id, "history"] as const,
  consultationsByPatient: (patientId: number) => ["patient", patientId, "consultations"] as const,
  draftConsultations: ["consultations", "drafts"] as const,
  consultation: (id: number) => ["consultation", id] as const,
  transcript: (consultationId: number) => ["consultation", consultationId, "transcript"] as const,
  structuredData: (consultationId: number) => ["consultation", consultationId, "structured-data"] as const,
  doctorNotes: (consultationId: number) => ["consultation", consultationId, "doctor-notes"] as const,
  action: (correlationId: string) => ["action", correlationId] as const,
};
