import React from 'react';

export interface PaginationProps {
  total: number;
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (pageSize: number) => void;
  pageSizeOptions?: number[];
  label?: string;
}

export default function Pagination({
  total,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = [10, 25, 50, 100],
  label
}: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  // Show at most 7 page numbers centered around the current page
  const pageNumbers = (() => {
    const start = Math.max(1, page - 3);
    const end = Math.min(totalPages, start + 6);
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  })();

  const startIndex = (page - 1) * pageSize;
  const endIndex = Math.min(page * pageSize, total);

  return (
    <div className="pagination">
      <div className="pagination__left">
        <span>
          Showing {total === 0 ? 0 : startIndex + 1}–{endIndex} of {total}{label ? ` ${label}` : ''}
        </span>
        {onPageSizeChange && (
          <select
            className="pagination__size-select"
            value={pageSize}
            onChange={e => onPageSizeChange(Number(e.target.value))}
          >
            {pageSizeOptions.map(o => (
              <option key={o} value={o}>{o} / page</option>
            ))}
          </select>
        )}
      </div>
      <div className="pagination__pages">
        <button
          className="pagination__page-btn"
          onClick={() => onPageChange(page - 1)}
          disabled={page <= 1}
          type="button"
        >
          <span className="material-symbols-outlined" style={{ fontSize: 14 }}>chevron_left</span>
        </button>
        {pageNumbers.map(p => (
          <button
            key={p}
            type="button"
            className={`pagination__page-btn${p === page ? ' pagination__page-btn--active' : ''}`}
            onClick={() => onPageChange(p)}
          >
            {p}
          </button>
        ))}
        <button
          className="pagination__page-btn"
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages}
          type="button"
        >
          <span className="material-symbols-outlined" style={{ fontSize: 14 }}>chevron_right</span>
        </button>
      </div>
    </div>
  );
}
