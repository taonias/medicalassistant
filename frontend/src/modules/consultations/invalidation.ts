import type { QueryClient } from '@tanstack/react-query';
import { consultationKeys } from './queryKeys';
import { patientKeys } from '../../features/patients';
import { transcriptKeys, structuredDataKeys } from '../clinical-record';

/**
 * Owned consultation cache-invalidation policy (R30): the repeated
 * combinations every consultation mutation used to invalidate inline. Each
 * function names one coherent combination — callers still choose which
 * combinations apply to their own mutation, so nothing invalidates more than
 * it did before this was extracted.
 */

/** The consultation-list views every "a consultation was created or changed status" mutation affects. */
export function invalidateConsultationLists(queryClient: QueryClient): void {
  queryClient.invalidateQueries({ queryKey: consultationKeys.draftConsultations });
  queryClient.invalidateQueries({ queryKey: consultationKeys.unattachedDraftConsultations });
  queryClient.invalidateQueries({ queryKey: consultationKeys.dashboardAnalytics });
}

/** The patient-scoped views of one consultation's patient. */
export function invalidatePatientConsultationViews(queryClient: QueryClient, patientId: number): void {
  queryClient.invalidateQueries({ queryKey: consultationKeys.consultationsByPatient(patientId) });
  queryClient.invalidateQueries({ queryKey: patientKeys.patientHistoryPrefix(patientId) });
}

/** A single consultation's derived clinical-record views (transcript, structured data). */
export function invalidateConsultationClinicalRecord(queryClient: QueryClient, consultationId: number): void {
  queryClient.invalidateQueries({ queryKey: transcriptKeys.transcript(consultationId) });
  queryClient.invalidateQueries({ queryKey: structuredDataKeys.structuredData(consultationId) });
}
