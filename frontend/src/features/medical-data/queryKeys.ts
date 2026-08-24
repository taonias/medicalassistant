export const structuredDataKeys = {
  structuredData: (consultationId: number) => ['consultation', consultationId, 'structured-data'] as const,
};
