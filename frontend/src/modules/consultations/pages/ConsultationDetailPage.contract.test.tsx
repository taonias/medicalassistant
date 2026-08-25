import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { createTestQueryClient } from '../../../test/queryClient';
import { server } from '../../../test/server';
import { ConsultationDetailPage } from './ConsultationDetailPage';

describe('consultation detail page journey', () => {
  it('fetches in hook-call order, keeps panel DOM order, and saves a doctor note through the notes panel', async () => {
    const requests: string[] = [];
    const notes: { id: number; content: string; dateCreated: string }[] = [];

    server.use(
      http.get('https://backend.test/api/consultation/42', () => {
        requests.push('GET /api/consultation/42');
        return HttpResponse.json({
          id: 42,
          patientId: 7,
          doctorId: 'doctor-1',
          consultationDate: '2026-08-20T09:00:00Z',
          status: 'Completed',
          durationSeconds: 90,
        });
      }),
      http.get('https://backend.test/api/patient/7', () => {
        requests.push('GET /api/patient/7');
        return HttpResponse.json({
          id: 7,
          firstName: 'Sam',
          lastName: 'Taylor',
          assignedDoctorId: 'doctor-1',
        });
      }),
      http.get('https://backend.test/api/transcript/42', () => {
        requests.push('GET /api/transcript/42');
        return HttpResponse.json(null);
      }),
      http.get('https://backend.test/api/patient/7/history', () => {
        requests.push('GET /api/patient/7/history');
        return HttpResponse.json({
          patient: { id: 7, firstName: 'Sam', lastName: 'Taylor', assignedDoctorId: 'doctor-1' },
          consultations: [],
          doctorNotes: [],
        });
      }),
      http.get('https://backend.test/api/consultation/42/structured-data', () => {
        requests.push('GET /api/consultation/42/structured-data');
        return HttpResponse.json(null);
      }),
      http.get('https://backend.test/api/doctorNotes/consultations/42', () => {
        requests.push('GET /api/doctorNotes/consultations/42');
        return HttpResponse.json(notes);
      }),
      http.post('https://backend.test/api/doctorNotes', async ({ request }) => {
        requests.push('POST /api/doctorNotes');
        const body = (await request.json()) as { consultationId: number; content: string };
        const created = { id: 1, content: body.content, dateCreated: '2026-08-24T10:00:00Z' };
        notes.push(created);
        return HttpResponse.json(created);
      }),
    );

    const router = createMemoryRouter(
      [
        {
          path: '/patients/:patientId/consultations/:consultationId',
          element: <ConsultationDetailPage />,
        },
      ],
      { initialEntries: ['/patients/7/consultations/42'] },
    );
    const user = userEvent.setup();

    render(
      <QueryClientProvider client={createTestQueryClient()}>
        <RouterProvider router={router} />
      </QueryClientProvider>,
    );

    // DOM order of the extracted panels + the untouched TranscriptViewer.
    const grid = await screen.findByText('Recording');
    const headings = within(grid.closest('.consultation-grid') as HTMLElement)
      .getAllByRole('heading', { level: 3 })
      .map((h) => h.textContent);
    expect(headings).toEqual(['Recording', 'Transcript', 'Summary', 'Doctor Notes']);

    // Every query fires. useStructuredData's `enabled` depends on the
    // consultation's status, which only becomes available after
    // useConsultation resolves — so its request is genuinely initiated
    // after useDoctorNotes' (whose `enabled` needs nothing async), even
    // though useDoctorNotes is called later in the orchestrator. This is
    // pre-existing behavior (unchanged `enabled` conditions), not something
    // this refactor introduced — asserted here to prove it survived intact.
    await waitFor(() =>
      expect(requests).toEqual([
        'GET /api/consultation/42',
        'GET /api/patient/7',
        'GET /api/transcript/42',
        'GET /api/patient/7/history',
        'GET /api/doctorNotes/consultations/42',
        'GET /api/consultation/42/structured-data',
      ]),
    );

    // The notes panel owns its own mutation — saving a note posts and refetches.
    const notesSection = screen.getByText('Doctor Notes').closest('section') as HTMLElement;
    await user.type(
      within(notesSection).getByPlaceholderText(/Write a clinical note/),
      'Patient recovering well.',
    );
    await user.click(within(notesSection).getByRole('button', { name: 'Save note' }));

    await waitFor(() => expect(requests.filter((r) => r === 'POST /api/doctorNotes')).toHaveLength(1));
    expect(await within(notesSection).findByText('Patient recovering well.')).toBeInTheDocument();
  });
});
