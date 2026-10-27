
export type LauncherType = 'None' | 'Playnite Fullscreen' | 'Steam Big Picture' | 'Xbox App';
export type ModeCommand = 'enterGameShell' | 'enterGameShellWithProfile' | 'enterWorkMode';

export function getLauncherSettings(launcher: LauncherType) {
  switch (launcher) {
    case 'None': return { type: 'None', executablePath: '', arguments: '' };
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
    case 'None': return 'None';
    case 'Playnite Fullscreen': return 'Playnite Fullscreen';
    case 'Steam Big Picture': return 'Steam Big Picture';
    case 'Xbox App': return 'Xbox App';
    case 'Steam': return 'Steam Big Picture';
    case 'XboxApp': return 'Xbox App';
    case 'Playnite':
    default: return 'Playnite Fullscreen';
  }
}
