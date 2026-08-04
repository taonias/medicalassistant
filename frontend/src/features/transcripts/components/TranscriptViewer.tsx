import { useEffect, useState } from 'react';
import {
  CloseIcon,
  EditIcon,
  ExpandIcon,
  SaveIcon,
} from '../../../layouts/navigation/NavIcons';
import { useUpdateTranscript } from '../hooks/useTranscript';

const PREVIEW_CHAR_LIMIT = 200;

interface Props {
  consultationId: number;
  text?: string;
  status?: string;
  failureReason?: string;
}

function normalizeStatus(status?: string) {
  return (status ?? '').replace(/\s+/g, '');
}

export function TranscriptViewer({
  consultationId,
  text,
  status,
  failureReason,
}: Props) {
  const [isFullOpen, setIsFullOpen] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState(text ?? '');
  const updateTranscript = useUpdateTranscript(consultationId);
  const canEdit = Boolean(text?.trim());

  useEffect(() => {
    if (!isEditing) {
      setDraft(text ?? '');
    }
  }, [text, isEditing]);

  if (failureReason) {
    return (
      <div className="panel transcript-panel">
        <h3>Transcript</h3>
        <p className="field__error">{failureReason}</p>
      </div>
    );
  }

  if (!text?.trim() && !isEditing) {
    const normalized = normalizeStatus(status);
    const uploadedOnly =
      normalized === 'AudioUploaded' ||
      normalized === 'DocumentUploaded' ||
      normalized === 'DocumentProcessingPending';
    const documentPending = normalized === 'DocumentProcessingPending';
    const emptyTranscriptMessage = documentPending
      ? 'Document processing is pending. No transcript has been created for this PDF yet.'
      : uploadedOnly
        ? 'No transcript for this consultation.'
        : 'This consultation is still processing.';

    return (
      <div className="panel transcript-panel">
        <h3>Transcript</h3>
        <p className="muted">{emptyTranscriptMessage}</p>
      </div>
    );
  }

  const hasMore = (text?.length ?? 0) > PREVIEW_CHAR_LIMIT;
  const preview = hasMore ? `${text!.slice(0, PREVIEW_CHAR_LIMIT)}…` : text;
  const isDirty = draft.trim() !== (text ?? '').trim();
  const canSave = draft.trim().length > 0 && isDirty && !updateTranscript.isPending;

  async function onSave() {
    const trimmed = draft.trim();
    if (!trimmed) return;

    await updateTranscript.mutateAsync(trimmed);
    setIsEditing(false);
    setIsFullOpen(false);
  }

  function onCancelEdit() {
    setDraft(text ?? '');
    setIsEditing(false);
    updateTranscript.reset();
  }

  return (
    <>
      <div className="panel transcript-panel">
        <div className="panel-heading">
          <h3>Transcript</h3>
          <div className="panel-heading__actions">
            {isEditing ? (
              <>
                <button
                  type="button"
                  className="icon-button"
                  aria-label="Cancel editing transcript"
                  title="Cancel"
                  onClick={onCancelEdit}
                  disabled={updateTranscript.isPending}
                >
                  <CloseIcon />
                </button>
                <button
                  type="button"
                  className="icon-button icon-button--primary"
                  aria-label={
                    updateTranscript.isPending ? 'Saving transcript' : 'Save transcript'
                  }
                  title="Save"
                  onClick={() => {
                    void onSave().catch(() => undefined);
                  }}
                  disabled={!canSave}
                >
                  <SaveIcon />
                </button>
              </>
            ) : (
              <>
                {canEdit ? (
                  <button
                    type="button"
                    className="icon-button"
                    aria-label="Edit transcript"
                    title="Edit transcript"
                    onClick={() => {
                      setDraft(text ?? '');
                      setIsEditing(true);
                      setIsFullOpen(false);
                    }}
                  >
                    <EditIcon />
                  </button>
                ) : null}
                {hasMore ? (
                  <button
                    type="button"
                    className="icon-button"
                    aria-label="View full transcript"
                    title="View full transcript"
                    onClick={() => setIsFullOpen(true)}
                  >
                    <ExpandIcon />
                  </button>
                ) : null}
              </>
            )}
          </div>
        </div>

        {isEditing ? (
          <div className="transcript-editor">
            <textarea
              className="transcript-editor__input"
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              rows={12}
              disabled={updateTranscript.isPending}
              aria-label="Edit transcript text"
            />
            {updateTranscript.isError ? (
              <p className="field__error">
                {(updateTranscript.error as Error)?.message ??
                  'Unable to save transcript.'}
              </p>
            ) : null}
          </div>
        ) : (
          <div className="transcript-viewer">{preview}</div>
        )}
      </div>

      {isFullOpen && !isEditing ? (
        <div
          className="modal-backdrop"
          role="presentation"
          onClick={() => setIsFullOpen(false)}
        >
          <div
            className="modal modal--transcript"
            role="dialog"
            aria-modal="true"
            aria-labelledby="transcript-modal-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="panel-heading">
              <h2 id="transcript-modal-title">Transcript</h2>
              <div className="panel-heading__actions">
                {canEdit ? (
                  <button
                    type="button"
                    className="icon-button"
                    aria-label="Edit transcript"
                    title="Edit transcript"
                    onClick={() => {
                      setDraft(text ?? '');
                      setIsEditing(true);
                      setIsFullOpen(false);
                    }}
                  >
                    <EditIcon />
                  </button>
                ) : null}
                <button
                  type="button"
                  className="icon-button"
                  aria-label="Close transcript"
                  title="Close"
                  onClick={() => setIsFullOpen(false)}
                >
                  <CloseIcon />
                </button>
              </div>
            </div>
            <div className="transcript-viewer transcript-viewer--modal">{text}</div>
            <div className="modal__actions">
              <button
                type="button"
                className="button button--secondary"
                onClick={() => setIsFullOpen(false)}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
