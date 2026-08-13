import { useTranslation } from 'react-i18next';
import type { DisplayLayoutProfile } from '../types/bridge';
import { SettingRow } from './SettingRow';
import { SettingsSection } from './SettingsSection';

type GamingBehaviour = {
  hideDesktopIcons: boolean;
  hideTaskbar: boolean;
  closeVisibleWindows: boolean;
  minimizeRemainingWindows: boolean;
  moveCursorToCorner: boolean;
  hideCursor: boolean;
  startLauncher: boolean;
  bringLauncherToForeground: boolean;
};

type GamingBehaviourKey = keyof GamingBehaviour;

const gamingBehaviourOptions: Array<{ key: GamingBehaviourKey; labelKey: string; description?: string }> = [
  { key: 'hideDesktopIcons', labelKey: 'behaviour.hideDesktopIcons' },
  { key: 'hideTaskbar', labelKey: 'behaviour.hideTaskbar' },
  { key: 'closeVisibleWindows', labelKey: 'behaviour.closeVisibleWindows' },
  { key: 'minimizeRemainingWindows', labelKey: 'behaviour.minimizeRemainingWindows' },
  { key: 'moveCursorToCorner', labelKey: 'behaviour.moveCursorToCorner' },
  { key: 'hideCursor', labelKey: 'behaviour.hideCursor' },
  { key: 'startLauncher', labelKey: 'behaviour.startLauncher' },
  { key: 'bringLauncherToForeground', labelKey: 'behaviour.bringLauncherToForeground' }
];

export function ProfileBehaviourStep({
  stepNumber,
  gamingProfiles,
  selectedProfileName,
  gamingBehaviour,
  onChangeBehaviourFlag
}: {
  stepNumber: number;
  gamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  gamingBehaviour: GamingBehaviour;
  onChangeBehaviourFlag: (key: GamingBehaviourKey, value: boolean) => void;
}) {
  const { t } = useTranslation();
  const hasWorkingProfile = selectedProfileName.trim().length > 0;
  const selectedProfileLabel = selectedProfileName || t('profile.noProfileSelected');

  return (
    <div className="wizard-copy">
      <p className="eyebrow">{t('setup.step')} {stepNumber}</p>
      <h2>{t('behaviour.profileTitle')}</h2>
      <p>{t('behaviour.profileDescription')}</p>
      <div className="field-grid">
        <div className="profile-target-line" aria-live="polite">
          <span>{t('profile.activeProfile')}</span>
          <strong>{selectedProfileLabel}</strong>
        </div>
      </div>
      <div className="settings-category-list">
        <SettingsSection title={t('behaviour.desktop')}>
          {gamingBehaviourOptions.slice(0, 2).map((option) => (
            <SettingRow
              key={option.key}
              title={t(option.labelKey)}
              description={option.description}
              checked={gamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasWorkingProfile}
            />
          ))}
          {gamingBehaviourOptions.slice(4, 6).map((option) => (
            <SettingRow
              key={option.key}
              title={t(option.labelKey)}
              description={option.description}
              checked={gamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasWorkingProfile}
            />
          ))}
        </SettingsSection>
        <SettingsSection title={t('behaviour.windows')}>
          {gamingBehaviourOptions.slice(2, 4).map((option) => (
            <SettingRow
              key={option.key}
              title={t(option.labelKey)}
              description={option.description}
              checked={gamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasWorkingProfile}
            />
          ))}
        </SettingsSection>
        <SettingsSection title={t('behaviour.launcher')}>
          {gamingBehaviourOptions.slice(6).map((option) => (
            <SettingRow
              key={option.key}
              title={t(option.labelKey)}
              description={option.description}
              checked={gamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasWorkingProfile}
            />
          ))}
        </SettingsSection>
      </div>
    </div>
  );
}
