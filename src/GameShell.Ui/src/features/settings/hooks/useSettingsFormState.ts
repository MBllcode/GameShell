import { useState } from 'react';
import type { LauncherType } from '../../../utils/modes';

export type HotkeySettingsState = { consoleMode: string; pcGamingMode: string; workMode: string };

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

export type PcGamingBehaviourState = ConsoleBehaviourState;

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

export const defaultPcGamingBehaviour: PcGamingBehaviourState = {
  ...defaultConsoleBehaviour,
  closeVisibleWindows: false,
  minimizeRemainingWindows: false,
  hideCursor: false
};

export const defaultWorkBehaviour: WorkBehaviourState = {
  closeLauncher: true,
  showTaskbars: true,
  showDesktopIcons: true,
  doNotMinimizeWindows: true
};

export function useSettingsFormState() {
  const [hotkeys, setHotkeys] = useState<HotkeySettingsState>({ consoleMode: '', pcGamingMode: '', workMode: '' });
  const [launcher, setLauncher] = useState<LauncherType>('Playnite Fullscreen');
  const [consoleBehaviour, setConsoleBehaviour] = useState<ConsoleBehaviourState>({ ...defaultConsoleBehaviour });
  const [pcGamingBehaviour, setPcGamingBehaviour] = useState<PcGamingBehaviourState>({ ...defaultPcGamingBehaviour });
  const [workBehaviour, setWorkBehaviour] = useState<WorkBehaviourState>({ ...defaultWorkBehaviour });
  const [finishOptions, setFinishOptions] = useState<FinishOptions>({
    startWithWindows: false,
    startMinimized: false,
    startMenuShortcut: true,
    desktopShortcut: false
  });

  return {
    hotkeys,
    setHotkeys,
    launcher,
    setLauncher,
    consoleBehaviour,
    setConsoleBehaviour,
    pcGamingBehaviour,
    setPcGamingBehaviour,
    workBehaviour,
    setWorkBehaviour,
    finishOptions,
    setFinishOptions
  };
}
