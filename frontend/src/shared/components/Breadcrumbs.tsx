import { Link } from 'react-router-dom';
import type { BreadcrumbItem } from '../hooks/useBreadcrumbItems';

interface Props {
  items: BreadcrumbItem[];
}

export function Breadcrumbs({ items }: Props) {
  if (items.length === 0) return null;

  return (
    <nav className="breadcrumbs" aria-label="Breadcrumb">
      <ol className="breadcrumbs__list">
        {items.map((item, index) => {
          const isLast = index === items.length - 1;

          return (
            <li key={`${item.label}-${index}`} className="breadcrumbs__item">
              {item.href && !isLast ? (
                <Link to={item.href} className="breadcrumbs__pill breadcrumbs__pill--link">
                  {item.label}
                </Link>
              ) : (
                <span
                  className={`breadcrumbs__pill${isLast ? ' breadcrumbs__pill--current' : ''}`}
                  aria-current={isLast ? 'page' : undefined}
                >
                  {item.label}
                </span>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
