import { useCallback, useSyncExternalStore } from 'react';
import type { Patient } from '../../features/patients';

const STORAGE_KEY = 'medical-assistant:recent-patients';
const MAX_RECENT = 10;

let recentPatients: Patient[] = loadFromStorage();
const listeners = new Set<() => void>();

function loadFromStorage(): Patient[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as Patient[]) : [];
  } catch {
    return [];
  }
}

function persist() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(recentPatients));
  listeners.forEach((listener) => listener());
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function getSnapshot() {
  return recentPatients;
}

export function useRecentPatients() {
  const patients = useSyncExternalStore(subscribe, getSnapshot, getSnapshot);

  const addRecentPatient = useCallback((patient: Patient) => {
    recentPatients = [
      patient,
      ...recentPatients.filter((item) => item.id !== patient.id),
    ].slice(0, MAX_RECENT);
    persist();
  }, []);

  const removeRecentPatient = useCallback((patientId: number) => {
    recentPatients = recentPatients.filter((item) => item.id !== patientId);
    persist();
  }, []);

  return { patients, addRecentPatient, removeRecentPatient };
}
