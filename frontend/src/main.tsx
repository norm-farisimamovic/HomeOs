import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { queryClient } from '@/platform/query/queryClient'
import { router } from '@/app/router'
import { Toaster } from '@/shared/components/Toaster'
import { ConfirmHost } from '@/shared/components/ConfirmHost'
import { ErrorBoundary } from '@/shared/components/ErrorBoundary'
import '@/platform/i18n'
import './index.css'

const rootElement = document.getElementById('root')
if (!rootElement) throw new Error('Root element #root not found.')

createRoot(rootElement).render(
  <StrictMode>
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
        {/* App-wide overlays, above every route. */}
        <Toaster />
        <ConfirmHost />
      </QueryClientProvider>
    </ErrorBoundary>
  </StrictMode>,
)
