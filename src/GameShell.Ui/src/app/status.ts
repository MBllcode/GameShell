import type { AppStatus, TransientOperationState } from '../bridge/contracts';


type TranslationLike = (key: string, options?: Record<string, string | number>) => string;

export function formatCurrentMode(status: AppStatus, t?: TranslationLike) {
  switch (status.currentMode) {
    case 'Console':
    case 'GamingProfile':
      return t ? t('status.gamingProfile') : 'Profile';
    case 'Work':
      return t ? t('status.workMode') : 'Default setup';
    default:
      return formatModeState(status.state, t);
  }
}

export function formatSessionState(state: string, t?: TranslationLike) {
  switch (state) {
    case 'ProfileActive': return t ? t('status.session.profileActive') : 'profile active';
    case 'EnteringProfile': return t ? t('status.session.launchingProfile') : 'starting profile';
    case 'LauncherActive': return t ? t('status.session.launcherActive') : 'launcher active';
    case 'LaunchingGame': return t ? t('status.session.launchingGame') : 'launching game';
    case 'GameActive': return t ? t('status.session.gameActive') : 'game active';
    case 'ReturningToLauncher': return t ? t('status.session.returningToLauncher') : 'returning to launcher';
    case 'ExitingToWorkMode': return t ? t('status.session.exitingToWorkMode') : 'restoring default setup';
    case 'Faulted': return t ? t('status.session.faulted') : 'session error';
    default: return state;
  }
}

function formatModeState(state: AppStatus['state'], t?: TranslationLike) {
  switch (state) {
    case 'WorkMode': return t ? t('status.workMode') : 'Default setup';
    case 'GameShell': return t ? t('status.gamingProfile') : 'Profile';
    case 'SwitchingToConsole': return t ? t('status.mode.switchingToConsole') : 'activating profile';
    case 'SwitchingToWork': return t ? t('status.mode.switchingToWork') : 'restoring default setup';
    case 'Error': return t ? t('status.mode.error') : 'requires attention';
    case 'Unknown':
    default: return t ? t('status.mode.unknown') : 'unknown';
  }
}

export function formatTransientOperation(
  operation: TransientOperationState | null | undefined,
  t?: TranslationLike
) {
  if (!operation?.isActive) return '';

  const keyByPhase: Record<string, string> = {
    PreparingDisplays: 'status.transient.preparingDisplays',
    ApplyingDisplayTopology: 'status.transient.applyingDisplayTopology',
    SynchronizingDisplays: 'status.transient.synchronizingDisplays',
    ApplyingProfileSettings: 'status.transient.applyingProfileSettings',
    StartingLauncher: 'status.transient.startingLauncher',
    ActivatingLauncher: 'status.transient.activatingLauncher',
    RestoringDisplays: 'status.transient.restoringDisplays',
    RestoringDesktop: 'status.transient.restoringDesktop'
  };
  const key = operation.messageKey || keyByPhase[operation.phase];
  return key && t ? t(key) : key || '';
}
