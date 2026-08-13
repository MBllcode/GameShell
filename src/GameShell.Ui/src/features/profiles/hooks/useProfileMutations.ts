import { useCallback, type Dispatch, type SetStateAction } from 'react';
import { useTranslation } from 'react-i18next';
import {
  normalizeControllerButtons
} from '../../../utils/controllers';
import { normalizeHotkeyForDisplay } from '../../../utils/hotkeys';
import {
  type GamingProfileBehaviourMap,
  type GamingProfileControllerShortcutMap,
  type GamingProfileHotkeysMap,
  type GamingProfileLauncherMap,
  type ProfileHotkeysState
} from '../utils';
import { tryAssignProfileControllerShortcut, tryAssignProfileHotkey, tryAssignWorkModeControllerShortcut, tryAssignWorkModeHotkey } from '../shortcutAssignment';
import type { ControllerShortcutSettings } from '../../../types/bridge';
import type { LauncherType } from '../../../utils/modes';
import { triggerValidationError } from '../../../utils/validation';

type GamingBehaviourState = {
  hideDesktopIcons: boolean;
  hideTaskbar: boolean;
  closeVisibleWindows: boolean;
  minimizeRemainingWindows: boolean;
  moveCursorToCorner: boolean;
  hideCursor: boolean;
  startLauncher: boolean;
  bringLauncherToForeground: boolean;
};

type GamingBehaviourKey = keyof GamingBehaviourState;

const defaultProfileHotkeys: ProfileHotkeysState = { enterProfile: '' };
const defaultControllerShortcutHoldDurationMs = 700;

type UseProfileMutationsOptions = {
  workingProfileName: string;
  gamingBehaviour: GamingBehaviourState;
  profileBehaviours: GamingProfileBehaviourMap;
  profileLaunchers: GamingProfileLauncherMap;
  profileHotkeys: GamingProfileHotkeysMap;
  profileControllerShortcuts: GamingProfileControllerShortcutMap;
  workModeControllerShortcut: ControllerShortcutSettings | null;
  workModeHotkey: string;
  setGamingBehaviour: Dispatch<SetStateAction<GamingBehaviourState>>;
  setProfileBehaviours: Dispatch<SetStateAction<GamingProfileBehaviourMap>>;
  setLauncher: Dispatch<SetStateAction<LauncherType>>;
  setProfileLaunchers: Dispatch<SetStateAction<GamingProfileLauncherMap>>;
  setProfileHotkeys: Dispatch<SetStateAction<GamingProfileHotkeysMap>>;
  setProfileControllerShortcuts: Dispatch<SetStateAction<GamingProfileControllerShortcutMap>>;
  setWorkModeControllerShortcut: Dispatch<SetStateAction<ControllerShortcutSettings | null>>;
  setHotkeys: Dispatch<SetStateAction<{ consoleMode: string; gamingMode: string; workMode: string }>>;
  setProfileHotkeyValidationError: Dispatch<SetStateAction<string | null>>;
  setProfileControllerValidationError: Dispatch<SetStateAction<string | null>>;
  setError: Dispatch<SetStateAction<string | null>>;
  markUnsavedChanges: () => void;
};

