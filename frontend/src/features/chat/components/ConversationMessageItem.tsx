import { useState } from 'react';
import { MessageState, type ChatCitation } from '../../../shared/types/api';
import { CitationCards } from './CitationCards';
import { CitationText } from './CitationText';

export interface UiMessage {
  key: string;
  role: 'user' | 'assistant';
  content: string;
  state?: number;
  refused?: boolean;
  failureReason?: string | null;
  messageId?: number;
  citations?: ChatCitation[];
}

interface Props {
  message: UiMessage;
  onRetry?: (messageId: number) => void;
  retrying?: boolean;
}

export function ConversationMessageItem({ message, onRetry, retrying }: Props) {
  const [activeLabel, setActiveLabel] = useState<string | null>(null);

  if (message.role === 'user') {
    return (
      <div className="chat-message chat-message--user">
        <div className="chat-message__bubble">
          <p>{message.content}</p>
        </div>
      </div>
    );
  }

  if (message.state === MessageState.Failed) {
    return (
      <div className="chat-message chat-message--assistant">
        <div className="chat-message__bubble chat-message__bubble--failed">
          <p className="chat-failed__reason">
            {message.failureReason ?? 'The answer could not be generated.'}
          </p>
          {message.messageId && onRetry ? (
            <button
              type="button"
              className="button button--secondary button--small"
              disabled={retrying}
              onClick={() => onRetry(message.messageId!)}
            >
              {retrying ? 'Retrying…' : 'Retry'}
            </button>
          ) : null}
        </div>
      </div>
    );
  }

  const citations = message.citations ?? [];
  const citationByLabel = new Map(citations.map((citation) => [citation.label, citation]));

  function activate(label: string) {
    setActiveLabel(label);
    document
      .getElementById(`citation-${message.key}-${label}`)
      ?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }

  return (
    <div className="chat-message chat-message--assistant">
      <div
        className={`chat-message__bubble${
          message.refused ? ' chat-message__bubble--refused' : ''
        }`}
      >
        <CitationText
          content={message.content}
          citationByLabel={citationByLabel}
          activeLabel={activeLabel}
          onActivate={activate}
        />
        <CitationCards
          messageKey={message.key}
          citations={citations}
          activeLabel={activeLabel}
          onActivate={activate}
        />
      </div>
    </div>
  );
}
