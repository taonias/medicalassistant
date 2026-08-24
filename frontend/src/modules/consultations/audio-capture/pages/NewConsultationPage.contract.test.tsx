import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { createTestQueryClient } from '../../../../test/queryClient';
import { server } from '../../../../test/server';
import { NewConsultationPage } from './NewConsultationPage';

describe('new Consultation upload workflow contract', () => {
  it('loads the Patient, creates a Consultation, uploads its Consultation Document, then navigates', async () => {
    const requests: string[] = [];
    const patient = {
      id: 7,
      firstName: 'Alex',
      lastName: 'Patient',
      assignedDoctorId: 'doctor-1',
    };
    const consultation = {
      id: 42,
      patientId: 7,
      doctorId: 'doctor-1',
      consultationDate: '2026-08-23T12:00:00Z',
      status: 'DocumentUploaded',
    };
    server.use(
      http.get('https://backend.test/api/patient/7', () => {
        requests.push('GET /api/patient/7');
        return HttpResponse.json(patient);
      }),
      http.post('https://backend.test/api/consultation', () => {
        requests.push('POST /api/consultation');
        return HttpResponse.json(consultation);
      }),
      http.post('https://backend.test/api/consultation/42/document', () => {
        requests.push('POST /api/consultation/42/document');
        return HttpResponse.json(consultation);
      }),
    );
    const router = createMemoryRouter(
      [
        {
          path: '/patients/:patientId/consultations/new',
          element: <NewConsultationPage />,
        },
        {
          path: '/patients/:patientId/consultations/:consultationId',
          element: <h1>Consultation detail</h1>,
        },
      ],
      { initialEntries: ['/patients/7/consultations/new'] },
    );
    const user = userEvent.setup();

    render(
      <QueryClientProvider client={createTestQueryClient()}>
        <RouterProvider router={router} />
      </QueryClientProvider>,
    );

    expect(await screen.findByText(/Ensure patient consent for recording/)).toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: 'Upload' }));
    await user.upload(
      screen.getByLabelText(/Drop audio or PDF here or click to browse/),
      new File(['document'], 'referral.pdf', { type: 'application/pdf' }),
    );

    expect(await screen.findByRole('heading', { name: 'Consultation detail' })).toBeInTheDocument();
    await waitFor(() => {
      expect(requests).toEqual([
        'GET /api/patient/7',
        'POST /api/consultation',
        'POST /api/consultation/42/document',
      ]);
    });
  });
});
