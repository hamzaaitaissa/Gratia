// Base styles first, so the components' CSS modules come after them and win ties
import './styles/global.css';
import { QueryClientProvider } from '@tanstack/react-query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { createQueryClient } from './api/queries';
import { App } from './App';
import { ThemeModeProvider } from './theme/ThemeMode';

const queryClient = createQueryClient();

// A data router, so a page can hold the user back before they lose work (useBlocker, the import's review). App keeps
// declaring its routes with <Routes> under this one catch-all route.
const router = createBrowserRouter([{ path: '*', element: <App /> }]);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ThemeModeProvider>
        <RouterProvider router={router} />
      </ThemeModeProvider>
    </QueryClientProvider>
  </StrictMode>,
);
