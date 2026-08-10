import type { AppSettings, ControllerShortcutSettings, DisplayIdentity, DisplayLayoutProfile } from '../../types/bridge';
import { normalizeControllerShortcut } from '../../utils/controllers';
import { normalizeHotkeyForDisplay, normalizeHotkeyValue } from '../../utils/hotkeys';
import { toLauncherType, type LauncherType } from '../../utils/modes';

type ProfileHotkeysState = { enterProfile: string };
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
type PcGamingProfileBehaviourMap = Record<string, PcGamingBehaviourState>;
type PcGamingProfileLauncherMap = Record<string, LauncherType>;
type PcGamingProfileHotkeysMap = Record<string, ProfileHotkeysState>;
type PcGamingProfileControllerShortcutMap = Record<string, ControllerShortcutSettings | null>;

export function normalizePcGamingProfileName(value: string) {
  return value.trim().toUpperCase();
}

export function toPcGamingBehaviourState(behaviour: Partial<PcGamingBehaviourState> | AppSettings['pcGaming'] | null | undefined): PcGamingBehaviourState {
  return {
    hideDesktopIcons: behaviour?.hideDesktopIcons ?? true,
    hideTaskbar: behaviour?.hideTaskbar ?? true,
    closeVisibleWindows: behaviour?.closeVisibleWindows ?? false,
    minimizeRemainingWindows: behaviour?.minimizeRemainingWindows ?? false,
    moveCursorToCorner: behaviour?.moveCursorToCorner ?? true,
    hideCursor: behaviour?.hideCursor ?? false,
    startLauncher: behaviour?.startLauncher ?? true,
    bringLauncherToForeground: behaviour?.bringLauncherToForeground ?? true
  };
}

export function resolvePcGamingBehaviourProfileName(activeName: string | null, profiles: DisplayLayoutProfile[]) {
  return resolveActivePcGamingProfile(activeName, profiles)?.name ?? null;
}

export function buildProfileBehaviourMap(profiles: DisplayLayoutProfile[], configurations: NonNullable<AppSettings['display']['pcGamingProfileConfigurations']>, savedBehaviours: NonNullable<AppSettings['display']['pcGamingProfileBehaviours']>, fallbackBehaviour: PcGamingBehaviourState): PcGamingProfileBehaviourMap {
  const map: PcGamingProfileBehaviourMap = {};
  profiles.forEach((profile) => {
    const configured = configurations.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    const saved = savedBehaviours.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    map[profile.name] = toPcGamingBehaviourState(configured?.behaviour ?? saved?.behaviour ?? fallbackBehaviour);
  });
  return map;
}

export function buildProfileLauncherMap(profiles: DisplayLayoutProfile[], configurations: NonNullable<AppSettings['display']['pcGamingProfileConfigurations']>, fallbackLauncher: LauncherType): PcGamingProfileLauncherMap {
  const map: PcGamingProfileLauncherMap = {};
  profiles.forEach((profile) => {
    const configured = configurations.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    map[profile.name] = toLauncherType(configured?.launcher?.type ?? fallbackLauncher);
  });
  return map;
}

export function buildProfileHotkeysMap(profiles: DisplayLayoutProfile[], configurations: NonNullable<AppSettings['display']['pcGamingProfileConfigurations']>, fallbackHotkey: ProfileHotkeysState): PcGamingProfileHotkeysMap {
  const map: PcGamingProfileHotkeysMap = {};
  profiles.forEach((profile) => {
    const configured = configurations.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    map[profile.name] = {
      enterProfile: normalizeHotkeyForDisplay(configured?.hotkeys?.enterProfile ?? fallbackHotkey.enterProfile)
    };
  });
  return map;
}

export function buildProfileControllerShortcutMap(profiles: DisplayLayoutProfile[], configurations: NonNullable<AppSettings['display']['pcGamingProfileConfigurations']>, fallbackShortcut: ControllerShortcutSettings | null): PcGamingProfileControllerShortcutMap {
  const map: PcGamingProfileControllerShortcutMap = {};
  profiles.forEach((profile) => {
    const configured = configurations.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    map[profile.name] = normalizeControllerShortcut(configured?.controllerShortcut ?? fallbackShortcut);
  });
  return map;
}

export function isDuplicateProfileEnterHotkey(profileName: string, candidateHotkey: string, profileHotkeys: PcGamingProfileHotkeysMap) {
  const normalizedCandidate = normalizeHotkeyValue(candidateHotkey);
  if (!normalizedCandidate) return false;
  return Object.entries(profileHotkeys).some(([name, hotkeys]) => name.toLowerCase() !== profileName.toLowerCase() && normalizeHotkeyValue(hotkeys.enterProfile) === normalizedCandidate);
}

export function isDuplicateWorkModeHotkey(candidateHotkey: string, profileHotkeys: PcGamingProfileHotkeysMap) {
  const normalizedCandidate = normalizeHotkeyValue(candidateHotkey);
  return normalizedCandidate.length > 0 && Object.values(profileHotkeys).some((hotkeys) => normalizeHotkeyValue(hotkeys.enterProfile) === normalizedCandidate);
}

export function resolveActivePcGamingProfile(activeName: string | null, profiles: DisplayLayoutProfile[]) {
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

export type { PcGamingBehaviourState, PcGamingProfileBehaviourMap, PcGamingProfileControllerShortcutMap, PcGamingProfileHotkeysMap, PcGamingProfileLauncherMap, ProfileHotkeysState };
