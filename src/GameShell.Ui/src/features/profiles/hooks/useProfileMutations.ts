import { updateProfile, type Profile, type ProfileBehaviour } from '../profileModel';
import { useCallback, type Dispatch, type SetStateAction } from 'react';
import { useTranslation } from 'react-i18next';
import { normalizeHotkeyForDisplay } from '../../shortcuts/hotkeys';
import {
  type GamingProfileBehaviourMap,
  type GamingProfileControllerShortcutMap,
  type GamingProfileHotkeysMap,
  type ProfileHotkeysState
} from '../utils';
import { tryAssignProfileControllerShortcut, tryAssignProfileHotkey, tryAssignWorkModeControllerShortcut, tryAssignWorkModeHotkey } from '../shortcutAssignment';
import type { ControllerShortcutSettings } from '../../../bridge/contracts';
import type { LauncherType } from '../profileCommands';
import { triggerValidationError } from '../../../shared/validation';

type GamingBehaviourState = ProfileBehaviour;

type GamingBehaviourKey = keyof GamingBehaviourState;


type UseProfileMutationsOptions = {
  setProfiles: Dispatch<SetStateAction<Profile[]>>;
  workingProfileName: string;
  gamingBehaviour: GamingBehaviourState;
  profileBehaviours: GamingProfileBehaviourMap;
  profileHotkeys: GamingProfileHotkeysMap;
  profileControllerShortcuts: GamingProfileControllerShortcutMap;
  workModeControllerShortcut: ControllerShortcutSettings | null;
  workModeHotkey: string;
  setGamingBehaviour: Dispatch<SetStateAction<GamingBehaviourState>>;
  setLauncher: Dispatch<SetStateAction<LauncherType>>;
  setWorkModeControllerShortcut: Dispatch<SetStateAction<ControllerShortcutSettings | null>>;
  setHotkeys: Dispatch<SetStateAction<{ consoleMode: string; gamingMode: string; workMode: string }>>;
  setProfileHotkeyValidationError: Dispatch<SetStateAction<string | null>>;
  setProfileControllerValidationError: Dispatch<SetStateAction<string | null>>;
  setError: Dispatch<SetStateAction<string | null>>;
  markUnsavedChanges: () => void;
};

export function useProfileMutations({
  setProfiles,
  workingProfileName,
  gamingBehaviour,
  profileBehaviours,
  profileHotkeys,
  profileControllerShortcuts,
  workModeControllerShortcut,
  workModeHotkey,
  setGamingBehaviour,
  setLauncher,
  setWorkModeControllerShortcut,
  setHotkeys,
  setProfileHotkeyValidationError,
  setProfileControllerValidationError,
  setError,
  markUnsavedChanges
}: UseProfileMutationsOptions) {
  const { t } = useTranslation();

  const setGamingBehaviourForProfile = useCallback((profileName: string, behaviour: GamingBehaviourState) => {
    setProfiles(current => updateProfile(current, profileName, { behaviour }));
    setGamingBehaviour(behaviour);
    markUnsavedChanges();
  }, [markUnsavedChanges, setGamingBehaviour, setProfiles]);

  const updateGamingBehaviourFlag = useCallback((key: GamingBehaviourKey, value: boolean) => {
    if (!workingProfileName) {
      setGamingBehaviour((current) => ({ ...current, [key]: value }));
      return;
    }

    const currentProfileBehaviour = profileBehaviours[workingProfileName] ?? gamingBehaviour;
    setGamingBehaviourForProfile(workingProfileName, { ...currentProfileBehaviour, [key]: value });
  }, [gamingBehaviour, profileBehaviours, workingProfileName, setGamingBehaviour, setGamingBehaviourForProfile, setProfiles, setGamingBehaviour]);

  const setProfileLauncher = useCallback((profileName: string, nextLauncher: LauncherType) => {
    setLauncher(nextLauncher);
    const behaviour = profileBehaviours[profileName] ?? gamingBehaviour;
    const nextBehaviour = { ...behaviour, startLauncher: nextLauncher !== 'None', bringLauncherToForeground: nextLauncher !== 'None' };
    setProfiles(current => updateProfile(current, profileName, { application: nextLauncher, behaviour: nextBehaviour }));
    setGamingBehaviour(nextBehaviour);
    markUnsavedChanges();
  }, [markUnsavedChanges, setLauncher, setProfiles, profileBehaviours, gamingBehaviour, setGamingBehaviourForProfile, setProfiles, setGamingBehaviour]);

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
    setProfiles(current => updateProfile(current, profileName, { hotkeys: assignment.value[profileName] }));
    markUnsavedChanges();

    if (workingProfileName === profileName) {
      setHotkeys((current) => ({ ...current, gamingMode: key === 'enterProfile' ? normalizedValue : current.gamingMode }));
    }
    console.info('[SHORTCUT ASSIGNMENT]', { CurrentSaved: profileHotkeys[profileName]?.enterProfile ?? '', Candidate: normalizedValue, ValidationResult: 'Valid', DraftUpdated: true, Persisted: false });
    return true;
  }, [markUnsavedChanges, profileHotkeys, setError, setHotkeys, setProfileHotkeyValidationError, setProfiles, workModeHotkey, workingProfileName]);

  const updateProfileControllerShortcut = useCallback((profileName: string, buttons: string[]): boolean => {
    const assignment = tryAssignProfileControllerShortcut(profileName, buttons, profileControllerShortcuts, workModeControllerShortcut);
    if (!assignment.accepted) {
      console.info('[SHORTCUT ASSIGNMENT]', { CurrentSaved: profileControllerShortcuts[profileName]?.buttons ?? [], Candidate: buttons, ValidationResult: 'Conflict', DraftUpdated: false, Persisted: false });
      triggerValidationError(t('profile.duplicateControllerOther'), setProfileControllerValidationError);
      return false;
    }

    setProfileControllerValidationError(null);
    setError(null);
    setProfiles(current => updateProfile(current, profileName, { controllerShortcut: assignment.value[profileName] }));
    markUnsavedChanges();
    console.info('[SHORTCUT ASSIGNMENT]', { CurrentSaved: profileControllerShortcuts[profileName]?.buttons ?? [], Candidate: buttons, ValidationResult: 'Valid', DraftUpdated: true, Persisted: false });
    return true;
  }, [markUnsavedChanges, profileControllerShortcuts, setError, setProfiles, setProfileControllerValidationError, workModeControllerShortcut]);

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
