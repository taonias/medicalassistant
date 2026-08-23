import { expect, test } from '../fixtures/authenticatedPage';
import {
  installDeterministicMediaCapture,
  readCaptureContract,
} from '../fixtures/mediaCapture';

const patient = {
  id: 7,
  firstName: 'Sam',
  lastName: 'Taylor',
  assignedDoctorId: 'doctor-e2e',
};

test('recording keeps MIME, soft pause/resume, duration, and autosave order', async ({
  authenticatedPage: page,
}) => {
  await installDeterministicMediaCapture(page);
  await page.clock.install({ time: new Date('2026-08-23T12:00:00Z') });
  const saveOrder: string[] = [];

  await page.route('**/api/patient/7', (route) => route.fulfill({ json: patient }));
  await page.route('**/api/consultation', async (route) => {
    if (route.request().method() !== 'POST') return route.fallback();
    saveOrder.push('create');
    expect(route.request().postDataJSON()).toMatchObject({ patientId: 7, durationSeconds: 2 });
    await route.fulfill({
      json: {
        id: 42,
        patientId: 7,
        doctorId: 'doctor-e2e',
        consultationDate: '2026-08-23T09:30:00Z',
        status: 'Draft',
        durationSeconds: 2,
      },
    });
  });
  await page.route('**/api/consultation/42/audio', async (route) => {
    if (route.request().method() !== 'POST') return route.fallback();
    saveOrder.push('upload');
    const multipart = route.request().postData() ?? '';
    expect(multipart).toContain('audio/webm;codecs=opus');
    expect(multipart).toContain('durationSeconds');
    expect(multipart).toContain('2');
    await route.fulfill({
      json: {
        id: 42,
        patientId: 7,
        doctorId: 'doctor-e2e',
        consultationDate: '2026-08-23T09:30:00Z',
        status: 'AudioUploaded',
        durationSeconds: 2,
      },
    });
  });

  await page.goto('/record?patientId=7');
  await expect(page.getByRole('button', { name: 'Pause recording' })).toBeVisible();
  await expect(page.locator('.record-page')).toHaveCount(1);
  await expect(page.locator('.record-session')).toHaveCount(1);
  await expect(page.locator('.record-controls-dock')).toHaveCount(1);
  await expect(page.locator('.record-controls')).toHaveCount(1);
  await expect(page.locator('.app-shell')).toHaveClass(/app-shell--recording-locked/);

  await page.getByRole('button', { name: 'Pause recording' }).click();
  await expect(page.getByRole('button', { name: 'Resume recording' })).toBeVisible();
  let capture = await readCaptureContract(page);
  expect(capture.recorderMimeTypes.at(-1)).toBe('audio/webm;codecs=opus');
  expect(capture.trackEnabled.at(-1)).toBe(false);
  await expect(page.locator('.app-shell')).toHaveScreenshot('recording-paused.png', {
    animations: 'disabled',
    caret: 'hide',
  });

  await page.getByRole('button', { name: 'Resume recording' }).click();
  capture = await readCaptureContract(page);
  expect(capture.trackEnabled.at(-1)).toBe(true);

  await page.getByRole('button', { name: 'Stop recording' }).click();
  await page.waitForURL('**/patients/7/consultations/42');
  expect(saveOrder).toEqual(['create', 'upload']);
});

test('new consultation recorder keeps its independent capture contract', async ({
  authenticatedPage: page,
}) => {
  await installDeterministicMediaCapture(page);
  await page.clock.install({ time: new Date('2026-08-23T12:00:00Z') });
  const saveOrder: string[] = [];

  await page.route('**/api/patient/7', (route) => route.fulfill({ json: patient }));
  await page.route('**/api/consultation', async (route) => {
    if (route.request().method() !== 'POST') return route.fallback();
    saveOrder.push('create');
    expect(route.request().postDataJSON()).toMatchObject({ patientId: 7 });
    await route.fulfill({
      json: {
        id: 45,
        patientId: 7,
        doctorId: 'doctor-e2e',
        consultationDate: '2026-08-23T11:00:00Z',
        status: 'Draft',
      },
    });
  });
  await page.route('**/api/consultation/45/audio', async (route) => {
    saveOrder.push('upload');
    const multipart = route.request().postData() ?? '';
    expect(multipart).toContain('audio/webm;codecs=opus');
    expect(multipart).toContain('durationSeconds');
    expect(multipart).toContain('2');
    await route.fulfill({
      json: {
        id: 45,
        patientId: 7,
        doctorId: 'doctor-e2e',
        consultationDate: '2026-08-23T11:00:00Z',
        status: 'AudioUploaded',
        durationSeconds: 2,
      },
    });
  });

  await page.goto('/patients/7/consultations/new');
  await expect(page.locator('.capture-tabs')).toHaveCount(1);
  await expect(page.locator('.audio-recorder')).toHaveCount(1);
  await page.getByRole('button', { name: 'Start recording' }).click();
  await page.getByRole('button', { name: 'Pause' }).click();
  await expect(page.getByRole('button', { name: 'Resume' })).toBeVisible();

  let capture = await readCaptureContract(page);
  expect(capture.recorderMimeTypes.at(-1)).toBe('audio/webm;codecs=opus');
  expect(capture.trackEnabled.at(-1)).toBe(false);
  await expect(page.locator('.app-shell')).toHaveScreenshot(
    'new-consultation-recording-paused.png',
    { animations: 'disabled', caret: 'hide' },
  );

  await page.getByRole('button', { name: 'Resume' }).click();
  capture = await readCaptureContract(page);
  expect(capture.trackEnabled.at(-1)).toBe(true);
  await page.getByRole('button', { name: 'Stop' }).click();

  await page.waitForURL('**/patients/7/consultations/45');
  expect(saveOrder).toEqual(['create', 'upload']);
});
