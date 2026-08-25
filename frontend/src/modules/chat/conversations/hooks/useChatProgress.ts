import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useAuthStore } from '../../../../features/auth';
import type { ChatProgressEvent } from '../types';

function hubUrl() {
  const apiBase = import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7037/api';
  // The hub lives at the server root (/hubs/chat), not under /api.
  const origin = apiBase.replace(/\/api\/?$/, '');
  return `${origin}/hubs/chat`;
}

/**
 * Subscribes to the doctor's chat-progress hub. Exposes the latest phase event; the
 * caller filters by the in-flight askId. Progress is a convenience — the whole answer
 * always arrives on the ask's HTTP response regardless of the socket.
 */
export function useChatProgress() {
  const [progress, setProgress] = useState<ChatProgressEvent | null>(null);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl(), { accessTokenFactory: () => useAuthStore.getState().token ?? '' })
      .withAutomaticReconnect()
      .build();

    connection.on('ChatProgress', (event: ChatProgressEvent) => setProgress(event));
    connection.start().catch(() => {
      // Best-effort: a failed connection just means no live progress line.
    });
    connectionRef.current = connection;

    return () => {
      connection.off('ChatProgress');
      if (connection.state !== HubConnectionState.Disconnected) {
        connection.stop().catch(() => undefined);
      }
      connectionRef.current = null;
    };
  }, []);

  const clear = useCallback(() => setProgress(null), []);

  return { progress, clear };
}
