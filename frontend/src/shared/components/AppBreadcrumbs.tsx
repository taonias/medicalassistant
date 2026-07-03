import { Breadcrumbs } from './Breadcrumbs';
import { useBreadcrumbItems } from '../hooks/useBreadcrumbItems';

export function AppBreadcrumbs() {
  const items = useBreadcrumbItems();

  return (
    <div className="app-breadcrumbs">
      <Breadcrumbs items={items} />
    </div>
  );
}
