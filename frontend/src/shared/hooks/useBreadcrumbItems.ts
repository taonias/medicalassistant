import { useMemo } from 'react';
import { useLocation, useParams, useSearchParams } from 'react-router-dom';
import { useConsultation } from '../../features/consultations/hooks/useConsultations';
import { usePatient } from '../../features/patients/hooks/usePatients';
import { formatPatientName } from '../utils/format';

export type BreadcrumbItem = { label: string; href?: string };

const PATIENT_TAB_LABELS: Record<string, string> = {
  history: 'History',
  'structured-data': 'Structured data',
};

function patientLabel(
  patient: { firstName: string; lastName: string } | undefined,
  patientId: number,
) {
  return patient ? formatPatientName(patient.firstName, patient.lastName) : `Patient ${patientId}`;
}

export function useBreadcrumbItems(): BreadcrumbItem[] {
  const { pathname } = useLocation();
  const { patientId: patientIdParam, consultationId: consultationIdParam } = useParams();
  const [searchParams] = useSearchParams();

  const patientIdFromRoute = patientIdParam ? Number(patientIdParam) : 0;
  const consultationIdFromRoute = consultationIdParam ? Number(consultationIdParam) : 0;
  const consultation = useConsultation(consultationIdFromRoute);

  const patientIdForLookup =
    patientIdFromRoute > 0 ? patientIdFromRoute : (consultation.data?.patientId ?? 0);

  const patient = usePatient(patientIdForLookup);

  return useMemo(() => {
    if (pathname === '/login') {
      return [{ label: 'Sign in' }];
    }

    if (pathname === '/') {
      return [{ label: 'Dashboard' }];
    }

    if (pathname === '/record') {
      return [{ label: 'Dashboard', href: '/' }, { label: 'Record' }];
    }

    if (pathname === '/chat') {
      return [{ label: 'Chat' }];
    }

    if (pathname === '/settings') {
      return [{ label: 'Settings' }];
    }

    if (pathname === '/patients') {
      return [{ label: 'Patients' }];
    }

    if (patientIdFromRoute > 0 && pathname.startsWith(`/patients/${patientIdFromRoute}`)) {
      const items: BreadcrumbItem[] = [
        { label: 'Patients', href: '/patients' },
        {
          label: patientLabel(patient.data, patientIdFromRoute),
          href: `/patients/${patientIdFromRoute}`,
        },
      ];

      if (pathname.endsWith('/consultations/new')) {
        items.push({ label: 'New consultation' });
        return items;
      }

      if (consultationIdFromRoute > 0 && pathname.includes('/consultations/')) {
        items.push({ label: `Consultation ${consultationIdFromRoute}` });
        return items;
      }

      if (pathname.endsWith('/chat')) {
        const linkedConsultationId = searchParams.get('consultation');
        if (linkedConsultationId) {
          items.push({ label: 'Chat', href: `/patients/${patientIdFromRoute}/chat` });
          items.push({ label: `Consultation ${linkedConsultationId}` });
        } else {
          items.push({ label: 'Chat' });
        }
        return items;
      }

      const tabSegment = pathname
        .slice(`/patients/${patientIdFromRoute}`.length)
        .replace(/^\//, '');

      if (tabSegment === 'history' || tabSegment === 'structured-data') {
        items.push({ label: PATIENT_TAB_LABELS[tabSegment] });
      } else {
        items.push({ label: 'Overview' });
      }

      return items;
    }

    if (pathname.startsWith('/consultations/') && consultationIdFromRoute > 0) {
      const items: BreadcrumbItem[] = [];
      const linkedPatientId = consultation.data?.patientId;

      if (linkedPatientId) {
        items.push({ label: 'Patients', href: '/patients' });
        items.push({
          label: patientLabel(patient.data, linkedPatientId),
          href: `/patients/${linkedPatientId}`,
        });
      } else {
        items.push({ label: 'Record', href: '/record' });
      }

      items.push({ label: `Consultation ${consultationIdFromRoute}` });
      return items;
    }

    return [{ label: 'Dashboard', href: '/' }];
  }, [
    consultation.data,
    consultationIdFromRoute,
    patient.data,
    patientIdFromRoute,
    pathname,
    searchParams,
  ]);
}
