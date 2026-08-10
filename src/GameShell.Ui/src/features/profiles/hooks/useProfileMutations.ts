import { useCallback, type Dispatch, type SetStateAction } from 'react';
import {
  isDuplicateProfileControllerShortcut,
  isDuplicateWorkModeControllerShortcut,
  isSameControllerShortcut,
  normalizeControllerButtons
} from '../../../utils/controllers';
import { isSameHotkey, normalizeHotkeyForDisplay } from '../../../utils/hotkeys';
import {
  isDuplicateProfileEnterHotkey,
  isDuplicateWorkModeHotkey,
  type PcGamingProfileBehaviourMap,
  type PcGamingProfileControllerShortcutMap,
  type PcGamingProfileHotkeysMap,
  type PcGamingProfileLauncherMap,
  type ProfileHotkeysState
} from '../utils';
import type { ControllerShortcutSettings } from '../../../types/bridge';
import type { LauncherType } from '../../../utils/modes';

type PcGamingBehaviourState = {
  hideDesktopIcons: boolean;
  hideTaskbar: boolean;
  closeVisibleWindows: boolean;
  minimizeRemainingWindows: boolean;
  moveCursorToCorner: boolean;
  hideCursor: boolean;
  startLauncher: boolean;
  bringLauncherToForeground: boolean;
};

type PcGamingBehaviourKey = keyof PcGamingBehaviourState;

const defaultProfileHotkeys: ProfileHotkeysState = { enterProfile: '' };
const defaultControllerShortcutHoldDurationMs = 700;

