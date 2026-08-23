import { expect, test } from '../fixtures/authenticatedPage';

test('patient consultation upload keeps PDF selection and create-before-upload order', async ({
  authenticatedPage: page,
}) => {
  const order: string[] = [];
  await page.route('**/api/patient/7', (route) =>
    route.fulfill({
      json: {
        id: 7,
        firstName: 'Sam',
        lastName: 'Taylor',
        assignedDoctorId: 'doctor-e2e',
      },
    }),
  );
  await page.route('**/api/consultation', async (route) => {
    if (route.request().method() !== 'POST') return route.fallback();
    order.push('create');
    await route.fulfill({
      json: {
        id: 43,
        patientId: 7,
        doctorId: 'doctor-e2e',
        consultationDate: '2026-08-23T10:00:00Z',
        status: 'Draft',
      },
    });
  });
  await page.route('**/api/consultation/43/document', async (route) => {
    order.push('upload');
    const multipart = route.request().postData() ?? '';
    expect(multipart).toContain('consultation.pdf');
    expect(multipart).toContain('application/pdf');
    await route.fulfill({
      json: {
        id: 43,
        patientId: 7,
        doctorId: 'doctor-e2e',
        consultationDate: '2026-08-23T10:00:00Z',
        status: 'DocumentUploaded',
        documentFileName: 'consultation.pdf',
      },
    });
  });

  await page.goto('/patients/7/consultations/new');
  await page.getByRole('tab', { name: 'Upload' }).click();
  await expect(page.getByText('Drop audio or PDF here or click to browse')).toBeVisible();
  await expect(page.locator('.app-shell')).toHaveScreenshot('upload-ready.png', {
    animations: 'disabled',
    caret: 'hide',
  });

  await page.locator('input[type=file]').setInputFiles({
    name: 'consultation.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.4 browser contract'),
  });

  await page.waitForURL('**/patients/7/consultations/43');
  expect(order).toEqual(['create', 'upload']);
});
