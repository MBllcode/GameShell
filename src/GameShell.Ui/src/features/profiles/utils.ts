import type { AppSettings, ControllerShortcutSettings, DisplayIdentity, DisplayLayoutProfile } from '../../types/bridge.ts';
import { normalizeControllerShortcut } from '../../utils/controllers.ts';
import { normalizeHotkeyForDisplay, normalizeHotkeyValue } from '../../utils/hotkeys.ts';
import { toLauncherType, type LauncherType } from '../../utils/modes.ts';

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

export function buildProfileBehaviourMap(profiles: DisplayLayoutProfile[], configurations: NonNullable<AppSettings['display']['gamingProfileConfigurations']>, savedBehaviours: NonNullable<AppSettings['display']['gamingProfileBehaviours']>, fallbackBehaviour: GamingBehaviourState): GamingProfileBehaviourMap {
  const map: GamingProfileBehaviourMap = {};
  profiles.forEach((profile) => {
    const configured = configurations.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    const saved = savedBehaviours.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    map[profile.name] = toGamingBehaviourState(configured?.behaviour ?? saved?.behaviour ?? fallbackBehaviour);
  });
  return map;
}

export function buildProfileLauncherMap(profiles: DisplayLayoutProfile[], configurations: NonNullable<AppSettings['display']['gamingProfileConfigurations']>, fallbackLauncher: LauncherType): GamingProfileLauncherMap {
  const map: GamingProfileLauncherMap = {};
  profiles.forEach((profile) => {
    const configured = configurations.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    map[profile.name] = toLauncherType(configured?.launcher?.type ?? fallbackLauncher);
  });
  return map;
}

export function buildProfileHotkeysMap(profiles: DisplayLayoutProfile[], configurations: NonNullable<AppSettings['display']['gamingProfileConfigurations']>, fallbackHotkey: ProfileHotkeysState): GamingProfileHotkeysMap {
  const map: GamingProfileHotkeysMap = {};
  profiles.forEach((profile) => {
    const configured = configurations.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    map[profile.name] = {
      enterProfile: normalizeHotkeyForDisplay(configured?.hotkeys?.enterProfile ?? fallbackHotkey.enterProfile)
    };
  });
  return map;
}

export function buildProfileControllerShortcutMap(profiles: DisplayLayoutProfile[], configurations: NonNullable<AppSettings['display']['gamingProfileConfigurations']>, fallbackShortcut: ControllerShortcutSettings | null): GamingProfileControllerShortcutMap {
  const map: GamingProfileControllerShortcutMap = {};
  profiles.forEach((profile) => {
    const configured = configurations.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    map[profile.name] = normalizeControllerShortcut(configured?.controllerShortcut ?? fallbackShortcut);
  });
  return map;
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
