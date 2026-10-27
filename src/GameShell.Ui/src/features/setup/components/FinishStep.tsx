import { useTranslation } from 'react-i18next';
import type { DisplayLayoutSnapshot } from '../../../bridge/contracts';
import { summarizeLayout } from '../../displays/displayTopology';
import { IconBadge, PageHeader } from '../../../shared/ui/UiPrimitives';

import type { LauncherType } from '../../profiles/profileCommands';

type ProfileHotkeysState = {
  enterProfile: string;
};

type GamingProfileLauncherMap = Record<string, LauncherType>;
type GamingProfileHotkeysMap = Record<string, ProfileHotkeysState>;

export function FinishStep({
  stepNumber,
  launcher,
  gamingLayout,
  gamingProfileLaunchers,
  gamingProfileHotkeys,
  activeProfileName
}: {
  stepNumber: number;
  launcher: LauncherType;
  gamingLayout: DisplayLayoutSnapshot | null;
  gamingProfileLaunchers: GamingProfileLauncherMap;
  gamingProfileHotkeys: GamingProfileHotkeysMap;
  activeProfileName: string | null;
}) {
  const { t } = useTranslation();
  const activeLauncher = activeProfileName ? gamingProfileLaunchers[activeProfileName] ?? launcher : launcher;
  const activeHotkeys = activeProfileName
    ? gamingProfileHotkeys[activeProfileName] ?? { enterProfile: '' }
    : { enterProfile: '' };

  return (
    <div className="wizard-copy">
      <PageHeader eyebrow={`${t('setup.step')} ${stepNumber}`} title={t('setup.finishSummary')} />
      <dl className="summary-list">
        <div>
          <IconBadge name="profile" size={16} />
          <dt>{t('summary.profileGaming')}</dt>
          <dd>{activeProfileName || t('summary.activeProfileNone')}</dd>
        </div>
        <div>
          <IconBadge name="layout" size={16} />
          <dt>{t('summary.layout')}</dt>
          <dd>{gamingLayout ? summarizeLayout(gamingLayout, t) : t('summary.layoutMissing')}</dd>
        </div>
        <div>
          <IconBadge name="launcher" size={16} />
          <dt>{t('summary.launcher')}</dt>
          <dd>{activeLauncher === 'None' ? t('launcher.noApplication') : activeLauncher}</dd>
        </div>
        <div>
          <IconBadge name="keyboard" size={16} />
          <dt>{t('summary.hotkey')}</dt>
          <dd>{activeHotkeys.enterProfile || t('summary.hotkeyMissing')}</dd>
        </div>
      </dl>
    </div>
  );
}
