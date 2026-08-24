export const conversationKeys = {
  conversations: (patientId: number) => ['patient', patientId, 'conversations'] as const,
  conversationThread: (conversationId: number) => ['conversation', conversationId, 'thread'] as const,
};
