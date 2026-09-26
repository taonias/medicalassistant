import { useEffect, useRef, useState } from 'react';
import { SendIcon } from '../../../layouts/navigation/NavIcons';
import { ChatMessageItem, type ChatMessage } from '../components/ChatMessageItem';
import { useChatQuery, useChatStatus } from '../hooks/useChat';
import { usePatient } from '../../patients/hooks/usePatients';
import { formatPatientName } from '../../../shared/utils/format';

interface Props {
  patientId?: number;
}

function createId() {
  return crypto.randomUUID();
}

export function ChatPage({ patientId }: Props) {
  const isGeneralChat = patientId === undefined;
  const { data: patient } = usePatient(patientId ?? 0);
  const chatQuery = useChatQuery();
  const [sessionId] = useState(() => createId());
  const [input, setInput] = useState('');
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [activeChatId, setActiveChatId] = useState<string | null>(null);
  const [pendingAssistantId, setPendingAssistantId] = useState<string | null>(null);
  const listRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages]);

  const chatStatusQuery = useChatStatus(activeChatId ?? undefined, Boolean(activeChatId));

  useEffect(() => {
    if (!activeChatId || !pendingAssistantId || !chatStatusQuery.data) return;

    const statusLower = chatStatusQuery.data.status.toLowerCase();
    if (statusLower !== 'completed' && statusLower !== 'failed') return;

    setMessages((current) =>
      current.map((message) => {
        if (message.id !== pendingAssistantId) return message;
        if (statusLower === 'completed') {
          return {
            ...message,
            role: 'assistant',
            content: chatStatusQuery.data.answer ?? '',
            citations: chatStatusQuery.data.citations,
            suggestedActions: isGeneralChat ? [] : chatStatusQuery.data.suggestedActions,
            response: chatStatusQuery.data,
          };
        }

        return {
          ...message,
          role: 'system',
          content: `Chat failed: ${chatStatusQuery.data.failureReason ?? 'Unknown failure'}`,
        };
      }),
    );

    setActiveChatId(null);
    setPendingAssistantId(null);
  }, [activeChatId, pendingAssistantId, chatStatusQuery.data, isGeneralChat]);

  async function sendMessage(message: string) {
    const trimmed = message.trim();
    if (!trimmed) return;

    const assistantId = createId();
    setMessages((current) => [
      ...current,
      { id: createId(), role: 'user', content: trimmed },
      { id: assistantId, role: 'assistant', content: 'Working…' },
    ]);
    setInput('');

    try {
      const job = await chatQuery.mutateAsync({
        ...(patientId !== undefined ? { patientId } : {}),
        message: trimmed,
        sessionId,
      });

      setPendingAssistantId(assistantId);
      setActiveChatId(job.correlationId);
    } catch {
      setMessages((current) =>
        current.map((item) =>
          item.id === assistantId
            ? { ...item, role: 'system', content: 'Failed to queue chat request.' }
            : item,
        ),
      );
    }
  }

  return (
    <div className="chat-page-layout">
      <div className="chat-page">
        {!isGeneralChat ? (
          <div className="chat-context-banner">
            <div>
              <span className="chat-context-banner__eyebrow muted">Chatting for</span>
              <strong>
                {patient
                  ? formatPatientName(patient.firstName, patient.lastName)
                  : `Patient #${patientId}`}
              </strong>
              <span className="muted"> · Full patient history context</span>
            </div>
          </div>
        ) : null}

        <div className="chat-message-list" ref={listRef}>
          {messages.length === 0 ? (
            <p className="muted chat-message-list__empty">
              {isGeneralChat
                ? 'Start a conversation with Medical Assistant.'
                : "Ask about this patient's history, transcripts, or structured data."}
            </p>
          ) : (
            messages.map((message) => (
              <ChatMessageItem key={message.id} message={message} />
            ))
          )}
        </div>

        <form
          className="chat-input"
          onSubmit={(event) => {
            event.preventDefault();
            void sendMessage(input);
          }}
        >
          <div className="chat-input__field">
            <input
              value={input}
              onChange={(event) => setInput(event.target.value)}
              placeholder={isGeneralChat ? 'Ask anything…' : 'Ask about this patient…'}
              aria-label="Chat message"
            />
            <button
              type="submit"
              className="chat-input__send"
              disabled={chatQuery.isPending || Boolean(activeChatId) || !input.trim()}
              aria-label="Send message"
            >
              <SendIcon />
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
