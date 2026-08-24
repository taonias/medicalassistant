export const chatKeys = {
  action: (correlationId: string) => ['action', correlationId] as const,
  conversations: (patientId: number) => ['patient', patientId, 'conversations'] as const,
  conversationThread: (conversationId: number) => ['conversation', conversationId, 'thread'] as const,
};
