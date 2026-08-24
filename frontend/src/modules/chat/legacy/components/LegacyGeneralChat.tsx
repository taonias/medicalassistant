import { useRef, useState } from 'react';
import { SendIcon } from '../../../../app/shell/navigation/NavIcons';
import { useChatQuery } from '../hooks/useChatQuery';
import { ChatMessageItem, type ChatMessage } from './ChatMessageItem';

function createId() {
  return crypto.randomUUID();
}

/** The non-patient general chat: stateless single-shot query, no history or citations panel. */
export function LegacyGeneralChat() {
  const chatQuery = useChatQuery();
  const [sessionId] = useState(() => createId());
  const [input, setInput] = useState('');
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const listRef = useRef<HTMLDivElement>(null);

  async function sendMessage(message: string) {
    const trimmed = message.trim();
    if (!trimmed) return;

    setMessages((current) => [...current, { id: createId(), role: 'user', content: trimmed }]);
    setInput('');

    const response = await chatQuery.mutateAsync({ message: trimmed, sessionId });

    setMessages((current) => [
      ...current,
      {
        id: createId(),
        role: 'assistant',
        content: response.answer,
        citations: response.citations,
      },
    ]);
  }

  return (
    <div className="chat-page-layout">
      <div className="chat-page">
        <div className="chat-message-list" ref={listRef}>
          {messages.length === 0 ? (
            <p className="muted chat-message-list__empty">
              Start a conversation with Medical Assistant.
            </p>
          ) : (
            messages.map((message) => <ChatMessageItem key={message.id} message={message} />)
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
              placeholder="Ask anything…"
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
      </div>
    </div>
  );
}
