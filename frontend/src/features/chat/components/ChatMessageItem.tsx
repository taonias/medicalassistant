import type { ChatJob } from '../../../shared/types/api';

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  citations?: string[];
  suggestedActions?: string[];
  response?: ChatJob;
}

interface Props {
  message: ChatMessage;
}

export function ChatMessageItem({ message }: Props) {
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
              <span key={action} className="chip">
                {action}
              </span>
            ))}
          </div>
        ) : null}
      </div>
    </div>
  );
}
