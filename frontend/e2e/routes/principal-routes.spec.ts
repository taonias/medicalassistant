import { expect, test } from '../fixtures/authenticatedPage';

test('dashboard route keeps its responsive shell and empty state', async ({
  authenticatedPage: page,
}) => {
  await page.route('**/api/consultation/analytics', (route) =>
    route.fulfill({ json: null }),
  );
  await page.route('**/api/consultation/drafts/unattached', (route) =>
    route.fulfill({ json: [] }),
  );

  await page.goto('/');

  await expect(
    page.getByRole('heading', { name: 'Unassigned recordings', exact: true }),
  ).toBeVisible();
  await expect(page.getByRole('heading', { name: 'No dashboard data yet' })).toBeVisible();
  await expect(page.locator('.app-shell')).toHaveScreenshot('dashboard-empty.png', {
    animations: 'disabled',
    caret: 'hide',
  });
});

test('patient list and settings routes keep their owned screen states', async ({
  authenticatedPage: page,
}) => {
  await page.route('**/api/patient', (route) =>
    route.fulfill({
      json: [
        {
          id: 7,
          firstName: 'Sam',
          lastName: 'Taylor',
          dateOfBirth: '1985-04-12',
          consultationCount: 3,
          lastConsultationDate: '2026-08-20T09:00:00Z',
        },
      ],
    }),
  );

  await page.goto('/patients');
  await expect(page.getByText('Taylor, Sam')).toBeVisible();
  await expect(page.locator('.app-shell')).toHaveScreenshot('patients-list.png', {
    animations: 'disabled',
    caret: 'hide',
  });

  await page.goto('/settings');
  await expect(page.getByRole('heading', { name: 'Appearance' })).toBeVisible();
  await expect(page.locator('.app-shell')).toHaveScreenshot('settings.png', {
    animations: 'disabled',
    caret: 'hide',
  });
});
