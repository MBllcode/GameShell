import type { AppStatus } from '../types/bridge';

type ModeCommand = 'enterGameShell' | 'enterGameShellWithProfile' | 'enterWorkMode';

export function formatCurrentMode(status: AppStatus) {
  switch (status.currentMode) {
    case 'Console':
    case 'PcGaming':
      return 'Tryb gamingowy';
    case 'Work':
      return 'Work Mode';
    default:
      return formatModeState(status.state);
  }
}

export function formatSessionState(state: string) {
  switch (state) {
    case 'EnteringProfile': return 'uruchamianie profilu';
    case 'LauncherActive': return 'launcher aktywny';
    case 'LaunchingGame': return 'uruchamianie gry';
    case 'GameActive': return 'gra aktywna';
    case 'ReturningToLauncher': return 'powrot do launchera';
    case 'ExitingToWorkMode': return 'powrot do Work Mode';
    case 'Faulted': return 'blad sesji';
    default: return state;
  }
}

function formatModeState(state: AppStatus['state']) {
  switch (state) {
    case 'WorkMode': return 'Work Mode';
    case 'GameShell': return 'tryb gamingowy';
    case 'SwitchingToConsole': return 'przełączanie do gamingowego';
    case 'SwitchingToWork': return 'przełączanie do Work Mode';
    case 'Error': return 'wymaga uwagi';
    case 'Unknown':
    default: return 'nieustalony';
  }
}

export function formatCommandTargetMode(command: ModeCommand) {
  switch (command) {
    case 'enterGameShell':
    case 'enterGameShellWithProfile': return 'Tryb gamingowy';
    case 'enterWorkMode': return 'Work Mode';
  }
}
