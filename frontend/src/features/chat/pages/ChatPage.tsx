import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { SendIcon } from '../../../layouts/navigation/NavIcons';
import { ActionConfirmationModal } from '../components/ActionConfirmationModal';
import { ChatMessageItem, type ChatMessage } from '../components/ChatMessageItem';
import { useActionStatus, useChatQuery, useTriggerAction } from '../hooks/useChat';
import { usePatient } from '../../patients/hooks/usePatients';
import { ActionType } from '../../../shared/types/api';
import { formatPatientName } from '../../../shared/utils/format';

interface Props {
  patientId?: number;
  consultationId?: number;
}

function createId() {
  return crypto.randomUUID();
}

export function ChatPage({ patientId, consultationId }: Props) {
  const isGeneralChat = patientId === undefined;
  const { data: patient } = usePatient(patientId ?? 0);
  const chatQuery = useChatQuery();
  const triggerAction = useTriggerAction();
  const [sessionId] = useState(() => createId());
  const [input, setInput] = useState('');
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [pendingAction, setPendingAction] = useState<{
    actionType: string;
    summary: string;
  } | null>(null);
  const [activeAction, setActiveAction] = useState<{
    correlationId: string;
    actionType: string;
    summary: string;
  } | null>(null);
  const listRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages]);

  const actionStatusQuery = useActionStatus(activeAction?.correlationId, Boolean(activeAction));

  useEffect(() => {
    if (!activeAction?.correlationId) return;
    if (!actionStatusQuery.data) return;

    const statusLower = actionStatusQuery.data.status.toLowerCase();
    if (statusLower !== 'completed' && statusLower !== 'failed') return;

    let extra = '';
    if (actionStatusQuery.data.responsePayload) {
      try {
        const parsed = JSON.parse(actionStatusQuery.data.responsePayload) as {
          action?: string;
          to?: string[];
          subject?: string;
        };
        if (parsed.action === 'SendEmail') {
          const to = parsed.to?.length ? parsed.to.join(', ') : 'recipient(s)';
          extra = ` Email draft to ${to} (${parsed.subject ?? 'no subject'})`;
        }
      } catch {
        // Fall back to generic status message below.
      }
    }

    setMessages((current) => [
      ...current,
      {
        id: createId(),
        role: 'system',
        content:
          statusLower === 'completed'
            ? `Action "${activeAction.actionType}" completed.${extra}`
            : `Action "${activeAction.actionType}" failed: ${
                actionStatusQuery.data.failureReason ?? 'Unknown failure'
              }`,
      },
    ]);

    setActiveAction(null);
  }, [activeAction, actionStatusQuery.data]);

  async function sendMessage(message: string) {
    const trimmed = message.trim();
    if (!trimmed) return;

    setMessages((current) => [
      ...current,
      { id: createId(), role: 'user', content: trimmed },
    ]);
    setInput('');

    const response = await chatQuery.mutateAsync({
      ...(patientId !== undefined ? { patientId } : {}),
      ...(consultationId !== undefined ? { consultationId } : {}),
      message: trimmed,
      sessionId,
    });

    setMessages((current) => [
      ...current,
      {
        id: createId(),
        role: 'assistant',
        content: response.answer,
        citations: response.citations,
        suggestedActions: isGeneralChat ? [] : response.suggestedActions,
        response,
      },
    ]);
  }

  return (
    <div className="chat-page-layout">
      {isGeneralChat ? (
        <header className="chat-page-layout__header">
          <h1>Chat</h1>
        </header>
      ) : null}

      <div className="chat-page">
        {!isGeneralChat ? (
          <div className="chat-context-banner">
            <div>
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
            {consultationId && patientId ? (
              <Link
                to={`/patients/${patientId}/consultations/${consultationId}`}
                className="button button--secondary button--small"
              >
                View consultation
              </Link>
            ) : null}
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
              <ChatMessageItem
                key={message.id}
                message={message}
                onSelectAction={
                  isGeneralChat
                    ? undefined
                    : (action) => setPendingAction({ actionType: action, summary: action })
                }
              />
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
              disabled={chatQuery.isPending || !input.trim()}
              aria-label="Send message"
            >
              <SendIcon />
            </button>
          </div>
        </form>

        {pendingAction && patientId !== undefined ? (
          <ActionConfirmationModal
            actionType={pendingAction.actionType}
            summary={pendingAction.summary}
            isPending={triggerAction.isPending}
            onCancel={() => setPendingAction(null)}
            onConfirm={async () => {
              const created = await triggerAction.mutateAsync({
                actionType: ActionType.ChatInsight,
                patientId,
                consultationId,
                parametersJson: JSON.stringify({ action: pendingAction.actionType }),
              });

              setPendingAction(null);

              setMessages((current) => [
                ...current,
                {
                  id: createId(),
                  role: 'system',
                  content: `Action "${pendingAction.actionType}" submitted for processing.`,
                },
              ]);

              setActiveAction({
                correlationId: created.correlationId,
                actionType: pendingAction.actionType,
                summary: pendingAction.summary,
              });
            }}
          />
        ) : null}
      </div>
    </div>
  );
}
