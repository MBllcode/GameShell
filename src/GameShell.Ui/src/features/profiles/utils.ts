import type { AppSettings, ControllerShortcutSettings, DisplayLayoutProfile } from '../../bridge/contracts.ts';
import { normalizeHotkeyValue } from '../shortcuts/hotkeys.ts';
import { type LauncherType } from './profileCommands.ts';

type ProfileHotkeysState = { enterProfile: string };
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
type GamingProfileBehaviourMap = Record<string, GamingBehaviourState>;
type GamingProfileLauncherMap = Record<string, LauncherType>;
type GamingProfileHotkeysMap = Record<string, ProfileHotkeysState>;
type GamingProfileControllerShortcutMap = Record<string, ControllerShortcutSettings | null>;

export function normalizeGamingProfileName(value: string) {
  return value.trim().toUpperCase();
}

export function toGamingBehaviourState(behaviour: Partial<GamingBehaviourState> | AppSettings['gaming'] | null | undefined): GamingBehaviourState {
  return {
    hideDesktopIcons: behaviour?.hideDesktopIcons ?? true,
    hideTaskbar: behaviour?.hideTaskbar ?? true,
    closeVisibleWindows: behaviour?.closeVisibleWindows ?? true,
    minimizeRemainingWindows: behaviour?.minimizeRemainingWindows ?? true,
    moveCursorToCorner: behaviour?.moveCursorToCorner ?? true,
    hideCursor: behaviour?.hideCursor ?? true,
    startLauncher: behaviour?.startLauncher ?? true,
    bringLauncherToForeground: behaviour?.bringLauncherToForeground ?? true
  };
}

export function resolveGamingBehaviourProfileName(activeName: string | null, profiles: DisplayLayoutProfile[]) {
  return resolveActiveGamingProfile(activeName, profiles)?.name ?? null;
}

export function isDuplicateProfileEnterHotkey(profileName: string, candidateHotkey: string, profileHotkeys: GamingProfileHotkeysMap) {
  const normalizedCandidate = normalizeHotkeyValue(candidateHotkey);
  if (!normalizedCandidate) return false;
  const currentOwnerId = normalizeOwnerId(profileName);
  return Object.entries(profileHotkeys).some(([name, hotkeys]) => normalizeOwnerId(name) !== currentOwnerId && normalizeHotkeyValue(hotkeys.enterProfile) === normalizedCandidate);
}

export function isDuplicateWorkModeHotkey(candidateHotkey: string, profileHotkeys: GamingProfileHotkeysMap) {
  const normalizedCandidate = normalizeHotkeyValue(candidateHotkey);
  return normalizedCandidate.length > 0 && Object.values(profileHotkeys).some((hotkeys) => normalizeHotkeyValue(hotkeys.enterProfile) === normalizedCandidate);
}

export function resolveActiveGamingProfile(activeName: string | null, profiles: DisplayLayoutProfile[]) {
  if (profiles.length === 0) return null;
  if (activeName) {
    const matched = profiles.find((profile) => profile.name.toLowerCase() === activeName.toLowerCase());
    if (matched) return matched;
  }
  return profiles[0];
}

export function resolvePreferredProfileNameForPostSaveSelection(draftProfileName: string, selectedProfileName: string) {
  const trimmedDraft = draftProfileName.trim();
  if (trimmedDraft.length > 0) return trimmedDraft;
  const trimmedSelected = selectedProfileName.trim();
  return trimmedSelected.length > 0 ? trimmedSelected : '';
}

function normalizeOwnerId(value: string) {
  return value.trim().toUpperCase();
}

export type { GamingBehaviourState, GamingProfileBehaviourMap, GamingProfileControllerShortcutMap, GamingProfileHotkeysMap, GamingProfileLauncherMap, ProfileHotkeysState };
