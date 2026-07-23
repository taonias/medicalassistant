import { useMemo } from 'react';
import { Bar } from 'react-chartjs-2';
import type { ChartData, ChartOptions } from 'chart.js';
import { ensureChartJsRegistered } from './chartSetup';
import { useChartTheme } from './useChartTheme';

ensureChartJsRegistered();

export interface BarChartSeries {
  label: string;
  values: number[];
  color?: string;
  /** Stacked bars use the same stack id. */
  stack?: string;
}

interface Props {
  labels: string[];
  series: BarChartSeries[];
  ariaLabel?: string;
  className?: string;
  horizontal?: boolean;
  stacked?: boolean;
  showLegend?: boolean;
  yBeginAtZero?: boolean;
}

export function BarChart({
  labels,
  series,
  ariaLabel = 'Bar chart',
  className,
  horizontal = false,
  stacked = false,
  showLegend = false,
  yBeginAtZero = true,
}: Props) {
  const theme = useChartTheme();

  const data = useMemo<ChartData<'bar'>>(
    () => ({
      labels,
      datasets: series.map((item, index) => ({
        label: item.label,
        data: item.values,
        backgroundColor: item.color ?? theme.palette[index % theme.palette.length],
        borderRadius: 6,
        borderSkipped: false,
        maxBarThickness: horizontal ? 28 : 18,
        stack: stacked ? item.stack ?? 'stack' : undefined,
      })),
    }),
    [labels, series, stacked, theme.palette, horizontal],
  );

  const options = useMemo<ChartOptions<'bar'>>(
    () => ({
      responsive: true,
      maintainAspectRatio: false,
      indexAxis: horizontal ? 'y' : 'x',
      color: theme.text,
      plugins: {
        legend: {
          display: showLegend,
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
              const label = context.dataset.label ? `${context.dataset.label}: ` : '';
              const value = horizontal ? context.parsed.x : context.parsed.y;
              return `${label}${value ?? 0}`;
            },
          },
        },
      },
      scales: {
        x: {
          stacked,
          grid: {
            color: theme.grid,
            drawTicks: false,
          },
          ticks: {
            color: theme.textMuted,
            font: {
              family: theme.fontFamily,
              size: 11,
              weight: 500,
            },
            maxRotation: 0,
            autoSkip: true,
          },
          border: { display: false },
        },
        y: {
          stacked,
          beginAtZero: yBeginAtZero,
          grid: {
            color: theme.grid,
            drawTicks: false,
          },
          ticks: {
            color: theme.textMuted,
            precision: 0,
            font: {
              family: theme.fontFamily,
              size: 11,
              weight: 500,
            },
          },
          border: { display: false },
        },
      },
    }),
    [horizontal, showLegend, stacked, theme, yBeginAtZero],
  );

  return (
    <div className={['chart-canvas', 'chart-canvas--bar', className].filter(Boolean).join(' ')}>
      <Bar key={theme.mode} data={data} options={options} aria-label={ariaLabel} />
    </div>
  );
}
