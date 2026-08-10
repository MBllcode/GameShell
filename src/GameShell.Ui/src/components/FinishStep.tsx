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
  const activeLauncher = activeProfileName ? pcGamingProfileLaunchers[activeProfileName] ?? launcher : launcher;
  const activeHotkeys = activeProfileName
    ? pcGamingProfileHotkeys[activeProfileName] ?? { enterProfile: '' }
    : { enterProfile: '' };

  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Podsumowanie ustawień</h2>
      <dl className="summary-list">
        <div>
          <dt>Profil gamingowy</dt>
          <dd>{activeProfileName || 'Nie wybrano'}</dd>
        </div>
        <div>
          <dt>Układ ekranów</dt>
          <dd>{pcGamingLayout ? summarizeLayout(pcGamingLayout) : 'Brak aktywnego profilu'}</dd>
        </div>
        <div>
          <dt>Launcher</dt>
          <dd>{activeLauncher}</dd>
        </div>
        <div>
          <dt>Skrót profilu</dt>
          <dd>{activeHotkeys.enterProfile || 'Brak przypisanego skrótu'}</dd>
        </div>
      </dl>
    </div>
  );
}
