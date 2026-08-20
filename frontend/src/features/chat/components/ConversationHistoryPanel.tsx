import type { ConversationSummary } from '../../../shared/types/api';

interface Props {
  conversations: ConversationSummary[];
  activeId: number | null;
  onSelect: (id: number) => void;
  onNew: () => void;
  onDelete: (id: number) => void;
  onRename: (id: number, currentTitle: string) => void;
  onClose?: () => void;
}

/** History list of the doctor's conversations about this patient, with New + rename + delete. */
export function ConversationHistoryPanel({
  conversations,
  activeId,
  onSelect,
  onNew,
  onDelete,
  onRename,
  onClose,
}: Props) {
  return (
    <aside className="conversation-history">
      <div className="conversation-history__header">
        <span>Conversations</span>
        <div className="conversation-history__header-actions">
          <button type="button" className="button button--secondary button--small" onClick={onNew}>
            New
          </button>
          {onClose ? (
            <button
              type="button"
              className="conversation-history__close"
              aria-label="Close"
              onClick={onClose}
            >
              ×
            </button>
          ) : null}
        </div>
      </div>
      <div className="conversation-history__list">
        {conversations.length === 0 ? (
          <p className="muted conversation-history__empty">No conversations yet.</p>
        ) : (
          conversations.map((conversation) => (
            <div
              key={conversation.id}
              className={`conversation-item${
                activeId === conversation.id ? ' conversation-item--active' : ''
              }`}
            >
              <button
                type="button"
                className="conversation-item__title"
                onClick={() => onSelect(conversation.id)}
                onDoubleClick={() => onRename(conversation.id, conversation.title)}
                title={conversation.title}
              >
                {conversation.title}
              </button>
              <button
                type="button"
                className="conversation-item__action"
                aria-label="Rename conversation"
                title="Rename"
                onClick={() => onRename(conversation.id, conversation.title)}
              >
                ✎
              </button>
              <button
                type="button"
                className="conversation-item__delete"
                aria-label="Delete conversation"
                onClick={() => onDelete(conversation.id)}
              >
                ×
              </button>
            </div>
          ))
        )}
      </div>
    </aside>
  );
}
