import type { AppStatus } from '../types/bridge';

type ModeCommand = 'enterGameShell' | 'enterGameShellWithProfile' | 'enterWorkMode';

type TranslationLike = (key: string, options?: Record<string, string | number>) => string;

export function formatCurrentMode(status: AppStatus, t?: TranslationLike) {
  switch (status.currentMode) {
    case 'Console':
    case 'GamingProfile':
      return t ? t('status.gamingProfile') : 'Gaming profile';
    case 'Work':
      return t ? t('status.workMode') : 'Work Mode';
    default:
      return formatModeState(status.state, t);
  }
}

export function formatSessionState(state: string, t?: TranslationLike) {
  switch (state) {
    case 'EnteringProfile': return t ? t('status.session.launchingProfile') : 'starting profile';
    case 'LauncherActive': return t ? t('status.session.launcherActive') : 'launcher active';
    case 'LaunchingGame': return t ? t('status.session.launchingGame') : 'launching game';
    case 'GameActive': return t ? t('status.session.gameActive') : 'game active';
    case 'ReturningToLauncher': return t ? t('status.session.returningToLauncher') : 'returning to launcher';
    case 'ExitingToWorkMode': return t ? t('status.session.exitingToWorkMode') : 'returning to Work Mode';
    case 'Faulted': return t ? t('status.session.faulted') : 'session error';
    default: return state;
  }
}

function formatModeState(state: AppStatus['state'], t?: TranslationLike) {
  switch (state) {
    case 'WorkMode': return t ? t('status.workMode') : 'Work Mode';
    case 'GameShell': return t ? t('status.gamingProfile') : 'Gaming profile';
    case 'SwitchingToConsole': return t ? t('status.mode.switchingToConsole') : 'switching to gaming mode';
    case 'SwitchingToWork': return t ? t('status.mode.switchingToWork') : 'switching to Work Mode';
    case 'Error': return t ? t('status.mode.error') : 'requires attention';
    case 'Unknown':
    default: return t ? t('status.mode.unknown') : 'unknown';
  }
}

export function formatCommandTargetMode(command: ModeCommand, t?: TranslationLike) {
  switch (command) {
    case 'enterGameShell':
    case 'enterGameShellWithProfile': return t ? t('status.gamingProfile') : 'Gaming profile';
    case 'enterWorkMode': return t ? t('status.workMode') : 'Work Mode';
  }
}
