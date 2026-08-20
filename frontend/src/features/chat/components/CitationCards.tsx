import type { ChatCitation } from '../../../shared/types/api';

interface Props {
  messageKey: string;
  citations: ChatCitation[];
  activeLabel: string | null;
  onActivate: (label: string) => void;
}

/** The evidence panel under an answer: one expandable card per citation. */
export function CitationCards({ messageKey, citations, activeLabel, onActivate }: Props) {
  if (citations.length === 0) {
    return null;
  }

  return (
    <div className="citation-cards">
      <span className="citation-cards__heading">Evidence</span>
      {citations.map((citation) => (
        <div
          key={citation.label}
          id={`citation-${messageKey}-${citation.label}`}
          className={`citation-card${activeLabel === citation.label ? ' citation-card--active' : ''}`}
        >
          <button
            type="button"
            className="citation-card__label"
            onClick={() => onActivate(citation.label)}
          >
            {citation.label}
          </button>
          <div className="citation-card__body">
            <div className="citation-card__meta muted">
              <span className="citation-card__type">{citation.documentType}</span>
              {citation.documentDate
                ? ` · ${new Date(citation.documentDate).toLocaleDateString()}`
                : ''}
              {citation.sessionId ? ` · session ${citation.sessionId}` : ''}
              {` · score ${citation.score.toFixed(2)}`}
            </div>
            <blockquote className="citation-card__quote">{citation.quote}</blockquote>
          </div>
        </div>
      ))}
    </div>
  );
}
