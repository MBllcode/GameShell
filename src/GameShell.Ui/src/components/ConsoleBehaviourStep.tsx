import { useTranslation } from 'react-i18next';
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

const pcGamingBehaviourOptions: Array<{ key: PcGamingBehaviourKey; labelKey: string; description?: string }> = [
  { key: 'hideDesktopIcons', labelKey: 'behaviour.hideDesktopIcons' },
  { key: 'hideTaskbar', labelKey: 'behaviour.hideTaskbar' },
  { key: 'closeVisibleWindows', labelKey: 'behaviour.closeVisibleWindows' },
  { key: 'minimizeRemainingWindows', labelKey: 'behaviour.minimizeRemainingWindows' },
  { key: 'moveCursorToCorner', labelKey: 'behaviour.moveCursorToCorner' },
  { key: 'hideCursor', labelKey: 'behaviour.hideCursor' },
  { key: 'startLauncher', labelKey: 'behaviour.startLauncher' },
  { key: 'bringLauncherToForeground', labelKey: 'behaviour.bringLauncherToForeground' }
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
          {pcGamingBehaviourOptions.slice(0, 2).map((option) => (
            <SettingRow
              key={option.key}
              title={t(option.labelKey)}
              description={option.description}
              checked={pcGamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasWorkingProfile}
            />
          ))}
          {pcGamingBehaviourOptions.slice(4, 6).map((option) => (
            <SettingRow
              key={option.key}
              title={t(option.labelKey)}
              description={option.description}
              checked={pcGamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasWorkingProfile}
            />
          ))}
        </SettingsSection>
        <SettingsSection title={t('behaviour.windows')}>
          {pcGamingBehaviourOptions.slice(2, 4).map((option) => (
            <SettingRow
              key={option.key}
              title={t(option.labelKey)}
              description={option.description}
              checked={pcGamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasWorkingProfile}
            />
          ))}
        </SettingsSection>
        <SettingsSection title={t('behaviour.launcher')}>
          {pcGamingBehaviourOptions.slice(6).map((option) => (
            <SettingRow
              key={option.key}
              title={t(option.labelKey)}
              description={option.description}
              checked={pcGamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasWorkingProfile}
            />
          ))}
        </SettingsSection>
      </div>
    </div>
  );
}
