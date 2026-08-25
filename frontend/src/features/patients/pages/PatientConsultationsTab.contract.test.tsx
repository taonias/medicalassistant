import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { createTestQueryClient } from '../../../test/queryClient';
import { server } from '../../../test/server';
import { PatientConsultationsTab } from './PatientTabs';

function renderAtHistoryRoute() {
  const router = createMemoryRouter(
    [
      {
        path: '/patients/:patientId',
        element: <Outlet context={{ patientId: 7 }} />,
        children: [{ path: 'history', element: <PatientConsultationsTab /> }],
      },
    ],
    { initialEntries: ['/patients/7/history'] },
  );

  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );

  return router;
}

describe('patient consultations tab journey', () => {
  it('keeps panel DOM order, fires the default history request, and reflects filter/delete actions in route state', async () => {
    const historyRequests: string[] = [];
    let consultations = [
      {
        id: 42,
        consultationDate: '2026-08-20T09:00:00Z',
        status: 'Completed',
        durationSeconds: 120,
        hasAudio: true,
        structuredSummary: 'Follow-up discussed.',
      },
      {
        id: 43,
        consultationDate: '2026-08-19T09:00:00Z',
        status: 'DocumentUploaded',
        hasDocument: true,
      },
    ];

    server.use(
      http.get('https://backend.test/api/patient/7/history', ({ request }) => {
        historyRequests.push(new URL(request.url).search);
        return HttpResponse.json({
          patient: { id: 7, firstName: 'Sam', lastName: 'Taylor', assignedDoctorId: 'doctor-1' },
          consultations,
          doctorNotes: [],
          totalConsultations: consultations.length,
        });
      }),
      http.delete('https://backend.test/api/consultation/43', () => {
        consultations = consultations.filter((c) => c.id !== 43);
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const user = userEvent.setup();
    renderAtHistoryRoute();

    // DOM order: the 3 extracted panels render in their original order.
    await screen.findByText('Follow-up discussed.');
    const container = document.querySelector('.stack') as HTMLElement;
    const panels = container.querySelectorAll(
      '.consultations-upload, .consultations-filters, .consultations-results',
    );
    expect(Array.from(panels).map((el) => el.className)).toEqual([
      expect.stringContaining('consultations-upload'),
      expect.stringContaining('consultations-filters'),
      expect.stringContaining('consultations-results'),
    ]);

    // Default request carries the backfilled date range and page size, no source filter.
    await waitFor(() => expect(historyRequests).toHaveLength(1));
    const initialParams = new URLSearchParams(historyRequests[0]);
    expect(initialParams.get('pageSize')).toBe('10');
    expect(initialParams.get('source')).toBeNull();
    expect(initialParams.get('fromDate')).toMatch(/T00:00:00$/);
    expect(initialParams.get('toDate')).toMatch(/T23:59:59$/);

    // Open the Filters panel and switch to PDF-only — route state (URL) updates and refetches.
    await user.click(screen.getByRole('heading', { name: 'Filters' }));
    await user.click(screen.getByRole('button', { name: 'PDF' }));

    await waitFor(() => expect(historyRequests).toHaveLength(2));
    expect(new URLSearchParams(historyRequests[1]).get('source')).toBe('pdf');

    // Delete flow: confirm modal, then the item disappears and a DELETE fires.
    // Consultation 43 renders second (array order) — its delete button is index 1.
    const deleteButtons = screen.getAllByRole('button', { name: /Delete consultation from/ });
    await user.click(deleteButtons[1]);
    await user.click(screen.getByRole('button', { name: 'Delete' }));

    await waitFor(() => expect(historyRequests).toHaveLength(3));
    expect(screen.getAllByRole('button', { name: /Delete consultation from/ })).toHaveLength(1);
  });
});
