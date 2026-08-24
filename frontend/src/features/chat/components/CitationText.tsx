import type { ReactNode } from 'react';
import type { ChatCitation } from '../types';
import { CitationAnchor } from './CitationAnchor';

interface Props {
  content: string;
  citationByLabel: Map<string, ChatCitation>;
  activeLabel: string | null;
  onActivate: (label: string) => void;
}

const MARKER = /\[E\d+\]/g;

/** Renders answer prose, turning each verified [E#] marker into an interactive anchor. */
export function CitationText({ content, citationByLabel, activeLabel, onActivate }: Props) {
  const parts: ReactNode[] = [];
  const regex = new RegExp(MARKER);
  let lastIndex = 0;
  let key = 0;
  let match: RegExpExecArray | null;

  while ((match = regex.exec(content)) !== null) {
    if (match.index > lastIndex) {
      parts.push(content.slice(lastIndex, match.index));
    }
    const label = match[0].slice(1, -1); // "[E1]" -> "E1"
    parts.push(
      <CitationAnchor
        key={`c-${key++}`}
        label={label}
        citation={citationByLabel.get(label)}
        active={activeLabel === label}
        onActivate={onActivate}
      />,
    );
    lastIndex = match.index + match[0].length;
  }

  if (lastIndex < content.length) {
    parts.push(content.slice(lastIndex));
  }

  return <p className="chat-answer">{parts}</p>;
}
