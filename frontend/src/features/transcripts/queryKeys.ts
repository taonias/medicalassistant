export const transcriptKeys = {
  transcript: (consultationId: number) => ['consultation', consultationId, 'transcript'] as const,
};
