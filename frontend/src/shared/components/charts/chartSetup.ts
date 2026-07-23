import {
  ArcElement,
  BarElement,
  CategoryScale,
  Chart as ChartJS,
  Filler,
  Legend,
  LinearScale,
  Tooltip,
} from 'chart.js';

let registered = false;

/** Register Chart.js elements once for app-wide reuse. */
export function ensureChartJsRegistered() {
  if (registered) return;
  ChartJS.register(
    ArcElement,
    BarElement,
    CategoryScale,
    LinearScale,
    Tooltip,
    Legend,
    Filler,
  );
  registered = true;
}
