import { useState } from 'react';
import { SaveIcon } from '../../../../app/shell/navigation/NavIcons';
import { ErrorMessage } from '../../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../../shared/components/LoadingSkeleton';
import { useCreateDoctorNote, type DoctorNote } from '../../../clinical-record';

interface Props {
  consultationId: number;
  notes: DoctorNote[] | undefined;
  isLoading: boolean;
  error: unknown;
}

export function ConsultationDoctorNotesPanel({ consultationId, notes, isLoading, error }: Props) {
  const createNoteMutation = useCreateDoctorNote();
  const [content, setContent] = useState('');

  async function onCreateNote() {
    const trimmed = content.trim();
    if (!trimmed) return;

    await createNoteMutation.mutateAsync({
      consultationId,
      content: trimmed,
    });

    setContent('');
  }

  return (
    <section className="panel">
      <h3>Doctor Notes</h3>

      {isLoading ? (
        <LoadingSkeleton label="Loading notes" />
      ) : error ? (
        <ErrorMessage message={(error as Error)?.message ?? 'Failed to load notes'} />
      ) : notes && notes.length > 0 ? (
        <div className="doctor-notes-list">
          {notes.map((n: DoctorNote) => (
            <div key={n.id} className="doctor-note">
              <p className="muted doctor-note__meta">
                {n.dateCreated ? new Date(n.dateCreated).toLocaleString() : null}
              </p>
              <p className="doctor-note__content">{n.content}</p>
            </div>
          ))}
        </div>
      ) : (
        <p className="muted">No notes yet.</p>
      )}

      <div className="doctor-note-form">
        <label className="field">
          <span className="field__label">Note</span>
          <textarea
            className="textarea"
            value={content}
            onChange={(e) => setContent(e.target.value)}
            placeholder="Write a clinical note for this consultation..."
            rows={5}
          />
        </label>

        <div className="doctor-note-form__actions">
          <button
            type="button"
            className="icon-button icon-button--primary"
            onClick={() => void onCreateNote()}
            disabled={createNoteMutation.isPending || !content.trim()}
            aria-label={createNoteMutation.isPending ? 'Saving note' : 'Save note'}
            title="Save note"
          >
            <SaveIcon />
          </button>
        </div>

        {createNoteMutation.error ? (
          <ErrorMessage
            message={(createNoteMutation.error as Error)?.message ?? 'Failed to save note'}
          />
        ) : null}
      </div>
    </section>
  );
}
