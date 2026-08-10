export function LoadingScreen() {
  return (
    <main className="app-shell">
      <section className="setup-window compact-window" aria-labelledby="app-title">
        <div className="setup-main">
          <header className="masthead">
            <div>
              <h1 id="app-title">GameShell</h1>
              <p className="intro">Wczytywanie ustawień...</p>
            </div>
            <span className="status-pill">Start</span>
          </header>
        </div>
      </section>
    </main>
  );
}
