export function WelcomeStep({ stepNumber }: { stepNumber: number }) {
  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Konfiguracja GameShell</h2>
      <p>
        To jest pierwsza konfiguracja GameShell. Ustaw pierwszy profil gamingowy, aktywację launchera i przejścia między trybem pracy a rozrywką. Po pierwszej konfiguracji będzie można dodawać kolejne profile z poziomu dashboardu w dowolnym momencie.
      </p>
    </div>
  );
}
