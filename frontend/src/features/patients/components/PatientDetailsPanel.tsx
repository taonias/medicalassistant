import { useEffect } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { CloseIcon, EditIcon, SaveIcon } from '../../../layouts/navigation/NavIcons';
import { ErrorMessage } from '../../../shared/components/ErrorMessage';
import type { Patient } from '../../../shared/types/api';
import {
  formatDateOfBirth,
  formatPatientName,
} from '../../../shared/utils/format';
import { useUpdatePatient } from '../hooks/usePatients';
import { SingleDatePicker } from './SingleDatePicker';

const patientDetailsSchema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  externalPatientId: z.string().optional(),
  dateOfBirth: z.string().optional(),
});

type PatientDetailsForm = z.infer<typeof patientDetailsSchema>;

function toDateInputValue(value?: string) {
  if (!value) return '';
  if (/^\d{4}-\d{2}-\d{2}/.test(value)) return value.slice(0, 10);
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const year = date.getFullYear();
  const month = (date.getMonth() + 1).toString().padStart(2, '0');
  const day = date.getDate().toString().padStart(2, '0');
  return `${year}-${month}-${day}`;
}

interface Props {
  patient: Patient;
  isEditing: boolean;
  onEdit: () => void;
  onCancel: () => void;
  onSaved: () => void;
}

export function PatientDetailsPanel({
  patient,
  isEditing,
  onEdit,
  onCancel,
  onSaved,
}: Props) {
  const updatePatient = useUpdatePatient();
  const {
    register,
    control,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<PatientDetailsForm>({
    resolver: zodResolver(patientDetailsSchema),
    defaultValues: {
      firstName: patient.firstName,
      lastName: patient.lastName,
      externalPatientId: patient.externalPatientId ?? '',
      dateOfBirth: toDateInputValue(patient.dateOfBirth),
    },
  });

  useEffect(() => {
    reset({
      firstName: patient.firstName,
      lastName: patient.lastName,
      externalPatientId: patient.externalPatientId ?? '',
      dateOfBirth: toDateInputValue(patient.dateOfBirth),
    });
  }, [patient, reset, isEditing]);

  if (!isEditing) {
    return (
      <section className="panel patient-details-panel">
        <div className="patient-details-panel__header">
          <h2>Patient details</h2>
          <button
            type="button"
            className="icon-button"
            onClick={onEdit}
            aria-label="Edit patient details"
            title="Edit patient details"
          >
            <EditIcon />
          </button>
        </div>

        <dl className="patient-details-grid">
          <div className="patient-details-grid__item">
            <dt className="muted">Name</dt>
            <dd>{formatPatientName(patient.firstName, patient.lastName)}</dd>
          </div>
          <div className="patient-details-grid__item">
            <dt className="muted">Patient ID</dt>
            <dd>{patient.id}</dd>
          </div>
          <div className="patient-details-grid__item">
            <dt className="muted">External ID / MRN</dt>
            <dd>{patient.externalPatientId?.trim() || '—'}</dd>
          </div>
          <div className="patient-details-grid__item">
            <dt className="muted">Date of birth</dt>
            <dd>{formatDateOfBirth(patient.dateOfBirth)}</dd>
          </div>
        </dl>
      </section>
    );
  }

  return (
    <section className="panel patient-details-panel">
      <div className="patient-details-panel__header">
        <h2>Patient details</h2>
        <div className="patient-details-panel__actions">
          <button
            type="button"
            className="icon-button"
            onClick={() => {
              reset();
              onCancel();
            }}
            disabled={updatePatient.isPending}
            aria-label="Cancel editing"
            title="Cancel"
          >
            <CloseIcon />
          </button>
          <button
            type="submit"
            form="patient-details-form"
            className="icon-button icon-button--primary"
            disabled={updatePatient.isPending || !isDirty}
            aria-label={updatePatient.isPending ? 'Saving patient details' : 'Save patient details'}
            title="Save"
          >
            <SaveIcon />
          </button>
        </div>
      </div>

      <form
        id="patient-details-form"
        className="patient-details-form"
        onSubmit={handleSubmit(async (values) => {
          await updatePatient.mutateAsync({
            id: patient.id,
            firstName: values.firstName.trim(),
            lastName: values.lastName.trim(),
            externalPatientId: values.externalPatientId?.trim() || null,
            dateOfBirth: values.dateOfBirth?.trim() || null,
          });
          onSaved();
        })}
      >
        <label className="field">
          <span className="field__label">First name</span>
          <input className="input" autoFocus {...register('firstName')} />
          {errors.firstName ? (
            <span className="field__error">{errors.firstName.message}</span>
          ) : null}
        </label>

        <label className="field">
          <span className="field__label">Last name</span>
          <input className="input" {...register('lastName')} />
          {errors.lastName ? (
            <span className="field__error">{errors.lastName.message}</span>
          ) : null}
        </label>

        <label className="field">
          <span className="field__label">External ID / MRN</span>
          <input className="input" {...register('externalPatientId')} />
        </label>

        <div className="field">
          <span className="field__label">Date of birth</span>
          <Controller
            name="dateOfBirth"
            control={control}
            render={({ field }) => (
              <SingleDatePicker
                value={field.value ?? ''}
                onChange={field.onChange}
                label="Date of birth"
                ariaLabel="Select date of birth"
              />
            )}
          />
        </div>

        {updatePatient.error ? (
          <ErrorMessage
            message={(updatePatient.error as Error).message ?? 'Unable to save patient details'}
          />
        ) : null}
      </form>
    </section>
  );
}
