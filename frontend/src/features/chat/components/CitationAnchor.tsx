import type { ChatCitation } from '../../../shared/types/api';

interface Props {
  label: string;
  citation?: ChatCitation;
  active: boolean;
  onActivate: (label: string) => void;
}

/** An inline [E#] reference: hover reveals the verbatim quote; click expands its card. */
export function CitationAnchor({ label, citation, active, onActivate }: Props) {
  return (
    <span className="citation-anchor-wrap">
      <button
        type="button"
        className={`citation-anchor${active ? ' citation-anchor--active' : ''}`}
        onClick={() => onActivate(label)}
        aria-label={`Evidence ${label}`}
      >
        {label}
      </button>
      {citation ? (
        <span className="citation-popover" role="tooltip">
          <span className="citation-popover__meta">
            {citation.documentType}
            {citation.documentDate
              ? ` · ${new Date(citation.documentDate).toLocaleDateString()}`
              : ''}
          </span>
          <span className="citation-popover__quote">“{citation.quote}”</span>
        </span>
      ) : null}
    </span>
  );
}
