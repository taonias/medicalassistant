import { expect, test } from '../fixtures/authenticatedPage';
import {
  installDeterministicMediaCapture,
  readCaptureContract,
} from '../fixtures/mediaCapture';

const failedConsultation = {
  id: 42,
  patientId: 7,
  doctorId: 'doctor-e2e',
  consultationDate: '2026-08-21T08:15:00Z',
  status: 'Failed',
  audioBlobUri: 'recordings/42.webm',
  audioContentType: 'audio/webm',
  durationSeconds: 62,
  failureReason: 'Transcription timed out.',
};

test('failed consultation retries and revokes its recording object URL on exit', async ({
  authenticatedPage: page,
}) => {
  await installDeterministicMediaCapture(page);
  let retryCount = 0;
  let releaseRetry: (() => void) | undefined;
  const retryGate = new Promise<void>((resolve) => {
    releaseRetry = resolve;
  });

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
  await page.route('**/api/patient/7/history**', (route) =>
    route.fulfill({
      json: {
        patient: { id: 7, firstName: 'Sam', lastName: 'Taylor', assignedDoctorId: 'doctor-e2e' },
        consultations: [],
        doctorNotes: [],
      },
    }),
  );
  await page.route('**/api/consultation/42', (route) =>
    route.fulfill({ json: failedConsultation }),
  );
  await page.route('**/api/consultation/42/audio', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'audio/webm',
      body: 'deterministic-stored-audio',
    }),
  );
  await page.route('**/api/transcript/42', (route) => route.fulfill({ json: null }));
  await page.route('**/api/consultation/42/structured-data', (route) =>
    route.fulfill({ json: null }),
  );
  await page.route('**/api/doctorNotes/consultations/42', (route) =>
    route.fulfill({ json: [] }),
  );
  await page.route('**/api/consultation/42/retry', async (route) => {
    retryCount += 1;
    await retryGate;
    await route.fulfill({
      json: { ...failedConsultation, status: 'AudioUploaded', failureReason: undefined },
    });
  });

  await page.goto('/patients/7/consultations/42');
  await expect(page.getByRole('alert').filter({ hasText: 'Transcription timed out.' })).toBeVisible();
  await expect(page.locator('.error-message')).toHaveCount(1);
  await expect(page.locator('.consultation-grid')).toHaveCount(1);
  await expect(page.locator('.consultation-meta')).toHaveCount(1);
  await expect.poll(async () => (await readCaptureContract(page)).createdObjectUrls.length).toBe(1);
  await expect(page.locator('.app-shell')).toHaveScreenshot('consultation-failed.png', {
    animations: 'disabled',
    caret: 'hide',
  });

  await page.getByRole('button', { name: 'Retry' }).click();
  await expect.poll(() => retryCount).toBe(1);
  await expect(page.getByRole('alert')).toContainText('Retrying…');
  releaseRetry?.();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await expect(page.getByLabel('Audio Uploaded')).toBeVisible();

  const createdUrl = (await readCaptureContract(page)).createdObjectUrls[0];
  await page.locator('a[href="/settings"]:visible').click();
  await expect(page.getByRole('heading', { name: 'Appearance' })).toBeVisible();
  await expect
    .poll(async () => (await readCaptureContract(page)).revokedObjectUrls)
    .toContain(createdUrl);
});
