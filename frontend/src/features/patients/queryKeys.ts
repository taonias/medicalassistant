export const patientKeys = {
  patient: (id: number) => ['patient', id] as const,
  patients: ['patients'] as const,
  patientHistory: (
    id: number,
    fromDate?: string,
    toDate?: string,
    page?: number,
    pageSize?: number,
    source?: string,
  ) =>
    [
      'patient',
      id,
      'history',
      fromDate ?? null,
      toDate ?? null,
      page ?? null,
      pageSize ?? null,
      source ?? null,
    ] as const,
  patientHistoryPrefix: (id: number) => ['patient', id, 'history'] as const,
};
