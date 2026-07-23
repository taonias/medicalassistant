import { useMemo } from 'react';
import { Doughnut } from 'react-chartjs-2';
import type { ChartData, ChartOptions, Plugin } from 'chart.js';
import { ensureChartJsRegistered } from './chartSetup';
import { useChartTheme } from './useChartTheme';

ensureChartJsRegistered();

export interface DoughnutChartSlice {
  label: string;
  value: number;
  color: string;
}

interface Props {
  slices: DoughnutChartSlice[];
  centerLabel?: string;
  centerValue?: string | number;
  ariaLabel?: string;
  className?: string;
  cutout?: string | number;
}

export function DoughnutChart({
  slices,
  centerLabel,
  centerValue,
  ariaLabel = 'Doughnut chart',
  className,
  cutout = '68%',
}: Props) {
  const theme = useChartTheme();

  const data = useMemo<ChartData<'doughnut'>>(
    () => ({
      labels: slices.map((slice) => slice.label),
      datasets: [
        {
          data: slices.map((slice) => slice.value),
          backgroundColor: slices.map((slice) => slice.color),
          borderColor: theme.surface,
          borderWidth: 2,
          hoverBorderColor: theme.surface,
          hoverOffset: 4,
        },
      ],
    }),
    [slices, theme.surface],
  );

  const centerPlugin = useMemo<Plugin<'doughnut'>>(
    () => ({
      id: 'doughnutCenterText',
      afterDraw(chart) {
        if (centerValue == null && !centerLabel) return;
        const { ctx, chartArea } = chart;
        if (!chartArea) return;

        const x = (chartArea.left + chartArea.right) / 2;
        const y = (chartArea.top + chartArea.bottom) / 2;

        ctx.save();
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillStyle = theme.text;
        ctx.font = `700 1.35rem ${theme.fontFamily}`;
        if (centerValue != null) {
          ctx.fillText(String(centerValue), x, y - (centerLabel ? 8 : 0));
        }
        if (centerLabel) {
          ctx.fillStyle = theme.textMuted;
          ctx.font = `500 0.75rem ${theme.fontFamily}`;
          ctx.fillText(centerLabel, x, y + (centerValue != null ? 14 : 0));
        }
        ctx.restore();
      },
    }),
    [centerLabel, centerValue, theme.fontFamily, theme.text, theme.textMuted],
  );

  const options = useMemo<ChartOptions<'doughnut'>>(
    () => ({
      responsive: true,
      maintainAspectRatio: false,
      cutout,
      color: theme.text,
      plugins: {
        legend: {
          position: 'bottom',
          labels: {
            boxWidth: 10,
            boxHeight: 10,
            usePointStyle: true,
            pointStyle: 'circle',
            color: theme.text,
            font: {
              family: theme.fontFamily,
              size: 12,
              weight: 500,
            },
            padding: 14,
            generateLabels(chart) {
              const dataset = chart.data.datasets[0];
              const values = (dataset.data as number[]) ?? [];
              return (chart.data.labels ?? []).map((label, index) => ({
                text: `${String(label)} · ${values[index] ?? 0}`,
                fillStyle: Array.isArray(dataset.backgroundColor)
                  ? String(dataset.backgroundColor[index])
                  : String(dataset.backgroundColor),
                strokeStyle: 'transparent',
                lineWidth: 0,
                hidden: false,
                index,
                fontColor: theme.text,
              }));
            },
          },
        },
        tooltip: {
          backgroundColor: theme.tooltipBg,
          titleColor: theme.tooltipText,
          bodyColor: theme.tooltipText,
          borderColor: theme.tooltipBorder,
          borderWidth: 1,
          titleFont: {
            family: theme.fontFamily,
            size: 12,
            weight: 600,
          },
          bodyFont: {
            family: theme.fontFamily,
            size: 12,
            weight: 400,
          },
          callbacks: {
            label(context) {
              const label = context.label ?? '';
              const value = typeof context.parsed === 'number' ? context.parsed : 0;
              return `${label}: ${value}`;
            },
          },
        },
      },
    }),
    [cutout, theme],
  );

  return (
    <div className={['chart-canvas', 'chart-canvas--doughnut', className].filter(Boolean).join(' ')}>
      <Doughnut
        key={theme.mode}
        data={data}
        options={options}
        plugins={[centerPlugin]}
        aria-label={ariaLabel}
      />
    </div>
  );
}
