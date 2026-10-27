import { Component, StrictMode, useEffect, useState, type ReactNode } from 'react';
import { createRoot } from 'react-dom/client';
import { I18nextProvider, useTranslation } from 'react-i18next';
import i18n from './i18n/i18n';
import { App } from './app/App';
import './styles/fonts.css';
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
  const { t } = useTranslation();
  const [startupError, setStartupError] = useState<string | null>(null);

  useEffect(() => {
    const handleError = (event: ErrorEvent) => {
      setStartupError(event.message || t('app.startupError'));
    };

    const handleUnhandledRejection = (event: PromiseRejectionEvent) => {
      const reason = event.reason instanceof Error ? event.reason.message : String(event.reason ?? 'Unknown error');
      setStartupError(reason || t('app.startupError'));
    };

    window.addEventListener('error', handleError);
    window.addEventListener('unhandledrejection', handleUnhandledRejection);

    return () => {
      window.removeEventListener('error', handleError);
      window.removeEventListener('unhandledrejection', handleUnhandledRejection);
    };
  }, [t]);

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
  const { t } = useTranslation();

  return (
    <main className="app-shell">
      <section className="setup-window compact-window" aria-labelledby="app-title">
        <div className="setup-main">
          <header className="masthead">
            <div>
                <h1 id="app-title">GameShell</h1>
              <p className="intro">{t('app.startupError')}</p>
            </div>
            <span className="status-pill has-error">{t('app.error')}</span>
          </header>
          <div className="startup-error-panel">
            <h2>{t('app.startupFailureTitle')}</h2>
            <p>{message}</p>
            <p>{t('app.startupFailureHint')}</p>
          </div>
        </div>
      </section>
    </main>
  );
}

createRoot(root).render(
  <StrictMode>
    <I18nextProvider i18n={i18n}>
      <BootstrapApp />
    </I18nextProvider>
  </StrictMode>
);
