import { useMemo, useState } from 'react';
import type { ConsultationHistoryItem } from '../types';
import { BarChart, DoughnutChart, useChartTheme } from '../../../shared/components/charts';
import {
  addDaysToDateInput,
  parseDateInput,
  toDateInputValue,
} from '../utils/historyDateRange';

type ActivityPeriod = 'currentMonth' | 'last30Days' | 'lastMonth' | 'lastYear';

const PERIOD_OPTIONS: { value: ActivityPeriod; label: string }[] = [
  { value: 'currentMonth', label: 'Current month' },
  { value: 'last30Days', label: 'Last 30 days' },
  { value: 'lastMonth', label: 'Last month' },
  { value: 'lastYear', label: 'Last year' },
];

interface Props {
  consultations: ConsultationHistoryItem[];
}

function dayKey(iso: string) {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso.slice(0, 10);
  return toDateInputValue(date);
}

function formatShortDay(value: string) {
  const date = new Date(`${value}T00:00:00`);
  return new Intl.DateTimeFormat(undefined, { weekday: 'short', day: 'numeric' }).format(date);
}

function formatMonthLabel(year: number, monthIndex: number) {
  return new Intl.DateTimeFormat(undefined, { month: 'short' }).format(
    new Date(year, monthIndex, 1),
  );
}

function resolvePeriodRange(period: ActivityPeriod, today = toDateInputValue()) {
  const todayDate = parseDateInput(today);

  if (period === 'currentMonth') {
    const from = toDateInputValue(new Date(todayDate.getFullYear(), todayDate.getMonth(), 1));
    return { from, to: today, mode: 'day' as const };
  }

  if (period === 'last30Days') {
    return {
      from: addDaysToDateInput(today, -29),
      to: today,
      mode: 'day' as const,
    };
  }

  if (period === 'lastMonth') {
    const firstOfThisMonth = new Date(todayDate.getFullYear(), todayDate.getMonth(), 1);
    const lastOfPrev = new Date(firstOfThisMonth.getTime() - 24 * 60 * 60 * 1000);
    const from = toDateInputValue(new Date(lastOfPrev.getFullYear(), lastOfPrev.getMonth(), 1));
    const to = toDateInputValue(lastOfPrev);
    return { from, to, mode: 'day' as const };
  }

  // Trailing 12 months through today, aggregated by month.
  const from = toDateInputValue(
    new Date(todayDate.getFullYear() - 1, todayDate.getMonth(), todayDate.getDate()),
  );
  return { from, to: today, mode: 'month' as const };
}

function consultationKind(item: ConsultationHistoryItem): 'audio' | 'pdf' | 'other' {
  const source = (item.source ?? '').toLowerCase();
  if (source === 'pdf') return 'pdf';
  if (source === 'audio') return 'audio';
  if (item.hasDocument && !item.hasAudio) return 'pdf';
  if (item.hasAudio) return 'audio';
  const status = item.status.replace(/\s+/g, '').toLowerCase();
  if (status === 'documentuploaded') return 'pdf';
  if (status === 'audiouploaded') return 'audio';
  return 'other';
}

function eachDay(from: string, to: string) {
  const days: string[] = [];
  let cursor = from;
  while (cursor <= to) {
    days.push(cursor);
    cursor = addDaysToDateInput(cursor, 1);
  }
  return days;
}

function eachMonth(from: string, to: string) {
  const start = parseDateInput(from);
  const end = parseDateInput(to);
  const months: { key: string; label: string }[] = [];
  const cursor = new Date(start.getFullYear(), start.getMonth(), 1);
  const endMonth = new Date(end.getFullYear(), end.getMonth(), 1);

  while (cursor <= endMonth) {
    const year = cursor.getFullYear();
    const monthIndex = cursor.getMonth();
    const key = `${year}-${String(monthIndex + 1).padStart(2, '0')}`;
    months.push({ key, label: formatMonthLabel(year, monthIndex) });
    cursor.setMonth(cursor.getMonth() + 1);
  }

  return months;
}

