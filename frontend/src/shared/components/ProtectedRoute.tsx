import { useEffect, useState } from 'react';

import { Navigate, Outlet, useLocation } from 'react-router-dom';

import { useAuthStore } from '../../features/auth';



export function ProtectedRoute() {

  const token = useAuthStore((state) => state.token);

  const location = useLocation();

  const [hasHydrated, setHasHydrated] = useState(() => useAuthStore.persist.hasHydrated());



  useEffect(() => {

    if (useAuthStore.persist.hasHydrated()) {

      setHasHydrated(true);

      return;

    }



    return useAuthStore.persist.onFinishHydration(() => {

      setHasHydrated(true);

    });

  }, []);



  if (!hasHydrated) {

    return null;

  }



  if (!token) {

    return <Navigate to="/login" replace state={{ from: location.pathname }} />;

  }



  return <Outlet />;

}


