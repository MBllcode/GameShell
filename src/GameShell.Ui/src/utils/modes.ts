import type { AppStatus } from '../types/bridge';
import { formatCommandTargetMode } from './status.ts';

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

type TranslationLike = (key: string, options?: Record<string, string | number>) => string;

export function getModeCardStateLabel(command: ModeCommand, status: AppStatus, isUnconfigured: boolean, t?: TranslationLike) {
  if (isCurrentMode(command, status)) {
    return t ? t('app.active') : 'ACTIVE';
  }

  return isUnconfigured ? (t ? t('mode.unconfigured') : 'UNCONFIGURED') : (t ? t('app.ready') : 'READY');
}

export function getModeCardHint(command: ModeCommand, status: AppStatus, t?: TranslationLike) {
  return isCurrentMode(command, status)
    ? (t ? t('mode.youAreHere') : 'You are here')
    : (t ? t('mode.switchTo', { mode: formatCommandTargetMode(command, t) }) : `Switch to ${formatCommandTargetMode(command)}`);
}

export function isCurrentMode(command: ModeCommand, status: AppStatus) {
  return (command === 'enterGameShell' && status.currentMode === 'Console')
    || (command === 'enterGameShellWithProfile' && status.currentMode === 'GamingProfile')
    || (command === 'enterWorkMode' && status.currentMode === 'Work');
}