export function PatientOverviewCharts({ consultations }: Props) {
  const theme = useChartTheme();
  const [period, setPeriod] = useState<ActivityPeriod>('currentMonth');

  const range = useMemo(() => resolvePeriodRange(period), [period]);

  const scopedConsultations = useMemo(
    () =>
      consultations.filter((item) => {
        const key = dayKey(item.consultationDate);
        return key >= range.from && key <= range.to;
      }),
    [consultations, range.from, range.to],
  );

  const frequency = useMemo(() => {
    if (range.mode === 'month') {
      const months = eachMonth(range.from, range.to);
      const values = months.map(() => 0);
      const indexByMonth = new Map(months.map((month, index) => [month.key, index]));

      for (const item of scopedConsultations) {
        const key = dayKey(item.consultationDate).slice(0, 7);
        const index = indexByMonth.get(key);
        if (index != null) values[index] += 1;
      }

      return {
        labels: months.map((month) => month.label),
        values,
        subtitle: 'Monthly consultation frequency',
      };
    }

    const days = eachDay(range.from, range.to);
    const values = days.map(() => 0);
    const indexByDay = new Map(days.map((day, index) => [day, index]));

    for (const item of scopedConsultations) {
      const key = dayKey(item.consultationDate);
      const index = indexByDay.get(key);
      if (index != null) values[index] += 1;
    }

    return {
      labels: days.map(formatShortDay),
      values,
      subtitle: 'Daily consultation frequency',
    };
  }, [range, scopedConsultations]);

  const typeSlices = useMemo(() => {
    let audio = 0;
    let pdf = 0;
    let other = 0;
    for (const item of scopedConsultations) {
      const kind = consultationKind(item);
      if (kind === 'audio') audio += 1;
      else if (kind === 'pdf') pdf += 1;
      else other += 1;
    }
    return [
      { label: 'Audio', value: audio, color: theme.primary },
      { label: 'PDF', value: pdf, color: theme.palette[3] },
      ...(other > 0 ? [{ label: 'Other', value: other, color: theme.palette[5] }] : []),
    ].filter((slice) => slice.value > 0);
  }, [scopedConsultations, theme]);

  const total = scopedConsultations.length;
  const periodLabel =
    PERIOD_OPTIONS.find((option) => option.value === period)?.label ?? 'Selected period';

  if (consultations.length === 0) {
    return null;
  }

  return (
    <section className="panel">
      <div className="patient-overview-charts__toolbar">
        <h2>Activity</h2>
        <div
          className="patient-overview-charts__periods"
          role="group"
          aria-label="Activity period"
        >
          {PERIOD_OPTIONS.map((option) => (
            <button
              key={option.value}
              type="button"
              className={[
                'patient-overview-charts__period',
                period === option.value ? 'patient-overview-charts__period--active' : undefined,
              ]
                .filter(Boolean)
                .join(' ')}
              aria-pressed={period === option.value}
              onClick={() => setPeriod(option.value)}
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>

      {total === 0 ? (
        <p className="muted">No consultations in this period.</p>
      ) : (
        <div className="patient-overview-charts">
          <div className="patient-overview-charts__card">
            <div className="patient-overview-charts__header">
              <h3>Frequency</h3>
              <p className="muted">{frequency.subtitle}</p>
            </div>
            <BarChart
              labels={frequency.labels}
              series={[
                {
                  label: 'Consultations',
                  values: frequency.values,
                  color: theme.primary,
                },
              ]}
              ariaLabel={`Consultation frequency for ${periodLabel}`}
            />
          </div>

          <div className="patient-overview-charts__card">
            <div className="patient-overview-charts__header">
              <h3>By type</h3>
              <p className="muted">
                {total} consult{total === 1 ? '' : 's'} · Audio vs PDF
              </p>
            </div>
            {typeSlices.length > 0 ? (
              <DoughnutChart
                slices={typeSlices}
                centerValue={total}
                centerLabel="files"
                ariaLabel={`Consultation type mix for ${periodLabel}`}
              />
            ) : null}
          </div>
        </div>
      )}
    </section>
  );
}
