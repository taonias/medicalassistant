import { useMemo } from 'react';
import { useLocation, useParams, useSearchParams } from 'react-router-dom';
import { usePatient } from '../../features/patients/hooks/usePatients';
import { formatPatientName } from '../utils/format';

/**
 * Resolves patient context from /patients/:patientId routes or ?patientId= on /record.
 */
export function useActivePatientContext() {
  const { pathname } = useLocation();
  const { patientId: patientIdParam } = useParams();
  const [searchParams] = useSearchParams();

  const patientIdFromParams = patientIdParam ? Number(patientIdParam) : 0;
  const patientIdFromQuery = Number(searchParams.get('patientId') ?? '0');

  const patientIdFromPath = useMemo(() => {
    const match = pathname.match(/^\/patients\/(\d+)(?:\/|$)/);
    if (!match) return 0;
    return Number(match[1]);
  }, [pathname]);

  const patientId =
    patientIdFromParams > 0
      ? patientIdFromParams
      : patientIdFromPath > 0
        ? patientIdFromPath
        : pathname === '/record' && patientIdFromQuery > 0
          ? patientIdFromQuery
          : 0;

  const patientQuery = usePatient(patientId);

  const patientName = patientQuery.data
    ? formatPatientName(patientQuery.data.firstName, patientQuery.data.lastName)
    : patientId > 0
      ? `Patient ${patientId}`
      : null;

  return {
    patientId: patientId > 0 ? patientId : null,
    patientName,
    patient: patientQuery.data,
    isLoading: patientId > 0 && patientQuery.isLoading,
  };
}
