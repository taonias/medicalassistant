interface Props {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  disabled?: boolean;
  className?: string;
}

function ChevronLeftIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M15 6 9 12l6 6"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function ChevronRightIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="m9 6 6 6-6 6"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function ConsultationPager({
  page,
  pageSize,
  totalCount,
  onPageChange,
  disabled,
  className,
}: Props) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  if (totalCount <= pageSize) return null;

  const canPrev = page > 1 && !disabled;
  const canNext = page < totalPages && !disabled;
  const from = (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);

  return (
    <nav
      className={['consultation-pager', className].filter(Boolean).join(' ')}
      aria-label="Consultation pages"
    >
      <p className="consultation-pager__meta muted">
        {from}–{to} of {totalCount}
      </p>
      <div className="consultation-pager__controls">
        <button
          type="button"
          className="consultation-pager__nav"
          disabled={!canPrev}
          aria-label="Previous page"
          title="Previous page"
          onClick={() => onPageChange(page - 1)}
        >
          <ChevronLeftIcon />
        </button>
        <span className="consultation-pager__page" aria-current="page">
          {page}
          <span className="consultation-pager__page-sep">/</span>
          {totalPages}
        </span>
        <button
          type="button"
          className="consultation-pager__nav"
          disabled={!canNext}
          aria-label="Next page"
          title="Next page"
          onClick={() => onPageChange(page + 1)}
        >
          <ChevronRightIcon />
        </button>
      </div>
    </nav>
  );
}
