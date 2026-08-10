import type { DisplayLayoutProfile } from '../types/bridge';

type LauncherType = 'Playnite Fullscreen' | 'Steam Big Picture' | 'Xbox App';

const launcherOptions: Array<{ value: LauncherType; title: string; description: string; badge: string }> = [
  {
    value: 'Playnite Fullscreen',
    title: 'Playnite Fullscreen',
    description: 'Wygodna obsługa wielu launcherów kontrolerem.',
    badge: 'Polecany'
  },
  {
    value: 'Steam Big Picture',
    title: 'Steam Big Picture',
    description: 'Uruchamia Steam w trybie Big Picture.',
    badge: 'Steam'
  },
  {
    value: 'Xbox App',
    title: 'Xbox App',
    description: 'Otwiera aplikację Xbox i bibliotekę Game Pass.',
    badge: 'Xbox'
  }
];

export function LauncherStep({
  stepNumber,
  pcGamingProfiles,
  selectedProfileName,
  launcher,
  setLauncher
}: {
  stepNumber: number;
  pcGamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  launcher: LauncherType;
  setLauncher: (launcher: LauncherType) => void;
}) {
  const hasProfiles = pcGamingProfiles.length > 0;
  const selectedProfileLabel = selectedProfileName || 'Brak zaznaczonego profilu';

  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Launcher profilu</h2>
      <p>Wybierz aplikację uruchamianą z tym profilem.</p>
      <div className="field-grid">
        <div className="profile-target-line" aria-live="polite">
          <span>Aktywny profil</span>
          <strong>{selectedProfileLabel}</strong>
        </div>
      </div>
      <div className="launcher-grid" aria-label="Launcher" aria-disabled={!hasProfiles}>
        {launcherOptions.map((item) => (
          <button
            className={`launcher-card ${launcher === item.value ? 'selected' : ''}`}
            key={item.value}
            type="button"
            onClick={() => setLauncher(item.value)}
            disabled={!hasProfiles}
          >
            <span className="launcher-badge">{item.badge}</span>
            <strong>{item.title}</strong>
            <small>{item.description}</small>
          </button>
        ))}
      </div>
    </div>
  );
}
