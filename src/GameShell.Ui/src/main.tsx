import { Component, StrictMode, useEffect, useState, type ReactNode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import './styles.css';

const root = document.getElementById('root');

if (!root) {
  throw new Error('Root element was not found.');
}

class AppErrorBoundary extends Component<{ children: ReactNode }, { error: string | null }> {
  constructor(props: { children: ReactNode }) {
    super(props);
    this.state = { error: null };
  }

  static getDerivedStateFromError(error: Error) {
    return { error: error.message };
  }

  render() {
    if (this.state.error) {
      return <StartupErrorScreen message={this.state.error} />;
    }

    return this.props.children;
  }
}

function BootstrapApp() {
  const [startupError, setStartupError] = useState<string | null>(null);

  useEffect(() => {
    const handleError = (event: ErrorEvent) => {
      setStartupError(event.message || 'Wystąpił nieoczekiwany błąd podczas uruchamiania aplikacji.');
    };

    const handleUnhandledRejection = (event: PromiseRejectionEvent) => {
      const reason = event.reason instanceof Error ? event.reason.message : String(event.reason ?? 'Unknown error');
      setStartupError(reason || 'Wystąpił nieoczekiwany błąd podczas uruchamiania aplikacji.');
    };

    window.addEventListener('error', handleError);
    window.addEventListener('unhandledrejection', handleUnhandledRejection);

    return () => {
      window.removeEventListener('error', handleError);
      window.removeEventListener('unhandledrejection', handleUnhandledRejection);
    };
  }, []);

  if (startupError) {
    return <StartupErrorScreen message={startupError} />;
  }

  return (
    <AppErrorBoundary>
      <App />
    </AppErrorBoundary>
  );
}

function StartupErrorScreen({ message }: { message: string }) {
  return (
    <main className="app-shell">
      <section className="setup-window compact-window" aria-labelledby="app-title">
        <div className="setup-main">
          <header className="masthead">
            <div>
              <h1 id="app-title">GameShell</h1>
              <p className="intro">Aplikacja nie mogła się uruchomić poprawnie.</p>
            </div>
            <span className="status-pill has-error">Błąd</span>
          </header>
          <div className="startup-error-panel">
            <h2>Wystąpił błąd startowy</h2>
            <p>{message}</p>
            <p>Sprawdź log: %LOCALAPPDATA%\GameShell\Logs\gameshell-YYYY-MM-DD.log</p>
          </div>
        </div>
      </section>
    </main>
  );
}

createRoot(root).render(
  <StrictMode>
    <BootstrapApp />
  </StrictMode>
);
