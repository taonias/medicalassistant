import { useState } from 'react';
import { SaveIcon } from '../../../app/shell/navigation/NavIcons';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import type { DoctorNote } from '../types';
import { usePatientHistory } from '../../patients';
import { useCreateDoctorNote } from '../hooks/useDoctorNotes';

interface Props {
  patientId: number;
}

export function PatientDoctorNotesPanel({ patientId }: Props) {
  const history = usePatientHistory(patientId);
  const createNoteMutation = useCreateDoctorNote();
  const [content, setContent] = useState('');

  const notes = history.data?.doctorNotes ?? [];

  async function onCreateNote() {
    const trimmed = content.trim();
    if (!trimmed) return;

    await createNoteMutation.mutateAsync({
      patientId,
      consultationId: null,
      content: trimmed,
    });

    setContent('');
  }

  return (
    <section className="panel">
      <h2>Doctor notes</h2>

      {history.isLoading ? (
        <LoadingSkeleton label="Loading notes" />
      ) : history.error ? (
        <ErrorMessage
          message={(history.error as Error)?.message ?? 'Failed to load notes'}
          onRetry={() => {
            void history.refetch();
          }}
        />
      ) : notes.length > 0 ? (
        <div className="doctor-notes-list">
          {notes.map((note: DoctorNote) => (
            <div key={note.id} className="doctor-note">
              <p className="muted doctor-note__meta">
                {note.dateCreated ? new Date(note.dateCreated).toLocaleString() : null}
              </p>
              <p className="doctor-note__content">{note.content}</p>
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
            onChange={(event) => setContent(event.target.value)}
            placeholder="Write a clinical note for this patient..."
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
