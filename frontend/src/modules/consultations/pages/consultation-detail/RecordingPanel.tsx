import { ErrorMessage } from '../../../../shared/components/ErrorMessage';
import { DownloadIcon } from '../../../../app/shell/navigation/NavIcons';
import type { ApiError } from '../../../../shared/types/api';
import { RecordingPreviewPlayer } from '../../record';
import { consultationApi } from '../../api/consultationApi';

interface Props {
  consultationId: number;
  durationSeconds: number;
  hasStoredAudio: boolean;
  audioIsLoading: boolean;
  audioError: unknown;
  audioSrc: string | null | undefined;
  onRetryAudio: () => void;
}

export function RecordingPanel({
  consultationId,
  durationSeconds,
  hasStoredAudio,
  audioIsLoading,
  audioError,
  audioSrc,
  onRetryAudio,
}: Props) {
  return (
    <section className="panel">
      <div className="panel-heading">
        <h3>Recording</h3>
        {hasStoredAudio ? (
          <button
            type="button"
            className="icon-button"
            aria-label="Download recording"
            title="Download recording"
            onClick={() => {
              void consultationApi
                .downloadAudio(consultationId)
                .catch((error: ApiError) => {
                  window.alert(error.message ?? 'Unable to download recording.');
                });
            }}
          >
            <DownloadIcon />
          </button>
        ) : null}
      </div>
      {hasStoredAudio && audioIsLoading ? (
        <p className="muted">Loading recording…</p>
      ) : hasStoredAudio && audioError ? (
        <ErrorMessage
          message={(audioError as Error).message ?? 'Unable to load recording'}
          onRetry={onRetryAudio}
        />
      ) : (
        <RecordingPreviewPlayer
          durationSeconds={durationSeconds}
          audioSrc={hasStoredAudio ? audioSrc : null}
        />
      )}
    </section>
  );
}
