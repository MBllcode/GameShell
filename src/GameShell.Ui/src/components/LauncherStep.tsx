import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { LauncherIcon, type LauncherBrand } from './LauncherIcon';
import { sendBridgeCommand } from '../services/webViewBridge';
import type { DependencyStatus } from '../types/bridge';
import type { DisplayLayoutProfile } from '../types/bridge';

type LauncherType = 'Playnite Fullscreen' | 'Steam Big Picture' | 'Xbox App';

const launcherOptions: Array<{ value: LauncherType; title: string; descriptionKey: string; badge: string; brand: LauncherBrand }> = [
  {
    value: 'Playnite Fullscreen',
    title: 'Playnite Fullscreen',
    descriptionKey: 'launcher.playniteDescription',
    badge: 'PLAYNITE',
    brand: 'playnite'
  },
  {
    value: 'Steam Big Picture',
    title: 'Steam Big Picture',
    descriptionKey: 'launcher.steamDescription',
    badge: 'STEAM',
    brand: 'steam'
  },
  {
    value: 'Xbox App',
    title: 'Xbox App',
    descriptionKey: 'launcher.xboxDescription',
    badge: 'XBOX',
    brand: 'xbox'
  }
];

export function LauncherStep({
  stepNumber,
  gamingProfiles,
  selectedProfileName,
  launcher,
  setLauncher
}: {
  stepNumber: number;
  gamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  launcher: LauncherType;
  setLauncher: (launcher: LauncherType) => void;
}) {
  const { t } = useTranslation();
  const hasWorkingProfile = selectedProfileName.trim().length > 0;
  const selectedProfileLabel = selectedProfileName || t('profile.noProfileSelected');
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
            setDependencyError(t('launcher.dependencyCheckError'));
          }
        });
    };

    refreshDependencies();
    const refreshTimer = window.setInterval(refreshDependencies, 2000);

    return () => {
      isMounted = false;
      window.clearInterval(refreshTimer);
    };
  }, [t]);

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
      .catch(() => setDependencyError(t('launcher.downloadError')));
  };

  return (
    <div className="wizard-copy">
      <p className="eyebrow">{t('setup.step')} {stepNumber}</p>
      <h2>{t('launcher.title')}</h2>
      <p>{t('launcher.description')}</p>
      <div className="field-grid">
        <div className="profile-target-line" aria-live="polite">
          <span>{t('launcher.activeProfile')}</span>
          <strong>{selectedProfileLabel}</strong>
        </div>
      </div>
      <div className="launcher-grid" aria-label={t('launcher.title')} aria-disabled={!hasWorkingProfile}>
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
              <small>{t(item.descriptionKey)}</small>
              {dependency ? <span className={`launcher-status ${dependency.isDetected ? 'detected' : 'missing'}`}>{dependency.isDetected ? t('launcher.statusDetected') : t('launcher.statusMissing')}</span> : null}
            </button>
          );
        })}
      </div>
      {selectedDependency && !selectedDependency.isDetected ? (
        <div className="launcher-install-panel" role="alert">
          <div>
            <h3>{t('launcher.notDetected', { name: selectedDependency.name })}</h3>
            <p>{selectedDependency.message} {t('launcher.installPrompt', { name: selectedDependency.name })}</p>
          </div>
          <button className="secondary-button" type="button" onClick={openDownloadPage} disabled={!hasWorkingProfile}>
            {t('launcher.download', { name: selectedDependency.name })}
          </button>
        </div>
      ) : null}
      {dependencyError ? <p className="error-line" role="alert">{dependencyError}</p> : null}
    </div>
  );
}
