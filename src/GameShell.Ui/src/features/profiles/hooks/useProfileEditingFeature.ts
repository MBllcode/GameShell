import type { Profile, ProfileBehaviour } from '../profileModel';
import { useEffect, type Dispatch, type SetStateAction } from 'react';
import { useControllerCapture } from '../../shortcuts/hooks/useControllerCapture';
import { useProfileMutations } from './useProfileMutations';
import type { ControllerShortcutSettings } from '../../../bridge/contracts';
import type { LauncherType } from '../profileCommands';
import type { GamingProfileBehaviourMap, GamingProfileControllerShortcutMap, GamingProfileHotkeysMap } from '../utils';

type BehaviourState = ProfileBehaviour;

type Options = {
  setProfiles: Dispatch<SetStateAction<Profile[]>>;
  workingProfileName: string;
  gamingBehaviour: BehaviourState;
  profileBehaviours: GamingProfileBehaviourMap;
  profileHotkeys: GamingProfileHotkeysMap;
  profileControllerShortcuts: GamingProfileControllerShortcutMap;
  workModeControllerShortcut: ControllerShortcutSettings | null;
  workModeHotkey: string;
  setGamingBehaviour: Dispatch<SetStateAction<BehaviourState>>;
  setLauncher: Dispatch<SetStateAction<LauncherType>>;
  setWorkModeControllerShortcut: Dispatch<SetStateAction<ControllerShortcutSettings | null>>;
  setHotkeys: Dispatch<SetStateAction<{ consoleMode: string; gamingMode: string; workMode: string }>>;
  setProfileHotkeyValidationError: Dispatch<SetStateAction<string | null>>;
  setProfileControllerValidationError: Dispatch<SetStateAction<string | null>>;
  setError: Dispatch<SetStateAction<string | null>>;
  markUnsavedChanges: () => void;
};

export function useProfileEditingFeature(options: Options) {
  const mutations = useProfileMutations(options);
  const controllerCapture = useControllerCapture({
    selectedProfileName: options.workingProfileName,
    profileShortcuts: options.profileControllerShortcuts,
    workModeShortcut: options.workModeControllerShortcut,
    onProfileShortcutCaptured: mutations.updateProfileControllerShortcut,
    onWorkModeShortcutCaptured: mutations.updateWorkModeControllerShortcut,
    onError: options.setError
  });

  useEffect(() => {
    options.setProfileHotkeyValidationError(null);
    options.setProfileControllerValidationError(null);
  }, [options.workingProfileName]);

  return { mutations, controllerCapture };
}
