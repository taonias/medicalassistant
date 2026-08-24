import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { SendIcon } from '../../../layouts/navigation/NavIcons';
import { queryKeys } from '../../../shared/constants/queryKeys';
import {
  MessageRole,
  MessageState,
  type ApiError,
  type AskChatResponse,
  type ConversationThread,
} from '../../../shared/types/api';
import { formatPatientName } from '../../../shared/utils/format';
import { usePatient } from '../../patients';
import { conversationApi } from '../api/conversationApi';
import {
  useAskChat,
  useConversations,
  useDeleteConversation,
  useRenameConversation,
  useRetryTurn,
} from '../hooks/useConversations';
import { useChatProgress } from '../hooks/useChatProgress';
import { ChatProgressLine } from './ChatProgressLine';
import { ConversationHistoryPanel } from './ConversationHistoryPanel';
import { ConversationMessageItem, type UiMessage } from './ConversationMessageItem';

interface Props {
  patientId: number;
  consultationId?: number;
}

function assistantFromResponse(response: AskChatResponse): UiMessage {
  return {
    key: `a-${response.askId}-${response.messageId}`,
    role: 'assistant',
    content: response.answer,
    state: response.state,
    refused: response.refused,
    failureReason: response.failureReason,
    messageId: response.messageId,
    citations: response.citations,
  };
}

function threadToMessages(thread: ConversationThread): UiMessage[] {
  return thread.messages
    .filter((m) => !(m.role === MessageRole.Assistant && m.state === MessageState.Pending))
    .map((m) =>
      m.role === MessageRole.User
        ? { key: `m${m.id}`, role: 'user', content: m.content }
        : {
            key: `m${m.id}`,
            role: 'assistant',
            content: m.content,
            state: m.state,
            refused: m.state === MessageState.Refused,
            failureReason: m.failureReason,
            messageId: m.id,
            citations: m.citations,
          },
    );
}