export function useProfileMutations({
  workingProfileName,
  gamingBehaviour,
  profileBehaviours,
  profileLaunchers,
  profileHotkeys,
  profileControllerShortcuts,
  workModeControllerShortcut,
  workModeHotkey,
  setGamingBehaviour,
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
  const { t } = useTranslation();

  const setGamingBehaviourForProfile = useCallback((profileName: string, behaviour: GamingBehaviourState) => {
    setProfileBehaviours((current) => ({ ...current, [profileName]: behaviour }));
    setGamingBehaviour(behaviour);
    markUnsavedChanges();
  }, [markUnsavedChanges, setGamingBehaviour, setProfileBehaviours]);

  const updateGamingBehaviourFlag = useCallback((key: GamingBehaviourKey, value: boolean) => {
    if (!workingProfileName) {
      setGamingBehaviour((current) => ({ ...current, [key]: value }));
      return;
    }

    const currentProfileBehaviour = profileBehaviours[workingProfileName] ?? gamingBehaviour;
    setGamingBehaviourForProfile(workingProfileName, { ...currentProfileBehaviour, [key]: value });
  }, [gamingBehaviour, profileBehaviours, workingProfileName, setGamingBehaviour, setGamingBehaviourForProfile]);

  const setProfileLauncher = useCallback((profileName: string, nextLauncher: LauncherType) => {
    setProfileLaunchers((current) => ({ ...current, [profileName]: nextLauncher }));
    setLauncher(nextLauncher);
    markUnsavedChanges();
  }, [markUnsavedChanges, setLauncher, setProfileLaunchers]);

  const updateProfileHotkey = useCallback((profileName: string, key: keyof ProfileHotkeysState, value: string): boolean => {
    const normalizedValue = normalizeHotkeyForDisplay(value);
    const assignment = key === 'enterProfile'
      ? tryAssignProfileHotkey(profileName, normalizedValue, profileHotkeys, workModeHotkey)
      : { accepted: true, value: profileHotkeys, validationResult: 'Valid' as const };
    if (!assignment.accepted) {
      console.info('[SHORTCUT ASSIGNMENT]', { CurrentSaved: profileHotkeys[profileName]?.enterProfile ?? '', Candidate: normalizedValue, ValidationResult: 'Conflict', DraftUpdated: false, Persisted: false });
      triggerValidationError(t('profile.duplicateHotkeyOther'), setProfileHotkeyValidationError);
      return false;
    }

    setProfileHotkeyValidationError(null);
    setError(null);
    setProfileHotkeys(assignment.value);
    markUnsavedChanges();

    if (workingProfileName === profileName) {
      setHotkeys((current) => ({ ...current, gamingMode: key === 'enterProfile' ? normalizedValue : current.gamingMode }));
    }
    console.info('[SHORTCUT ASSIGNMENT]', { CurrentSaved: profileHotkeys[profileName]?.enterProfile ?? '', Candidate: normalizedValue, ValidationResult: 'Valid', DraftUpdated: true, Persisted: false });
    return true;
  }, [markUnsavedChanges, profileHotkeys, setError, setHotkeys, setProfileHotkeyValidationError, setProfileHotkeys, workModeHotkey, workingProfileName]);

  const updateProfileControllerShortcut = useCallback((profileName: string, buttons: string[]): boolean => {
    const assignment = tryAssignProfileControllerShortcut(profileName, buttons, profileControllerShortcuts, workModeControllerShortcut);
    if (!assignment.accepted) {
      console.info('[SHORTCUT ASSIGNMENT]', { CurrentSaved: profileControllerShortcuts[profileName]?.buttons ?? [], Candidate: buttons, ValidationResult: 'Conflict', DraftUpdated: false, Persisted: false });
      triggerValidationError(t('profile.duplicateControllerOther'), setProfileControllerValidationError);
      return false;
    }

    setProfileControllerValidationError(null);
    setError(null);
    setProfileControllerShortcuts(assignment.value);
    markUnsavedChanges();
    console.info('[SHORTCUT ASSIGNMENT]', { CurrentSaved: profileControllerShortcuts[profileName]?.buttons ?? [], Candidate: buttons, ValidationResult: 'Valid', DraftUpdated: true, Persisted: false });
    return true;
  }, [markUnsavedChanges, profileControllerShortcuts, setError, setProfileControllerShortcuts, setProfileControllerValidationError, workModeControllerShortcut]);

  const updateWorkModeControllerShortcut = useCallback((buttons: string[]): boolean => {
    const assignment = tryAssignWorkModeControllerShortcut(buttons, profileControllerShortcuts, workModeControllerShortcut);
    if (!assignment.accepted) {
      console.info('[SHORTCUT ASSIGNMENT]', { CurrentSaved: workModeControllerShortcut?.buttons ?? [], Candidate: buttons, ValidationResult: 'Conflict', DraftUpdated: false, Persisted: false });
      triggerValidationError(t('profile.duplicateControllerProfile'), setProfileControllerValidationError);
      return false;
    }

    setProfileControllerValidationError(null);
    setError(null);
    setWorkModeControllerShortcut(assignment.value);
    markUnsavedChanges();
    console.info('[SHORTCUT ASSIGNMENT]', { CurrentSaved: workModeControllerShortcut?.buttons ?? [], Candidate: buttons, ValidationResult: 'Valid', DraftUpdated: true, Persisted: false });
    return true;
  }, [markUnsavedChanges, profileControllerShortcuts, setError, setProfileControllerValidationError, setWorkModeControllerShortcut, workModeControllerShortcut]);

  const updateWorkModeHotkey = useCallback((value: string): boolean => {
    const assignment = tryAssignWorkModeHotkey(value, profileHotkeys);
    if (!assignment.accepted) {
      console.info('[SHORTCUT ASSIGNMENT]', { CurrentSaved: workModeHotkey, Candidate: value, ValidationResult: 'Conflict', DraftUpdated: false, Persisted: false });
      triggerValidationError(t('profile.duplicateHotkeyProfile'), setProfileHotkeyValidationError);
      return false;
    }

    setProfileHotkeyValidationError(null);
    setError(null);
    setHotkeys((current) => ({ ...current, workMode: assignment.value }));
    markUnsavedChanges();
    console.info('[SHORTCUT ASSIGNMENT]', { CurrentSaved: workModeHotkey, Candidate: value, ValidationResult: 'Valid', DraftUpdated: true, Persisted: false });
    return true;
  }, [markUnsavedChanges, profileHotkeys, setError, setHotkeys, setProfileHotkeyValidationError]);

  return {
    setProfileLauncher,
    updateGamingBehaviourFlag,
    updateProfileHotkey,
    updateProfileControllerShortcut,
    updateWorkModeControllerShortcut,
    updateWorkModeHotkey
  };
}
