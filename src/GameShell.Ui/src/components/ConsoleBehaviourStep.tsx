import type { DisplayLayoutProfile } from '../types/bridge';
import { SettingRow } from './SettingRow';
import { SettingsSection } from './SettingsSection';

type PcGamingBehaviour = {
  hideDesktopIcons: boolean;
  hideTaskbar: boolean;
  closeVisibleWindows: boolean;
  minimizeRemainingWindows: boolean;
  moveCursorToCorner: boolean;
  hideCursor: boolean;
  startLauncher: boolean;
  bringLauncherToForeground: boolean;
};

type PcGamingBehaviourKey = keyof PcGamingBehaviour;

const pcGamingBehaviourOptions: Array<{ key: PcGamingBehaviourKey; label: string; description?: string }> = [
  { key: 'hideDesktopIcons', label: 'Ukryj ikony pulpitu' },
  { key: 'hideTaskbar', label: 'Ukryj pasek zadań' },
  { key: 'closeVisibleWindows', label: 'Zamknij widoczne okna aplikacji' },
  { key: 'minimizeRemainingWindows', label: 'Zminimalizuj pozostałe okna' },
  { key: 'moveCursorToCorner', label: 'Przesuń kursor w róg' },
  { key: 'hideCursor', label: 'Ukryj kursor' },
  { key: 'startLauncher', label: 'Uruchom launcher' },
  { key: 'bringLauncherToForeground', label: 'Przenieś launcher na pierwszy plan' }
];

export function ConsoleBehaviourStep({
  stepNumber,
  pcGamingProfiles,
  selectedProfileName,
  pcGamingBehaviour,
  onChangeBehaviourFlag
}: {
  stepNumber: number;
  pcGamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  pcGamingBehaviour: PcGamingBehaviour;
  onChangeBehaviourFlag: (key: PcGamingBehaviourKey, value: boolean) => void;
}) {
  const hasProfiles = pcGamingProfiles.length > 0;
  const selectedProfileLabel = selectedProfileName || 'Brak zaznaczonego profilu';

  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Zachowanie profilu</h2>
      <p>Ustaw wygląd pulpitu po uruchomieniu profilu.</p>
      <div className="field-grid">
        <div className="profile-target-line" aria-live="polite">
          <span>Aktywny profil</span>
          <strong>{selectedProfileLabel}</strong>
        </div>
      </div>
      <div className="settings-category-list">
        <SettingsSection title="PULPIT">
          {pcGamingBehaviourOptions.slice(0, 2).map((option) => (
            <SettingRow
              key={option.key}
              title={option.label}
              description={option.description}
              checked={pcGamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasProfiles}
            />
          ))}
          {pcGamingBehaviourOptions.slice(4, 6).map((option) => (
            <SettingRow
              key={option.key}
              title={option.label}
              description={option.description}
              checked={pcGamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasProfiles}
            />
          ))}
        </SettingsSection>
        <SettingsSection title="OKNA">
          {pcGamingBehaviourOptions.slice(2, 4).map((option) => (
            <SettingRow
              key={option.key}
              title={option.label}
              description={option.description}
              checked={pcGamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasProfiles}
            />
          ))}
        </SettingsSection>
        <SettingsSection title="LAUNCHER">
          {pcGamingBehaviourOptions.slice(6).map((option) => (
            <SettingRow
              key={option.key}
              title={option.label}
              description={option.description}
              checked={pcGamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasProfiles}
            />
          ))}
        </SettingsSection>
      </div>
    </div>
  );
}
