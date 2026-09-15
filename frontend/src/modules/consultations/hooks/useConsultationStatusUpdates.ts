import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { useEffect, useRef } from 'react';
import { useAuthStore } from '../../../features/auth';

interface ConsultationStatusChangedEvent {
  consultationId: number;
  occurredAtUtc: string;
}

function hubUrl() {
  const apiBase = import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7037/api';
  // The hub lives at the server root (/hubs/consultations), not under /api.
  const origin = apiBase.replace(/\/api\/?$/, '');
  return `${origin}/hubs/consultations`;
}

/**
 * Subscribes to the doctor's consultation-status hub for the lifetime of the caller.
 * Replaces client-side polling: the server pushes a ping whenever a watched
 * consultation's status changes, and the caller decides what to refetch. A missed
 * or never-established connection just means no live update until the next manual
 * refresh — the durable state in the database is unaffected either way.
 */
export function useConsultationStatusUpdates(onStatusChanged: (event: ConsultationStatusChangedEvent) => void) {
  const callbackRef = useRef(onStatusChanged);

  useEffect(() => {
    callbackRef.current = onStatusChanged;
  }, [onStatusChanged]);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl(), { accessTokenFactory: () => useAuthStore.getState().token ?? '' })
      .withAutomaticReconnect()
      .build();

    connection.on('ConsultationStatusChanged', (event: ConsultationStatusChangedEvent) => {
      callbackRef.current(event);
    });
    connection.start().catch(() => {
      // Best-effort: a failed connection just means no live updates on this page.
    });

    return () => {
      connection.off('ConsultationStatusChanged');
      if (connection.state !== HubConnectionState.Disconnected) {
        connection.stop().catch(() => undefined);
      }
    };
  }, []);
}