type UseProfileMutationsOptions = {
  workingProfileName: string;
  pcGamingBehaviour: PcGamingBehaviourState;
  profileBehaviours: PcGamingProfileBehaviourMap;
  profileLaunchers: PcGamingProfileLauncherMap;
  profileHotkeys: PcGamingProfileHotkeysMap;
  profileControllerShortcuts: PcGamingProfileControllerShortcutMap;
  workModeControllerShortcut: ControllerShortcutSettings | null;
  workModeHotkey: string;
  setPcGamingBehaviour: Dispatch<SetStateAction<PcGamingBehaviourState>>;
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

export function useProfileMutations({
  workingProfileName,
  pcGamingBehaviour,
  profileBehaviours,
  profileLaunchers,
  profileHotkeys,
  profileControllerShortcuts,
  workModeControllerShortcut,
  workModeHotkey,
  setPcGamingBehaviour,
  setProfileBehaviours,
  setLauncher,
  setProfileLaunchers,
  setProfileHotkeys,
  setProfileControllerShortcuts,
  setWorkModeControllerShortcut,
  setHotkeys,
  setProfileHotkeyValidationError,
  setProfileControllerValidationError,
  setError,
  markUnsavedChanges
}: UseProfileMutationsOptions) {
  const setPcGamingBehaviourForProfile = useCallback((profileName: string, behaviour: PcGamingBehaviourState) => {
    setProfileBehaviours((current) => ({ ...current, [profileName]: behaviour }));
    setPcGamingBehaviour(behaviour);
    markUnsavedChanges();
  }, [markUnsavedChanges, setPcGamingBehaviour, setProfileBehaviours]);

  const updatePcGamingBehaviourFlag = useCallback((key: PcGamingBehaviourKey, value: boolean) => {
    if (!workingProfileName) {
      setPcGamingBehaviour((current) => ({ ...current, [key]: value }));
      return;
    }

    const currentProfileBehaviour = profileBehaviours[workingProfileName] ?? pcGamingBehaviour;
    setPcGamingBehaviourForProfile(workingProfileName, { ...currentProfileBehaviour, [key]: value });
  }, [pcGamingBehaviour, profileBehaviours, workingProfileName, setPcGamingBehaviour, setPcGamingBehaviourForProfile]);

  const setProfileLauncher = useCallback((profileName: string, nextLauncher: LauncherType) => {
    setProfileLaunchers((current) => ({ ...current, [profileName]: nextLauncher }));
    setLauncher(nextLauncher);
    markUnsavedChanges();
  }, [markUnsavedChanges, setLauncher, setProfileLaunchers]);

  const updateProfileHotkey = useCallback((profileName: string, key: keyof ProfileHotkeysState, value: string) => {
    const normalizedValue = normalizeHotkeyForDisplay(value);
    if (key === 'enterProfile' && (isDuplicateProfileEnterHotkey(profileName, normalizedValue, profileHotkeys) || isSameHotkey(normalizedValue, workModeHotkey))) {
      setProfileHotkeyValidationError('Ten skrót jest już przypisany do innego profilu lub Work Mode. Wybierz inną kombinację.');
      return;
    }

    setProfileHotkeyValidationError(null);
    setError(null);
    setProfileHotkeys((current) => ({
      ...current,
      [profileName]: { ...(current[profileName] ?? defaultProfileHotkeys), [key]: normalizedValue }
    }));
    markUnsavedChanges();

    if (workingProfileName === profileName) {
      setHotkeys((current) => ({ ...current, pcGamingMode: key === 'enterProfile' ? normalizedValue : current.pcGamingMode }));
    }
  }, [markUnsavedChanges, profileHotkeys, setError, setHotkeys, setProfileHotkeyValidationError, setProfileHotkeys, workModeHotkey, workingProfileName]);

  const updateProfileControllerShortcut = useCallback((profileName: string, buttons: string[]) => {
    const normalizedButtons = normalizeControllerButtons(buttons);
    if (isDuplicateProfileControllerShortcut(profileName, normalizedButtons, profileControllerShortcuts) || isSameControllerShortcut(normalizedButtons, workModeControllerShortcut?.buttons)) {
      setProfileControllerValidationError('Ta kombinacja przyciskow jest juz przypisana do innego profilu lub Work Mode. Wybierz inna kombinacje.');
      return;
    }

    setProfileControllerValidationError(null);
    setError(null);
    setProfileControllerShortcuts((current) => ({
      ...current,
      [profileName]: normalizedButtons.length === 0 ? null : {
        buttons: normalizedButtons,
        holdDurationMs: current[profileName]?.holdDurationMs ?? defaultControllerShortcutHoldDurationMs
      }
    }));
    markUnsavedChanges();
  }, [markUnsavedChanges, profileControllerShortcuts, setError, setProfileControllerShortcuts, setProfileControllerValidationError, workModeControllerShortcut]);

  const updateWorkModeControllerShortcut = useCallback((buttons: string[]) => {
    const normalizedButtons = normalizeControllerButtons(buttons);
    if (isDuplicateWorkModeControllerShortcut(normalizedButtons, profileControllerShortcuts)) {
      setProfileControllerValidationError('Ta kombinacja przyciskow jest juz przypisana do profilu. Wybierz inna kombinacje.');
      return;
    }

    setProfileControllerValidationError(null);
    setError(null);
    setWorkModeControllerShortcut(normalizedButtons.length === 0 ? null : {
      buttons: normalizedButtons,
      holdDurationMs: workModeControllerShortcut?.holdDurationMs ?? defaultControllerShortcutHoldDurationMs
    });
    markUnsavedChanges();
  }, [markUnsavedChanges, profileControllerShortcuts, setError, setProfileControllerValidationError, setWorkModeControllerShortcut, workModeControllerShortcut]);

  const updateWorkModeHotkey = useCallback((value: string) => {
    const normalizedValue = normalizeHotkeyForDisplay(value);
    if (isDuplicateWorkModeHotkey(normalizedValue, profileHotkeys)) {
      setProfileHotkeyValidationError('Ten skrót jest już przypisany do profilu. Wybierz inną kombinację.');
      return;
    }

    setProfileHotkeyValidationError(null);
    setError(null);
    setHotkeys((current) => ({ ...current, workMode: normalizedValue }));
    markUnsavedChanges();
  }, [markUnsavedChanges, profileHotkeys, setError, setHotkeys, setProfileHotkeyValidationError]);

  return {
    setProfileLauncher,
    updatePcGamingBehaviourFlag,
    updateProfileHotkey,
    updateProfileControllerShortcut,
    updateWorkModeControllerShortcut,
    updateWorkModeHotkey
  };
}
