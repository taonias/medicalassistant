interface Props {
  actionType: string;
  summary: string;
  onConfirm: () => void;
  onCancel: () => void;
  isPending?: boolean;
}

export function ActionConfirmationModal({
  actionType,
  summary,
  onConfirm,
  onCancel,
  isPending,
}: Props) {
  return (
    <div className="modal-backdrop" role="presentation" onClick={onCancel}>
      <div
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="action-confirm-title"
        onClick={(event) => event.stopPropagation()}
      >
        <h2 id="action-confirm-title">Confirm action</h2>
        <p className="muted">Review before executing. This action will be logged.</p>
        <dl className="action-preview">
          <div>
            <dt>Action</dt>
            <dd>{actionType}</dd>
          </div>
          <div>
            <dt>Summary</dt>
            <dd>{summary}</dd>
          </div>
        </dl>
        <div className="modal__actions">
          <button type="button" className="button button--secondary" onClick={onCancel}>
            Cancel
          </button>
          <button
            type="button"
            className="button button--primary"
            onClick={onConfirm}
            disabled={isPending}
          >
            {isPending ? 'Executing…' : 'Confirm'}
          </button>
        </div>
      </div>
    </div>
  );
}