export function ConversationChat({ patientId, consultationId }: Props) {
  const { data: patient } = usePatient(patientId);
  const { data: conversations = [] } = useConversations(patientId);
  const queryClient = useQueryClient();

  const ask = useAskChat();
  const retryTurn = useRetryTurn();
  const deleteConversation = useDeleteConversation(patientId);
  const renameConversation = useRenameConversation(patientId);
  const { progress, clear: clearProgress } = useChatProgress();

  const [conversationId, setConversationId] = useState<number | null>(null);
  const [messages, setMessages] = useState<UiMessage[]>([]);
  const [input, setInput] = useState('');
  const [pendingAskId, setPendingAskId] = useState<string | null>(null);
  const [retryingId, setRetryingId] = useState<number | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const listRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages, pendingAskId]);

  async function selectConversation(id: number) {
    setConversationId(id);
    setInput('');
    const thread = await conversationApi.getThread(id);
    setMessages(threadToMessages(thread));
  }

  function startNewConversation() {
    setConversationId(null);
    setMessages([]);
    setInput('');
  }

  async function handleDelete(id: number) {
    await deleteConversation.mutateAsync(id);
    if (id === conversationId) {
      startNewConversation();
    }
  }

  async function handleRename(id: number, currentTitle: string) {
    const title = window.prompt('Rename conversation', currentTitle)?.trim();
    if (!title || title === currentTitle) return;
    await renameConversation.mutateAsync({ conversationId: id, title });
  }

  async function send(question: string) {
    const trimmed = question.trim();
    if (!trimmed || pendingAskId) return;

    const askId = crypto.randomUUID();
    setInput('');
    setMessages((current) => [...current, { key: `u-${askId}`, role: 'user', content: trimmed }]);
    setPendingAskId(askId);
    clearProgress();

    try {
      const response = await ask.mutateAsync({
        conversationId: conversationId ?? undefined,
        patientId,
        consultationId,
        question: trimmed,
        askId,
      });
      setConversationId(response.conversationId);
      setMessages((current) => [...current, assistantFromResponse(response)]);
      void queryClient.invalidateQueries({ queryKey: queryKeys.conversations(patientId) });
    } catch (error) {
      // A non-200 (validation/auth/network) — no persisted turn to retry.
      setMessages((current) => [
        ...current,
        {
          key: `err-${askId}`,
          role: 'assistant',
          content: '',
          state: MessageState.Failed,
          failureReason: (error as ApiError)?.message ?? 'The request failed.',
        },
      ]);
    } finally {
      setPendingAskId(null);
    }
  }

  async function handleRetry(messageId: number) {
    if (!conversationId) return;
    setRetryingId(messageId);
    try {
      const response = await retryTurn.mutateAsync({ conversationId, messageId });
      setMessages((current) =>
        current.map((m) => (m.messageId === messageId ? assistantFromResponse(response) : m)),
      );
    } finally {
      setRetryingId(null);
    }
  }

  const progressMessage =
    pendingAskId && progress?.askId === pendingAskId ? progress.message : 'Thinking…';

  return (
    <>
      <div className="chat-page-layout">
        <div className="chat-page conversation-main conversation-main--full">
        <div className="chat-context-banner">
          <div>
            <span className="chat-context-banner__eyebrow muted">Chatting about</span>
            <strong>
              {patient
                ? formatPatientName(patient.firstName, patient.lastName)
                : `Patient #${patientId}`}
            </strong>
            {consultationId ? (
              <span className="muted"> · Consultation #{consultationId}</span>
            ) : (
              <span className="muted"> · Full patient history context</span>
            )}
          </div>
          <div className="chat-context-banner__actions">
            <button
              type="button"
              className="button button--secondary button--small"
              onClick={() => setHistoryOpen(true)}
            >
              Conversations
            </button>
            {consultationId ? (
              <Link
                to={`/patients/${patientId}/consultations/${consultationId}`}
                className="button button--secondary button--small"
              >
                View consultation
              </Link>
            ) : null}
          </div>
        </div>

        <div className="chat-message-list" ref={listRef}>
          {messages.length === 0 && !pendingAskId ? (
            <p className="muted chat-message-list__empty">
              Ask about this patient's history, transcripts, or structured data.
            </p>
          ) : (
            messages.map((message) => (
              <ConversationMessageItem
                key={message.key}
                message={message}
                onRetry={(id) => void handleRetry(id)}
                retrying={retryingId === message.messageId}
              />
            ))
          )}
          {pendingAskId ? <ChatProgressLine message={progressMessage} /> : null}
        </div>

        <form
          className="chat-input"
          onSubmit={(event) => {
            event.preventDefault();
            void send(input);
          }}
        >
          <div className="chat-input__field">
            <input
              value={input}
              onChange={(event) => setInput(event.target.value)}
              placeholder="Ask about this patient…"
              aria-label="Chat message"
            />
            <button
              type="submit"
              className="chat-input__send"
              disabled={Boolean(pendingAskId) || !input.trim()}
              aria-label="Send message"
            >
              <SendIcon />
            </button>
          </div>
        </form>
        </div>
      </div>

      {historyOpen ? (
        <div className="modal-backdrop" role="presentation" onClick={() => setHistoryOpen(false)}>
          <div
            className="modal modal--history"
            role="dialog"
            aria-modal="true"
            aria-label="Conversations"
            onClick={(event) => event.stopPropagation()}
          >
            <ConversationHistoryPanel
              conversations={conversations}
              activeId={conversationId}
              onSelect={(id) => {
                void selectConversation(id);
                setHistoryOpen(false);
              }}
              onNew={() => {
                startNewConversation();
                setHistoryOpen(false);
              }}
              onDelete={(id) => void handleDelete(id)}
              onRename={(id, title) => void handleRename(id, title)}
              onClose={() => setHistoryOpen(false)}
            />
          </div>
        </div>
      ) : null}
    </>
  );
}
