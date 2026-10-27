import { useTranslation } from 'react-i18next';
import { SettingRow } from '../../../shared/ui/SettingRow';
import { SettingsSection } from '../../../shared/ui/SettingsSection';
import { IconBadge, PageHeader, type UiIconName } from '../../../shared/ui/UiPrimitives';

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

const gamingBehaviourOptions: Array<{ key: GamingBehaviourKey; labelKey: string; description?: string; icon: UiIconName }> = [
  { key: 'hideDesktopIcons', labelKey: 'behaviour.hideDesktopIcons', icon: 'desktopOff' },
  { key: 'hideTaskbar', labelKey: 'behaviour.hideTaskbar', icon: 'taskbar' },
  { key: 'closeVisibleWindows', labelKey: 'behaviour.closeVisibleWindows', icon: 'windowClose' },
  { key: 'minimizeRemainingWindows', labelKey: 'behaviour.minimizeRemainingWindows', icon: 'windowMinimize' },
  { key: 'moveCursorToCorner', labelKey: 'behaviour.moveCursorToCorner', icon: 'cursor' },
  { key: 'hideCursor', labelKey: 'behaviour.hideCursor', icon: 'cursorOff' },
  { key: 'bringLauncherToForeground', labelKey: 'behaviour.bringLauncherToForeground', icon: 'foreground' }
];

export function ProfileBehaviourStep({
  stepNumber,
  selectedProfileName,
  gamingBehaviour,
  onChangeBehaviourFlag
}: {
  stepNumber: number;
  selectedProfileName: string;
  gamingBehaviour: GamingBehaviour;
  onChangeBehaviourFlag: (key: GamingBehaviourKey, value: boolean) => void;
}) {
  const { t } = useTranslation();
  const hasWorkingProfile = selectedProfileName.trim().length > 0;
  const selectedProfileLabel = selectedProfileName || t('profile.noProfileSelected');

  return (
    <div className="wizard-copy profile-behaviour-step">
      <PageHeader eyebrow={`${t('setup.step')} ${stepNumber}`} title={t('behaviour.profileTitle')} description={t('behaviour.profileDescription')} />
      <div className="field-grid">
        <div className="profile-target-line" aria-live="polite">
          <IconBadge name="profile" size={16} />
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
              showInfo
              icon={option.icon}
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
              showInfo
              icon={option.icon}
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
              showInfo
              icon={option.icon}
              checked={gamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasWorkingProfile}
            />
          ))}
        </SettingsSection>
        {gamingBehaviour.startLauncher ? <SettingsSection title={t('behaviour.launcher')}>
          {gamingBehaviourOptions.slice(6).map((option) => (
            <SettingRow
              key={option.key}
              title={t(option.labelKey)}
              description={option.description}
              showInfo
              icon={option.icon}
              checked={gamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasWorkingProfile}
            />
          ))}
        </SettingsSection> : null}
      </div>
    </div>
  );
}
