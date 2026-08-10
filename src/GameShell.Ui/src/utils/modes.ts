import type { AppStatus } from '../types/bridge';
import { formatCommandTargetMode } from './status';

export type LauncherType = 'Playnite Fullscreen' | 'Steam Big Picture' | 'Xbox App';
export type ModeCommand = 'enterGameShell' | 'enterGameShellWithProfile' | 'enterWorkMode';

export function getLauncherSettings(launcher: LauncherType) {
  switch (launcher) {
    case 'Steam Big Picture':
      return { type: 'Steam', executablePath: 'steam://open/bigpicture', arguments: '' };
    case 'Xbox App':
      return { type: 'XboxApp', executablePath: '', arguments: '' };
    case 'Playnite Fullscreen':
    default:
      return {
        type: 'Playnite',
        executablePath: '%LOCALAPPDATA%\\Playnite\\Playnite.FullscreenApp.exe',
        arguments: '--startfullscreen --hidesplashscreen'
      };
  }
}

export function toLauncherType(type: string): LauncherType {
  switch (type) {
    case 'Steam': return 'Steam Big Picture';
    case 'XboxApp': return 'Xbox App';
    case 'Playnite':
    default: return 'Playnite Fullscreen';
  }
}

export function getModeActionKey(command: ModeCommand) {
  return `${command}:`;
}

export function getModeCardClass(command: ModeCommand, status: AppStatus, isUnconfigured = false, isSelected = false) {
  const classes = ['mode-card'];
  if (isCurrentMode(command, status)) {
    classes.push('active');
  } else if (isUnconfigured) {
    classes.push('unconfigured');
  }

  if (isSelected) {
    classes.push('selected');
  }

  return classes.join(' ');
}

export function getModeCardStateLabel(command: ModeCommand, status: AppStatus, isUnconfigured: boolean) {
  if (isCurrentMode(command, status)) {
    return 'AKTYWNY';
  }

  return isUnconfigured ? 'NIESKONFIGUROWANY' : 'GOTOWY';
}

export function getModeCardHint(command: ModeCommand, status: AppStatus) {
  return isCurrentMode(command, status) ? 'Jesteś tutaj' : `Przełącz na ${formatCommandTargetMode(command)}`;
}

export function isCurrentMode(command: ModeCommand, status: AppStatus) {
  return (command === 'enterGameShell' && status.currentMode === 'Console')
    || (command === 'enterGameShellWithProfile' && status.currentMode === 'PcGaming')
    || (command === 'enterWorkMode' && status.currentMode === 'Work');
}
