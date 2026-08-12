import type { ControllerShortcutSettings } from '../../types/bridge.ts';
import { isDuplicateProfileControllerShortcut, isDuplicateWorkModeControllerShortcut, isSameControllerShortcut, normalizeControllerButtons } from '../../utils/controllers.ts';
import { normalizeHotkeyForDisplay } from '../../utils/hotkeys.ts';
import { isDuplicateProfileEnterHotkey, isDuplicateWorkModeHotkey, type PcGamingProfileControllerShortcutMap, type PcGamingProfileHotkeysMap } from './utils.ts';

type HotkeyState = { enterProfile: string };

type AssignmentResult<T> = {
  accepted: boolean;
  value: T;
  validationResult: 'Valid' | 'Conflict';
};

export function tryAssignProfileHotkey(profileName: string, candidate: string, profileHotkeys: PcGamingProfileHotkeysMap, workModeHotkey: string): AssignmentResult<PcGamingProfileHotkeysMap> {
  const normalizedCandidate = normalizeHotkeyForDisplay(candidate);
  const conflict = isDuplicateProfileEnterHotkey(profileName, normalizedCandidate, profileHotkeys)
    || (normalizedCandidate.length > 0 && normalizedCandidate.toUpperCase() === workModeHotkey.trim().toUpperCase());
  if (conflict) return { accepted: false, value: profileHotkeys, validationResult: 'Conflict' };

  return {
    accepted: true,
    value: { ...profileHotkeys, [profileName]: { ...(profileHotkeys[profileName] ?? { enterProfile: '' }), enterProfile: normalizedCandidate } },
    validationResult: 'Valid'
  };
}

export function tryAssignWorkModeHotkey(candidate: string, profileHotkeys: PcGamingProfileHotkeysMap): AssignmentResult<string> {
  const normalizedCandidate = normalizeHotkeyForDisplay(candidate);
  if (isDuplicateWorkModeHotkey(normalizedCandidate, profileHotkeys)) {
    return { accepted: false, value: candidate, validationResult: 'Conflict' };
  }
  return { accepted: true, value: normalizedCandidate, validationResult: 'Valid' };
}

export function tryAssignProfileControllerShortcut(profileName: string, candidate: string[], profileShortcuts: PcGamingProfileControllerShortcutMap, workModeShortcut: ControllerShortcutSettings | null): AssignmentResult<PcGamingProfileControllerShortcutMap> {
  const normalizedCandidate = normalizeControllerButtons(candidate);
  const conflict = isDuplicateProfileControllerShortcut(profileName, normalizedCandidate, profileShortcuts)
    || isSameControllerShortcut(normalizedCandidate, workModeShortcut?.buttons);
  if (conflict) return { accepted: false, value: profileShortcuts, validationResult: 'Conflict' };

  return {
    accepted: true,
    value: {
      ...profileShortcuts,
      [profileName]: normalizedCandidate.length === 0 ? null : {
        buttons: normalizedCandidate,
        holdDurationMs: profileShortcuts[profileName]?.holdDurationMs ?? 700
      }
    },
    validationResult: 'Valid'
  };
}

export function tryAssignWorkModeControllerShortcut(candidate: string[], profileShortcuts: PcGamingProfileControllerShortcutMap, currentShortcut: ControllerShortcutSettings | null): AssignmentResult<ControllerShortcutSettings | null> {
  const normalizedCandidate = normalizeControllerButtons(candidate);
  if (isDuplicateWorkModeControllerShortcut(normalizedCandidate, profileShortcuts)) {
    return { accepted: false, value: currentShortcut, validationResult: 'Conflict' };
  }
  return {
    accepted: true,
    value: normalizedCandidate.length === 0 ? null : { buttons: normalizedCandidate, holdDurationMs: currentShortcut?.holdDurationMs ?? 700 },
    validationResult: 'Valid'
  };
}

export type { AssignmentResult, HotkeyState };
