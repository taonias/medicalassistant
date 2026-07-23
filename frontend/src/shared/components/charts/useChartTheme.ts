import { useEffect, useMemo, useState } from 'react';
import { Chart as ChartJS } from 'chart.js';

function readCssVar(name: string, fallback: string) {
  if (typeof window === 'undefined') return fallback;
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return value || fallback;
}

function resolveFontFamily() {
  if (typeof window === 'undefined') {
    return '"Plus Jakarta Sans", Inter, "Segoe UI", Roboto, Helvetica, Arial, sans-serif';
  }

  const fromBody = getComputedStyle(document.body).fontFamily?.trim();
  if (fromBody) return fromBody;

  return readCssVar(
    '--font-sans',
    '"Plus Jakarta Sans", Inter, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
  );
}

function currentDocumentTheme() {
  if (typeof document === 'undefined') return 'light';
  return document.documentElement.getAttribute('data-theme') ?? 'light';
}

export interface ChartTheme {
  mode: string;
  text: string;
  textMuted: string;
  border: string;
  surface: string;
  surfaceMuted: string;
  primary: string;
  accent: string;
  danger: string;
  fontFamily: string;
  grid: string;
  tooltipBg: string;
  tooltipBorder: string;
  tooltipText: string;
  palette: string[];
}

/** Theme-aware Chart.js colors/fonts resolved from CSS variables. */
export function useChartTheme(): ChartTheme {
  const [themeToken, setThemeToken] = useState(currentDocumentTheme);

  useEffect(() => {
    const root = document.documentElement;
    const sync = () => setThemeToken(currentDocumentTheme());
    sync();

    const observer = new MutationObserver(sync);
    observer.observe(root, {
      attributes: true,
      attributeFilter: ['data-theme', 'class', 'style'],
    });
    return () => observer.disconnect();
  }, []);

  const theme = useMemo<ChartTheme>(() => {
    void themeToken;
    return {
      mode: themeToken,
      text: readCssVar('--text', '#0d2b52'),
      textMuted: readCssVar('--text-muted', '#5a7a8a'),
      border: readCssVar('--border', '#c9dde0'),
      surface: readCssVar('--surface', '#ffffff'),
      surfaceMuted: readCssVar('--surface-muted', '#eaf3f4'),
      primary: readCssVar('--primary', '#3fb1b5'),
      accent: readCssVar('--accent', '#0d2b52'),
      danger: readCssVar('--chart-danger', readCssVar('--danger', '#b42318')),
      fontFamily: resolveFontFamily(),
      grid: readCssVar('--chart-grid', 'rgba(13, 43, 82, 0.12)'),
      tooltipBg: readCssVar('--chart-tooltip-bg', '#ffffff'),
      tooltipBorder: readCssVar('--chart-tooltip-border', '#c9dde0'),
      tooltipText: readCssVar('--chart-tooltip-text', '#0d2b52'),
      palette: [
        readCssVar('--chart-1', '#3fb1b5'),
        readCssVar('--chart-2', '#0d2b52'),
        readCssVar('--chart-3', '#2a8f93'),
        readCssVar('--chart-4', '#5a9aa3'),
        readCssVar('--chart-5', '#7a9bb0'),
        readCssVar('--chart-6', '#9ebec3'),
      ],
    };
  }, [themeToken]);

  useEffect(() => {
    ChartJS.defaults.color = theme.textMuted;
    ChartJS.defaults.borderColor = theme.grid;
    ChartJS.defaults.font.family = theme.fontFamily;
    ChartJS.defaults.font.size = 12;
    ChartJS.defaults.plugins.legend.labels.color = theme.text;
    ChartJS.defaults.plugins.legend.labels.font = {
      family: theme.fontFamily,
      size: 12,
      weight: 500,
    };
    ChartJS.defaults.plugins.tooltip.backgroundColor = theme.tooltipBg;
    ChartJS.defaults.plugins.tooltip.titleColor = theme.tooltipText;
    ChartJS.defaults.plugins.tooltip.bodyColor = theme.tooltipText;
    ChartJS.defaults.plugins.tooltip.borderColor = theme.tooltipBorder;
    ChartJS.defaults.plugins.tooltip.borderWidth = 1;
    ChartJS.defaults.plugins.tooltip.titleFont = {
      family: theme.fontFamily,
      size: 12,
      weight: 600,
    };
    ChartJS.defaults.plugins.tooltip.bodyFont = {
      family: theme.fontFamily,
      size: 12,
      weight: 400,
    };
  }, [theme]);

  return theme;
}

export function statusChartColor(status: string, theme: ChartTheme): string {
  switch (status) {
    case 'Completed':
      return theme.palette[0];
    case 'StructuredDataPending':
      return theme.palette[1];
    case 'Transcribed':
      return theme.palette[2];
    case 'Transcribing':
      return theme.palette[3];
    case 'AudioUploaded':
    case 'DocumentUploaded':
      return theme.palette[4];
    case 'Draft':
      return theme.palette[5];
    case 'Failed':
      return theme.danger;
    default:
      return theme.primary;
  }
}
