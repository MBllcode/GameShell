import { useTranslation } from 'react-i18next';
import type { DisplayLayoutSnapshot } from '../types/bridge';
import { summarizeLayout } from '../utils/displays';

type LauncherType = 'Playnite Fullscreen' | 'Steam Big Picture' | 'Xbox App';

type ProfileHotkeysState = {
  enterProfile: string;
};

type PcGamingProfileLauncherMap = Record<string, LauncherType>;
type PcGamingProfileHotkeysMap = Record<string, ProfileHotkeysState>;

export function FinishStep({
  stepNumber,
  launcher,
  pcGamingLayout,
  pcGamingProfileLaunchers,
  pcGamingProfileHotkeys,
  activeProfileName
}: {
  stepNumber: number;
  launcher: LauncherType;
  pcGamingLayout: DisplayLayoutSnapshot | null;
  pcGamingProfileLaunchers: PcGamingProfileLauncherMap;
  pcGamingProfileHotkeys: PcGamingProfileHotkeysMap;
  activeProfileName: string | null;
}) {
  const { t } = useTranslation();
  const activeLauncher = activeProfileName ? pcGamingProfileLaunchers[activeProfileName] ?? launcher : launcher;
  const activeHotkeys = activeProfileName
    ? pcGamingProfileHotkeys[activeProfileName] ?? { enterProfile: '' }
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
          <dd>{pcGamingLayout ? summarizeLayout(pcGamingLayout, t) : t('summary.layoutMissing')}</dd>
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
