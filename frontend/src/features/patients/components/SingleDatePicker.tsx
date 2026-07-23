import { useEffect, useId, useMemo, useRef, useState } from 'react';
import {
  formatDateInputLabel,
  getMonthMatrix,
  parseDateInput,
  shiftMonth,
  toDateInputValue,
} from '../utils/historyDateRange';
import { CalendarMonthToolbar } from './CalendarMonthToolbar';

interface Props {
  value?: string;
  onChange: (value: string) => void;
  label?: string;
  allowClear?: boolean;
  maxDate?: string;
  ariaLabel?: string;
}

const WEEKDAYS = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];

export function SingleDatePicker({
  value = '',
  onChange,
  label = 'Date',
  allowClear = true,
  maxDate,
  ariaLabel = 'Select date',
}: Props) {
  const today = useMemo(() => toDateInputValue(), []);
  const latestAllowed = maxDate ?? today;
  const listboxId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);

  const focusDate = value || latestAllowed;
  const initialMonth = parseDateInput(focusDate);
  const [visibleYear, setVisibleYear] = useState(initialMonth.getFullYear());
  const [visibleMonth, setVisibleMonth] = useState(initialMonth.getMonth());

  const weeks = useMemo(
    () => getMonthMatrix(visibleYear, visibleMonth),
    [visibleYear, visibleMonth],
  );

  function closePicker() {
    setOpen(false);
  }

  useEffect(() => {
    if (!open) return;

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') closePicker();
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    dialogRef.current?.focus();
  }, [open]);

  function isDisabled(date: string) {
    return date > latestAllowed;
  }

  function handleDayClick(date: string) {
    if (isDisabled(date)) return;
    onChange(date);
    closePicker();
  }

  function moveMonth(delta: number) {
    const next = shiftMonth(visibleYear, visibleMonth, delta);
    const limit = parseDateInput(latestAllowed);
    if (
      delta > 0 &&
      (next.year > limit.getFullYear() ||
        (next.year === limit.getFullYear() && next.monthIndex > limit.getMonth()))
    ) {
      return;
    }
    setVisibleYear(next.year);
    setVisibleMonth(next.monthIndex);
  }

  const canGoNextMonth = useMemo(() => {
    const limit = parseDateInput(latestAllowed);
    return (
      visibleYear < limit.getFullYear() ||
      (visibleYear === limit.getFullYear() && visibleMonth < limit.getMonth())
    );
  }, [latestAllowed, visibleMonth, visibleYear]);

  return (
    <div className="history-range-picker single-date-picker">
      <button
        type="button"
        className="history-range-picker__trigger"
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-controls={listboxId}
        onClick={() => {
          setOpen((current) => !current);
          const focus = parseDateInput(value || latestAllowed);
          setVisibleYear(focus.getFullYear());
          setVisibleMonth(focus.getMonth());
        }}
      >
        <span className="history-range-picker__trigger-label">{label}</span>
        <span className="history-range-picker__trigger-value">
          {value ? formatDateInputLabel(value) : 'Select date'}
        </span>
      </button>

      {open ? (
        <div
          className="history-range-picker__backdrop"
          role="presentation"
          onClick={closePicker}
        >
          <div
            id={listboxId}
            ref={dialogRef}
            className="history-range-picker__dialog"
            role="dialog"
            aria-modal="true"
            aria-label={ariaLabel}
            tabIndex={-1}
            onClick={(event) => event.stopPropagation()}
          >
            <CalendarMonthToolbar
              visibleYear={visibleYear}
              visibleMonth={visibleMonth}
              onChange={(year, monthIndex) => {
                setVisibleYear(year);
                setVisibleMonth(monthIndex);
              }}
              maxDate={latestAllowed}
              onPreviousMonth={() => moveMonth(-1)}
              onNextMonth={() => moveMonth(1)}
              canGoNextMonth={canGoNextMonth}
            />

            <div className="history-range-picker__weekdays" aria-hidden="true">
              {WEEKDAYS.map((day) => (
                <span key={day}>{day}</span>
              ))}
            </div>

            <div className="history-range-picker__grid">
              {weeks.map((week, weekIndex) => (
                <div key={weekIndex} className="history-range-picker__week">
                  {week.map((date, dayIndex) => {
                    if (!date) {
                      return (
                        <span
                          key={`empty-${dayIndex}`}
                          className="history-range-picker__day history-range-picker__day--empty"
                        />
                      );
                    }

                    const disabled = isDisabled(date);
                    const selected = date === value;
                    const isToday = date === today;

                    return (
                      <button
                        key={date}
                        type="button"
                        className={[
                          'history-range-picker__day',
                          selected ? 'history-range-picker__day--edge' : undefined,
                          isToday ? 'history-range-picker__day--today' : undefined,
                          disabled ? 'history-range-picker__day--disabled' : undefined,
                        ]
                          .filter(Boolean)
                          .join(' ')}
                        disabled={disabled}
                        aria-pressed={selected}
                        aria-label={date}
                        onClick={() => handleDayClick(date)}
                      >
                        {Number(date.slice(-2))}
                      </button>
                    );
                  })}
                </div>
              ))}
            </div>

            <div className="single-date-picker__footer">
              <p className="muted history-range-picker__hint">
                Future dates cannot be selected.
              </p>
              {allowClear && value ? (
                <button
                  type="button"
                  className="button button--ghost button--small"
                  onClick={() => {
                    onChange('');
                    closePicker();
                  }}
                >
                  Clear
                </button>
              ) : null}
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
