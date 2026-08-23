import { expect, test } from '../fixtures/authenticatedPage';

test('general chat keeps the stateless send-and-answer journey', async ({
  authenticatedPage: page,
}) => {
  await page.route('**/api/chat/query', async (route) => {
    const request = route.request().postDataJSON();
    expect(request).toMatchObject({ message: 'What should I review today?' });
    expect(request.sessionId).toEqual(expect.any(String));
    await route.fulfill({
      json: {
        answer: 'Review the latest consultation and unresolved follow-up notes.',
        citations: [],
        suggestedActions: [],
      },
    });
  });

  await page.goto('/chat');
  await page.getByLabel('Chat message').fill('What should I review today?');
  await page.getByRole('button', { name: 'Send message' }).click();

  await expect(
    page.getByText('Review the latest consultation and unresolved follow-up notes.'),
  ).toBeVisible();
  await expect(page.locator('.app-shell')).toHaveScreenshot('general-chat-answer.png', {
    animations: 'disabled',
    caret: 'hide',
  });
});
