import { useMemo } from 'react';
import { consultationStatusLabel } from '../../../shared/utils/format';
import {
  BarChart,
  DoughnutChart,
  statusChartColor,
  useChartTheme,
} from '../../../shared/components/charts';
import type {
  DashboardAnalytics,
  DashboardDailyVolume,
  DashboardStatusCount,
} from '../../../shared/types/api';

function formatShortDay(dateIso: string) {
  const date = new Date(`${dateIso}T00:00:00`);
  return new Intl.DateTimeFormat(undefined, { weekday: 'short' }).format(date);
}

interface DonutProps {
  items: DashboardStatusCount[];
}

export function StatusDonutChart({ items }: DonutProps) {
  const theme = useChartTheme();

  const slices = useMemo(
    () =>
      items.map((item) => ({
        label: consultationStatusLabel(item.status),
        value: item.count,
        color: statusChartColor(item.status, theme),
      })),
    [items, theme],
  );

  const total = items.reduce((sum, item) => sum + item.count, 0);

  return (
    <DoughnutChart
      slices={slices}
      centerValue={total}
      centerLabel="consults"
      ariaLabel="Consultation status breakdown"
    />
  );
}

interface VolumeProps {
  items: DashboardDailyVolume[];
}

export function VolumeBarChart({ items }: VolumeProps) {
  const theme = useChartTheme();
  const labels = items.map((item) => formatShortDay(item.date));
  const values = items.map((item) => item.count);

  return (
    <BarChart
      labels={labels}
      series={[
        {
          label: 'Consultations',
          values,
          color: theme.primary,
        },
      ]}
      ariaLabel="Consultations over the last 14 days"
    />
  );
}

interface PatientMixProps {
  analytics: DashboardAnalytics;
}

export function PatientMixChart({ analytics }: PatientMixProps) {
  const theme = useChartTheme();

  return (
    <BarChart
      labels={['Patients']}
      series={[
        {
          label: 'With consultations',
          values: [analytics.patientsWithConsultations],
          color: theme.primary,
          stack: 'coverage',
        },
        {
          label: 'No consultations yet',
          values: [analytics.patientsWithoutConsultations],
          color: theme.palette[5],
          stack: 'coverage',
        },
      ]}
      horizontal
      stacked
      showLegend
      ariaLabel="Patient consultation coverage"
      className="chart-canvas--patient-mix"
    />
  );
}
