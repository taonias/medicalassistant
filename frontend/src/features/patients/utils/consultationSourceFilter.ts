import type { ConsultationHistoryItem } from '../types';

export type ConsultationSourceFilter = 'all' | 'audio' | 'pdf';
export type ConsultationSourceKind = 'audio' | 'pdf' | 'unknown';

export function parseSourceFilter(value: string | null): ConsultationSourceFilter {
  if (value === 'audio' || value === 'pdf') return value;
  return 'all';
}

export function consultationSource(item: ConsultationHistoryItem): ConsultationSourceKind {
  const raw = item as ConsultationHistoryItem & {
    HasAudio?: boolean;
    HasDocument?: boolean;
    Source?: string;
  };
  const source = (item.source ?? raw.Source ?? '').toLowerCase();
  if (source === 'pdf') return 'pdf';
  if (source === 'audio') return 'audio';

  const hasAudio = Boolean(item.hasAudio ?? raw.HasAudio);
  const hasDocument = Boolean(item.hasDocument ?? raw.HasDocument);
  if (hasDocument && !hasAudio) return 'pdf';
  if (hasAudio) return 'audio';

  const status = item.status.replace(/\s+/g, '').toLowerCase();
  if (status === 'documentuploaded') return 'pdf';
  if (status === 'audiouploaded') return 'audio';
  return 'unknown';
}

export function consultationSummaryText(item: ConsultationHistoryItem) {
  return item.structuredSummary?.trim() || null;
}
