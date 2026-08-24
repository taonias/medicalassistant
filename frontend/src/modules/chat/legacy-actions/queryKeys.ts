export const actionKeys = {
  action: (correlationId: string) => ['action', correlationId] as const,
};
