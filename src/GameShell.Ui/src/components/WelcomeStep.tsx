export function WelcomeStep({ stepNumber }: { stepNumber: number }) {
  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Ustaw GameShell</h2>
      <p>
        Ustaw profile gamingowe, launchery i przełączanie między graniem a pracą.
      </p>
    </div>
  );
}
