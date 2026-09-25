import { QueryClientProvider } from '@tanstack/react-query';

import { type ReactNode, useEffect } from 'react';

import {

  applyThemeToDocument,

  useThemeStore,

} from '../features/theme/store/themeStore';

import { queryClient } from '../shared/api/queryClient';



function ThemeInitializer() {

  const theme = useThemeStore((state) => state.theme);



  useEffect(() => {

    applyThemeToDocument(theme);

  }, [theme]);



  return null;

}



export function AppProviders({ children }: { children: ReactNode }) {

  return (

    <QueryClientProvider client={queryClient}>

      <ThemeInitializer />

      {children}

    </QueryClientProvider>

  );

}
