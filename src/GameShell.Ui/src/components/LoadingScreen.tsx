import { useTranslation } from 'react-i18next';

export function LoadingScreen() {
  const { t } = useTranslation();

  return (
    <main className="app-shell">
      <section className="setup-window compact-window" aria-labelledby="app-title">
        <div className="setup-main">
          <header className="masthead">
            <div>
              <h1 id="app-title">GameShell</h1>
              <p className="intro">{t('app.loading')}</p>
            </div>
            <span className="status-pill">{t('setup.start')}</span>
          </header>
        </div>
      </section>
    </main>
  );
}
