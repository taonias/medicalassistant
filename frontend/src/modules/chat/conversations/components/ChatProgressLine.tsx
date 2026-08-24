interface Props {
  message: string;
}

/** The ephemeral "what the system is doing" status line shown while awaiting an answer. */
export function ChatProgressLine({ message }: Props) {
  return (
    <div className="chat-progress" role="status" aria-live="polite">
      <span className="chat-progress__dots" aria-hidden>
        <span />
        <span />
        <span />
      </span>
      <span className="chat-progress__text">{message}</span>
    </div>
  );
}
