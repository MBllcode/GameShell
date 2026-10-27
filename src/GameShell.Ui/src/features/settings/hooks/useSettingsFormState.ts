import { DEFAULT_PROFILE_BEHAVIOUR, type ProfileBehaviour } from '../../profiles/profileModel';
import { useState } from 'react';
import { DEFAULT_LANGUAGE, type SupportedLanguage } from '../../../i18n/language';
import type { LauncherType } from '../../profiles/profileCommands';

export type HotkeySettingsState = { consoleMode: string; gamingMode: string; workMode: string };

export type ConsoleBehaviourState = ProfileBehaviour;
export type GamingBehaviourState = ProfileBehaviour;

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
  startupBehavior: 'AlwaysWorkMode' | 'RestoreLastActiveMode' | 'AlwaysProfile';
  startupProfileId: string | null;
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

export const defaultGamingBehaviour = DEFAULT_PROFILE_BEHAVIOUR;

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
  desktopShortcut: true,
  startupBehavior: 'AlwaysWorkMode',
  startupProfileId: null
};

export function useSettingsFormState() {
  const [hotkeys, setHotkeys] = useState<HotkeySettingsState>({ consoleMode: '', gamingMode: '', workMode: '' });
  const [launcher, setLauncher] = useState<LauncherType>('None');
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
