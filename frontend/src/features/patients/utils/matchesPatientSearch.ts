import type { PatientListItem } from '../types';
import { formatPatientName } from '../../../shared/utils/format';

export function matchesPatientSearch(patient: PatientListItem, query: string) {
  const normalized = query.trim().toLowerCase();
  if (!normalized) return true;

  const fullName = formatPatientName(patient.firstName, patient.lastName).toLowerCase();
  return (
    fullName.includes(normalized) ||
    String(patient.id).includes(normalized) ||
    (patient.dateOfBirth?.includes(normalized) ?? false)
  );
}
