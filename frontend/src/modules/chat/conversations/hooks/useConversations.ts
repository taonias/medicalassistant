import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { conversationKeys } from '../queryKeys';
import type { AskChatRequest } from '../types';
import { chatApi } from '../api/chatApi';
import { conversationApi } from '../api/conversationApi';

export function useConversations(patientId?: number) {
  return useQuery({
    queryKey: conversationKeys.conversations(patientId ?? 0),
    queryFn: () => conversationApi.listForPatient(patientId!),
    enabled: Boolean(patientId),
  });
}

export function useConversationThread(conversationId?: number) {
  return useQuery({
    queryKey: conversationKeys.conversationThread(conversationId ?? 0),
    queryFn: () => conversationApi.getThread(conversationId!),
    enabled: Boolean(conversationId),
  });
}

export function useAskChat() {
  return useMutation({
    mutationFn: (request: AskChatRequest) => chatApi.ask(request),
  });
}

export function useRetryTurn() {
  return useMutation({
    mutationFn: (vars: { conversationId: number; messageId: number }) =>
      conversationApi.retry(vars.conversationId, vars.messageId),
  });
}

export function useCreateConversation(patientId?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (vars: { patientId?: number; consultationId?: number }) =>
      conversationApi.create(vars),
    onSuccess: () => {
      if (patientId) {
        void queryClient.invalidateQueries({ queryKey: conversationKeys.conversations(patientId) });
      }
    },
  });
}

export function useRenameConversation(patientId?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (vars: { conversationId: number; title: string }) =>
      conversationApi.rename(vars.conversationId, vars.title),
    onSuccess: () => {
      if (patientId) {
        void queryClient.invalidateQueries({ queryKey: conversationKeys.conversations(patientId) });
      }
    },
  });
}

export function useDeleteConversation(patientId?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (conversationId: number) => conversationApi.remove(conversationId),
    onSuccess: () => {
      if (patientId) {
        void queryClient.invalidateQueries({ queryKey: conversationKeys.conversations(patientId) });
      }
    },
  });
}
