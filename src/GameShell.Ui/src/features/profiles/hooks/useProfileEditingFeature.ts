import { useEffect, type Dispatch, type SetStateAction } from 'react';
import { useControllerCapture } from '../../controller/hooks/useControllerCapture';
import { useProfileMutations } from './useProfileMutations';
import type { ControllerShortcutSettings } from '../../../types/bridge';
import type { LauncherType } from '../../../utils/modes';
import type { PcGamingProfileBehaviourMap, PcGamingProfileControllerShortcutMap, PcGamingProfileHotkeysMap, PcGamingProfileLauncherMap } from '../utils';

type BehaviourState = {
  hideDesktopIcons: boolean;
  hideTaskbar: boolean;
  closeVisibleWindows: boolean;
  minimizeRemainingWindows: boolean;
  moveCursorToCorner: boolean;
  hideCursor: boolean;
  startLauncher: boolean;
  bringLauncherToForeground: boolean;
};

type Options = {
  selectedProfileName: string;
  pcGamingBehaviour: BehaviourState;
  profileBehaviours: PcGamingProfileBehaviourMap;
  profileLaunchers: PcGamingProfileLauncherMap;
  profileHotkeys: PcGamingProfileHotkeysMap;
  profileControllerShortcuts: PcGamingProfileControllerShortcutMap;
  workModeControllerShortcut: ControllerShortcutSettings | null;
  workModeHotkey: string;
  setPcGamingBehaviour: Dispatch<SetStateAction<BehaviourState>>;
  setProfileBehaviours: Dispatch<SetStateAction<PcGamingProfileBehaviourMap>>;
  setLauncher: Dispatch<SetStateAction<LauncherType>>;
  setProfileLaunchers: Dispatch<SetStateAction<PcGamingProfileLauncherMap>>;
  setProfileHotkeys: Dispatch<SetStateAction<PcGamingProfileHotkeysMap>>;
  setProfileControllerShortcuts: Dispatch<SetStateAction<PcGamingProfileControllerShortcutMap>>;
  setWorkModeControllerShortcut: Dispatch<SetStateAction<ControllerShortcutSettings | null>>;
  setHotkeys: Dispatch<SetStateAction<{ consoleMode: string; pcGamingMode: string; workMode: string }>>;
  setProfileHotkeyValidationError: Dispatch<SetStateAction<string | null>>;
  setProfileControllerValidationError: Dispatch<SetStateAction<string | null>>;
  setError: Dispatch<SetStateAction<string | null>>;
  markUnsavedChanges: () => void;
};

export function useProfileEditingFeature(options: Options) {
  const mutations = useProfileMutations(options);
  const controllerCapture = useControllerCapture({
    selectedProfileName: options.selectedProfileName,
    profileShortcuts: options.profileControllerShortcuts,
    workModeShortcut: options.workModeControllerShortcut,
    onProfileShortcutCaptured: mutations.updateProfileControllerShortcut,
    onWorkModeShortcutCaptured: mutations.updateWorkModeControllerShortcut,
    onError: options.setError
  });

  useEffect(() => {
    options.setProfileHotkeyValidationError(null);
    options.setProfileControllerValidationError(null);
  }, [options.selectedProfileName]);

  return { mutations, controllerCapture };
}
