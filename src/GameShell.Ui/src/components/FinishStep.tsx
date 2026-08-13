import { useTranslation } from 'react-i18next';
import type { DisplayLayoutSnapshot } from '../types/bridge';
import { summarizeLayout } from '../utils/displays';

type LauncherType = 'Playnite Fullscreen' | 'Steam Big Picture' | 'Xbox App';

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
      <p className="eyebrow">{t('setup.step')} {stepNumber}</p>
      <h2>{t('setup.finishSummary')}</h2>
      <dl className="summary-list">
        <div>
          <dt>{t('summary.profileGaming')}</dt>
          <dd>{activeProfileName || t('summary.activeProfileNone')}</dd>
        </div>
        <div>
          <dt>{t('summary.layout')}</dt>
          <dd>{gamingLayout ? summarizeLayout(gamingLayout, t) : t('summary.layoutMissing')}</dd>
        </div>
        <div>
          <dt>{t('summary.launcher')}</dt>
          <dd>{activeLauncher}</dd>
        </div>
        <div>
          <dt>{t('summary.hotkey')}</dt>
          <dd>{activeHotkeys.enterProfile || t('summary.hotkeyMissing')}</dd>
        </div>
      </dl>
    </div>
  );
}
