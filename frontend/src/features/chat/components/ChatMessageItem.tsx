import type { ChatResponse } from '../types';

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  citations?: string[];
  suggestedActions?: string[];
  response?: ChatResponse;
}

interface Props {
  message: ChatMessage;
  onSelectAction?: (action: string) => void;
}

export function ChatMessageItem({ message, onSelectAction }: Props) {
  return (
    <div className={`chat-message chat-message--${message.role}`}>
      <div className="chat-message__bubble">
        <p>{message.content}</p>
        {message.citations && message.citations.length > 0 ? (
          <div className="chat-message__citations">
            {message.citations.map((citation) => (
              <span key={citation} className="chip">
                {citation}
              </span>
            ))}
          </div>
        ) : null}
        {message.suggestedActions && message.suggestedActions.length > 0 ? (
          <div className="chat-message__actions">
            {message.suggestedActions.map((action) => (
              <button
                key={action}
                type="button"
                className="button button--secondary button--small"
                onClick={() => onSelectAction?.(action)}
              >
                {action}
              </button>
            ))}
          </div>
        ) : null}
      </div>
    </div>
  );
}
