import { expect, test as base, type Page } from '@playwright/test';

const apiPattern = 'https://localhost:7037/api/**';

export const doctorSession = {
  id: 'doctor-e2e',
  userName: 'dr.rivera',
  email: 'rivera@example.test',
  emailConfirmed: true,
  firstName: 'Alex',
  lastName: 'Rivera',
};

export async function authenticate(page: Page) {
  await page.addInitScript((session) => {
    window.localStorage.setItem(
      'medical-assistant-auth',
      JSON.stringify({
        state: { token: 'browser-contract-token', user: session, roles: ['Doctor'] },
        version: 0,
      }),
    );
  }, doctorSession);
}

export async function installApiBoundary(page: Page) {
  await page.route(apiPattern, async (route) => {
    const url = new URL(route.request().url());

    if (url.pathname === '/api/auth/session') {
      await route.fulfill({ json: doctorSession });
      return;
    }

    await route.fulfill({
      status: 501,
      contentType: 'application/json',
      body: JSON.stringify({ message: `Unhandled browser-contract API: ${url.pathname}` }),
    });
  });
}

export const test = base.extend<{ authenticatedPage: Page }>({
  authenticatedPage: async ({ page }, run) => {
    await authenticate(page);
    await installApiBoundary(page);
    await run(page);
  },
});

export { expect };
