import { useState } from 'react';
import { DEFAULT_LANGUAGE, type SupportedLanguage } from '../../../i18n/language';
import type { LauncherType } from '../../../utils/modes';

export type HotkeySettingsState = { consoleMode: string; gamingMode: string; workMode: string };

export type ConsoleBehaviourState = {
  hideDesktopIcons: boolean;
  hideTaskbar: boolean;
  closeVisibleWindows: boolean;
  minimizeRemainingWindows: boolean;
  moveCursorToCorner: boolean;
  hideCursor: boolean;
  startLauncher: boolean;
  bringLauncherToForeground: boolean;
};

export type GamingBehaviourState = ConsoleBehaviourState;

export type WorkBehaviourState = {
  closeLauncher: boolean;
  showTaskbars: boolean;
  showDesktopIcons: boolean;
  doNotMinimizeWindows: boolean;
};

export type FinishOptions = {
  startWithWindows: boolean;
  startMinimized: boolean;
  startMenuShortcut: boolean;
  desktopShortcut: boolean;
};

export const defaultProfileHotkeys = { enterProfile: '' };

export const defaultConsoleBehaviour: ConsoleBehaviourState = {
  hideDesktopIcons: true,
  hideTaskbar: true,
  closeVisibleWindows: true,
  minimizeRemainingWindows: true,
  moveCursorToCorner: true,
  hideCursor: true,
  startLauncher: true,
  bringLauncherToForeground: true
};

export const defaultGamingBehaviour: GamingBehaviourState = {
  ...defaultConsoleBehaviour
};

export const defaultWorkBehaviour: WorkBehaviourState = {
  closeLauncher: true,
  showTaskbars: true,
  showDesktopIcons: true,
  doNotMinimizeWindows: true
};

export const defaultFinishOptions: FinishOptions = {
  startWithWindows: true,
  startMinimized: false,
  startMenuShortcut: true,
  desktopShortcut: true
};

export function useSettingsFormState() {
  const [hotkeys, setHotkeys] = useState<HotkeySettingsState>({ consoleMode: '', gamingMode: '', workMode: '' });
  const [launcher, setLauncher] = useState<LauncherType>('Playnite Fullscreen');
  const [language, setLanguage] = useState<SupportedLanguage>(DEFAULT_LANGUAGE);
  const [consoleBehaviour, setConsoleBehaviour] = useState<ConsoleBehaviourState>({ ...defaultConsoleBehaviour });
  const [gamingBehaviour, setGamingBehaviour] = useState<GamingBehaviourState>({ ...defaultGamingBehaviour });
  const [workBehaviour, setWorkBehaviour] = useState<WorkBehaviourState>({ ...defaultWorkBehaviour });
  const [finishOptions, setFinishOptions] = useState<FinishOptions>({ ...defaultFinishOptions });

  return {
    hotkeys,
    setHotkeys,
    launcher,
    setLauncher,
    language,
    setLanguage,
    consoleBehaviour,
    setConsoleBehaviour,
    gamingBehaviour,
    setGamingBehaviour,
    workBehaviour,
    setWorkBehaviour,
    finishOptions,
    setFinishOptions
  };
}
