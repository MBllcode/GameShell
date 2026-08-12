import { useEffect, useState } from 'react';
import { LauncherIcon, type LauncherBrand } from './LauncherIcon';
import { sendBridgeCommand } from '../services/webViewBridge';
import type { DependencyStatus } from '../types/bridge';
import type { DisplayLayoutProfile } from '../types/bridge';

type LauncherType = 'Playnite Fullscreen' | 'Steam Big Picture' | 'Xbox App';

const launcherOptions: Array<{ value: LauncherType; title: string; description: string; badge: string; brand: LauncherBrand }> = [
  {
    value: 'Playnite Fullscreen',
    title: 'Playnite Fullscreen',
    description: 'Łączy gry z różnych launcherów w jednej bibliotece.',
    badge: 'PLAYNITE',
    brand: 'playnite'
  },
  {
    value: 'Steam Big Picture',
    title: 'Steam Big Picture',
    description: 'Uruchamia Steam w trybie Big Picture.',
    badge: 'STEAM',
    brand: 'steam'
  },
  {
    value: 'Xbox App',
    title: 'Xbox App',
    description: 'Otwiera aplikację Xbox i bibliotekę Game Pass.',
    badge: 'XBOX',
    brand: 'xbox'
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
  const hasWorkingProfile = selectedProfileName.trim().length > 0;
  const selectedProfileLabel = selectedProfileName || 'Brak zaznaczonego profilu';
  const [dependencies, setDependencies] = useState<DependencyStatus[]>([]);
  const [dependencyError, setDependencyError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;
    const refreshDependencies = () => {
      void sendBridgeCommand<DependencyStatus[]>('getDependencies')
        .then((nextDependencies) => {
          if (isMounted) {
            setDependencies(nextDependencies);
            setDependencyError(null);
          }
        })
        .catch(() => {
          if (isMounted) {
            setDependencyError('Nie udało się sprawdzić dostępności launcherów.');
          }
        });
    };

    refreshDependencies();
    const refreshTimer = window.setInterval(refreshDependencies, 2000);

    return () => {
      isMounted = false;
      window.clearInterval(refreshTimer);
    };
  }, []);

  const selectedLauncherName = launcher === 'Playnite Fullscreen'
    ? 'Playnite'
    : launcher === 'Steam Big Picture'
      ? 'Steam'
      : 'Xbox App';
  const getDependencyName = (launcherType: LauncherType) => launcherType === 'Playnite Fullscreen'
    ? 'Playnite'
    : launcherType === 'Steam Big Picture'
      ? 'Steam'
      : 'Xbox App';
  const selectedDependency = dependencies.find((dependency) => dependency.name === selectedLauncherName);

  const openDownloadPage = () => {
    void sendBridgeCommand('openDependencyDownloadPage', { name: selectedLauncherName })
      .catch(() => setDependencyError('Nie udało się otworzyć strony pobierania launchera.'));
  };

  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Launcher profilu</h2>
      <p>Wybierz aplikację aktywowaną wraz z tym profilem. Po pierwszej konfiguracji będzie można zmieniać launcher i dodawać kolejne profile bez utraty istniejących ustawień.</p>
      <div className="field-grid">
        <div className="profile-target-line" aria-live="polite">
          <span>Aktywny profil</span>
          <strong>{selectedProfileLabel}</strong>
        </div>
      </div>
      <div className="launcher-grid" aria-label="Launcher" aria-disabled={!hasWorkingProfile}>
        {launcherOptions.map((item) => {
          const dependency = dependencies.find((candidate) => candidate.name === getDependencyName(item.value));

          return (
            <button
              className={`launcher-card launcher-card--${item.brand} ${launcher === item.value ? 'selected' : ''}`}
              key={item.value}
              type="button"
              onClick={() => setLauncher(item.value)}
              disabled={!hasWorkingProfile}
            >
              <span className="launcher-card-heading">
                <LauncherIcon brand={item.brand} />
                <span className="launcher-badge">{item.badge}</span>
              </span>
              <strong>{item.title}</strong>
              <small>{item.description}</small>
              {dependency ? <span className={`launcher-status ${dependency.isDetected ? 'detected' : 'missing'}`}>{dependency.isDetected ? 'WYKRYTO' : 'NIE WYKRYTO'}</span> : null}
            </button>
          );
        })}
      </div>
      {selectedDependency && !selectedDependency.isDetected ? (
        <div className="launcher-install-panel" role="alert">
          <div>
            <h3>Nie wykryto: {selectedDependency.name}</h3>
            <p>{selectedDependency.message} Zainstaluj launcher, aby móc uruchomić ten profil.</p>
          </div>
          <button className="secondary-button" type="button" onClick={openDownloadPage} disabled={!hasWorkingProfile}>
            Pobierz {selectedDependency.name}
          </button>
        </div>
      ) : null}
      {dependencyError ? <p className="error-line" role="alert">{dependencyError}</p> : null}
    </div>
  );
}
