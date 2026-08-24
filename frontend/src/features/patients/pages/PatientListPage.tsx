import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { PlusIcon } from '../../../app/shell/navigation/NavIcons';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import { EmptyState } from '../../../shared/components/EmptyState';
import { LoadingSkeleton } from '../../../shared/components/LoadingSkeleton';
import { PatientCard } from '../components/PatientCard';
import { PatientSearchField } from '../components/PatientSearchField';
import { useCreatePatient, usePatients } from '../hooks/usePatients';
import { matchesPatientSearch } from '../utils/matchesPatientSearch';

const createPatientSchema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  externalPatientId: z.string().optional(),
  dateOfBirth: z.string().optional(),
});

type CreatePatientForm = z.infer<typeof createPatientSchema>;

function CreatePatientModal({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate();
  const createPatient = useCreatePatient();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CreatePatientForm>({
    resolver: zodResolver(createPatientSchema),
  });

  return (
    <div className="modal-backdrop" role="presentation" onClick={onClose}>
      <div
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="create-patient-title"
        onClick={(event) => event.stopPropagation()}
      >
        <h2 id="create-patient-title">New patient</h2>
        <p className="muted">Create a patient record to start consultations and chat.</p>

        <form
          className="settings-form"
          onSubmit={handleSubmit(async (values) => {
            const patient = await createPatient.mutateAsync(values);
            onClose();
            navigate(`/patients/${patient.id}`);
          })}
        >
          <label className="field">
            <span>First name</span>
            <input autoFocus {...register('firstName')} />
            {errors.firstName ? (
              <span className="field__error">{errors.firstName.message}</span>
            ) : null}
          </label>

          <label className="field">
            <span>Last name</span>
            <input {...register('lastName')} />
            {errors.lastName ? (
              <span className="field__error">{errors.lastName.message}</span>
            ) : null}
          </label>

          <label className="field">
            <span>External ID</span>
            <input {...register('externalPatientId')} />
          </label>

          <label className="field">
            <span>Date of birth</span>
            <input type="date" {...register('dateOfBirth')} />
          </label>

          {createPatient.error ? (
            <ErrorMessage message={(createPatient.error as Error).message} />
          ) : null}

          <div className="modal__actions">
            <button type="button" className="button button--secondary" onClick={onClose}>
              Cancel
            </button>
            <button
              type="submit"
              className="button button--primary"
              disabled={createPatient.isPending}
            >
              {createPatient.isPending ? 'Creating…' : 'Create patient'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export function PatientListPage() {
  const { data: patients, isLoading, error, refetch } = usePatients();
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [search, setSearch] = useState('');

  const visiblePatients = useMemo(() => {
    if (!patients) return [];
    return patients.filter((patient) => matchesPatientSearch(patient, search));
  }, [patients, search]);

  return (
    <div className="page patients-page">
      {!isLoading && !error && patients && patients.length > 0 ? (
        <div className="patients-toolbar">
          <PatientSearchField value={search} onChange={setSearch} />
          <button
            type="button"
            className="icon-button icon-button--primary"
            onClick={() => setShowCreateForm(true)}
            aria-label="Add new patient"
            title="Add new patient"
          >
            <PlusIcon />
          </button>
        </div>
      ) : null}

      {isLoading ? (
        <LoadingSkeleton label="Loading patients" />
      ) : error ? (
        <ErrorMessage message={(error as Error).message} onRetry={() => refetch()} />
      ) : !patients || patients.length === 0 ? (
        <EmptyState
          title="No patients yet"
          description="Add your first patient to begin recording consultations and reviewing history."
          action={
            <button
              type="button"
              className="button button--primary"
              onClick={() => setShowCreateForm(true)}
            >
              Add patient
            </button>
          }
        />
      ) : visiblePatients.length === 0 ? (
        <p className="muted">No patients match your search.</p>
      ) : (
        <div className="patient-card-grid">
          {visiblePatients.map((patient) => (
            <PatientCard key={patient.id} patient={patient} />
          ))}
        </div>
      )}

      {showCreateForm ? <CreatePatientModal onClose={() => setShowCreateForm(false)} /> : null}
    </div>
  );
}
