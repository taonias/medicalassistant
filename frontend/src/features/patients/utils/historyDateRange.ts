/** Local calendar date as YYYY-MM-DD */
export function toDateInputValue(date = new Date()) {
  const year = date.getFullYear();
  const month = (date.getMonth() + 1).toString().padStart(2, '0');
  const day = date.getDate().toString().padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function parseDateInput(value: string) {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

export function addMonthsToDateInput(value: string, months: number) {
  const date = parseDateInput(value);
  date.setMonth(date.getMonth() + months);
  return toDateInputValue(date);
}

export function addDaysToDateInput(value: string, days: number) {
  const date = parseDateInput(value);
  date.setDate(date.getDate() + days);
  return toDateInputValue(date);
}

export function compareDateInputs(a: string, b: string) {
  return parseDateInput(a).getTime() - parseDateInput(b).getTime();
}

export function formatDateInputLabel(value: string) {
  const date = parseDateInput(value);
  return new Intl.DateTimeFormat(undefined, {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(date);
}

export function formatHistoryRangeLabel(fromDate: string, toDate: string) {
  if (fromDate === toDate) {
    return formatDateInputLabel(fromDate);
  }
  return `${formatDateInputLabel(fromDate)} – ${formatDateInputLabel(toDate)}`;
}

export function isValidHistoryRangePair(a: string, b: string) {
  const start = a <= b ? a : b;
  const end = a <= b ? b : a;
  return compareDateInputs(end, addMonthsToDateInput(start, 1)) <= 0;
}

/** Inclusive one-month window starting at `fromDate`. */
export function isDateWithinOneMonthOf(fromDate: string, candidate: string) {
  return isValidHistoryRangePair(fromDate, candidate);
}

export function getMonthMatrix(year: number, monthIndex: number) {
  const first = new Date(year, monthIndex, 1);
  const startOffset = first.getDay(); // Sunday = 0
  const daysInMonth = new Date(year, monthIndex + 1, 0).getDate();
  const cells: (string | null)[] = [];

  for (let i = 0; i < startOffset; i += 1) {
    cells.push(null);
  }

  for (let day = 1; day <= daysInMonth; day += 1) {
    cells.push(toDateInputValue(new Date(year, monthIndex, day)));
  }

  while (cells.length % 7 !== 0) {
    cells.push(null);
  }

  const weeks: (string | null)[][] = [];
  for (let i = 0; i < cells.length; i += 7) {
    weeks.push(cells.slice(i, i + 7));
  }
  return weeks;
}

export function shiftMonth(year: number, monthIndex: number, delta: number) {
  const date = new Date(year, monthIndex + delta, 1);
  return { year: date.getFullYear(), monthIndex: date.getMonth() };
}
