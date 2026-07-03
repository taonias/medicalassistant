import { Outlet } from 'react-router-dom';
import { Breadcrumbs } from '../shared/components/Breadcrumbs';
import { useBreadcrumbItems } from '../shared/hooks/useBreadcrumbItems';

export function AuthLayout() {
  const breadcrumbItems = useBreadcrumbItems();

  return (
    <div className="auth-layout">
      <div className="auth-layout__breadcrumbs">
        <Breadcrumbs items={breadcrumbItems} />
      </div>
      <Outlet />
    </div>
  );
}
