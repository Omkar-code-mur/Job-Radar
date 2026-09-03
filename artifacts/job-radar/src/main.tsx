import { createRoot } from 'react-dom/client';

import RoleAwareApp from './RoleAwareApp';
import AuthGate from './auth/AuthGate';
import { ErrorBoundary } from '@/components/error-boundary';

import './index.css';

window.addEventListener('error', (event) => {
  console.error('[JobRadar UI] Unhandled browser error', {
    message: event.message,
    filename: event.filename,
    line: event.lineno,
    column: event.colno,
    error: event.error,
  });
});

window.addEventListener('unhandledrejection', (event) => {
  console.error('[JobRadar UI] Unhandled promise rejection', {
    reason: event.reason,
  });
});

createRoot(document.getElementById('root')!, {
  onCaughtError: (error, errorInfo) => {
    console.error('[JobRadar UI] React caught an error', {
      error,
      componentStack: errorInfo.componentStack,
    });
  },
}).render(
  <ErrorBoundary>
    <AuthGate>
      <RoleAwareApp />
    </AuthGate>
  </ErrorBoundary>,
);
