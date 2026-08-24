import { useMemo } from 'react';
import { useLocation, useParams, useSearchParams } from 'react-router-dom';
import { useConsultation } from '../../modules/consultations';
import { usePatient, usePatientHistory, addDaysToDateInput, toDateInputValue } from '../../features/patients';
import { formatPatientName } from '../utils/format';

export type BreadcrumbItem = { label: string; href?: string };

function patientLabel(
  patient: { firstName: string; lastName: string } | undefined,
  patientId: number,
) {
  return patient ? formatPatientName(patient.firstName, patient.lastName) : `Patient ${patientId}`;
}

function parseSourceFilter(value: string | null): 'all' | 'audio' | 'pdf' {
  if (value === 'audio' || value === 'pdf') return value;
  return 'all';
}

export function useBreadcrumbItems(): BreadcrumbItem[] {
  const { pathname } = useLocation();
  const { patientId: patientIdParam, consultationId: consultationIdParam } = useParams();
  const [searchParams] = useSearchParams();

  const patientIdFromRoute = patientIdParam ? Number(patientIdParam) : 0;
  const consultationIdFromRoute = consultationIdParam ? Number(consultationIdParam) : 0;
  const consultation = useConsultation(consultationIdFromRoute);

  const patientIdForLookup =
    patientIdFromRoute > 0
      ? patientIdFromRoute
      : pathname === '/record'
        ? Number(searchParams.get('patientId') ?? '0')
        : (consultation.data?.patientId ?? 0);

  const patient = usePatient(patientIdForLookup);

  const onConsultationsTab =
    patientIdFromRoute > 0 && pathname === `/patients/${patientIdFromRoute}/history`;
  const today = toDateInputValue();
  const historyFromDate = searchParams.get('fromDate') ?? addDaysToDateInput(today, -6);
  const historyToDate = searchParams.get('toDate') ?? today;
  const historySource = parseSourceFilter(searchParams.get('source'));
  const consultationsHistory = usePatientHistory(onConsultationsTab ? patientIdFromRoute : 0, {
    fromDate: historyFromDate,
    toDate: historyToDate,
    page: 1,
    pageSize: 1,
    source: historySource,
  });
  const consultationCount =
    consultationsHistory.data?.totalConsultations ??
    consultationsHistory.data?.consultations?.length;

  return useMemo(() => {
    if (pathname === '/login') {
      return [{ label: 'Sign in' }];
    }

    if (pathname === '/') {
      return [{ label: 'Dashboard' }];
    }

    if (pathname === '/record') {
      const recordPatientId = Number(searchParams.get('patientId') ?? '0');
      if (recordPatientId > 0) {
        return [
          { label: 'Patients', href: '/patients' },
          {
            label: patientLabel(patient.data, recordPatientId),
            href: `/patients/${recordPatientId}`,
          },
          { label: 'Record' },
        ];
      }
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
        items.push({ label: String(consultationIdFromRoute) });
        return items;
      }

      if (pathname.endsWith('/chat')) {
        const linkedConsultationId = searchParams.get('consultation');
        if (linkedConsultationId) {
          items.push({ label: 'Chat', href: `/patients/${patientIdFromRoute}/chat` });
          items.push({ label: linkedConsultationId });
        } else {
          items.push({ label: 'Chat' });
        }
        return items;
      }

      const tabSegment = pathname
        .slice(`/patients/${patientIdFromRoute}`.length)
        .replace(/^\//, '');

      if (tabSegment === 'history') {
        items.push({
          label: consultationCount != null ? String(consultationCount) : 'Consultations',
        });
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

      items.push({ label: String(consultationIdFromRoute) });
      return items;
    }

    return [{ label: 'Dashboard', href: '/' }];
  }, [
    consultation.data,
    consultationCount,
    consultationIdFromRoute,
    patient.data,
    patientIdFromRoute,
    pathname,
    searchParams,
  ]);
}
