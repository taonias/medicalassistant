export const doctorNotesKeys = {
  doctorNotes: (consultationId: number) => ['consultation', consultationId, 'doctor-notes'] as const,
  patientDoctorNotes: (patientId: number) => ['patient', patientId, 'doctor-notes'] as const,
};
