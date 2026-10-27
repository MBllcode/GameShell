import { normalizeControllerShortcut } from '../shortcuts/controllers.ts';
import type { AppSettings, ControllerShortcutSettings, DisplayLayoutProfile } from '../../bridge/contracts';
import type { LauncherType } from './profileCommands';
import { normalizeHotkeyForDisplay } from '../shortcuts/hotkeys.ts';
import { toLauncherType } from './profileCommands.ts';
import { toGamingBehaviourState } from './utils.ts';

export type ProfileBehaviour = Pick<AppSettings['gaming'], 'hideDesktopIcons' | 'hideTaskbar' | 'closeVisibleWindows' | 'minimizeRemainingWindows' | 'moveCursorToCorner' | 'hideCursor' | 'startLauncher' | 'bringLauncherToForeground'>;

export const DEFAULT_PROFILE_BEHAVIOUR: ProfileBehaviour = {
  hideDesktopIcons: false, hideTaskbar: false, closeVisibleWindows: false,
  minimizeRemainingWindows: false, moveCursorToCorner: false, hideCursor: false,
  startLauncher: false, bringLauncherToForeground: false
};

// One editable record owns all settings for a profile. Legacy JSON maps are boundary projections.
export type Profile = DisplayLayoutProfile & {
  application: LauncherType;
  behaviour: ProfileBehaviour;
  hotkeys: { enterProfile: string };
  controllerShortcut: ControllerShortcutSettings | null;
};

export function createProfile(layout: DisplayLayoutProfile, settings: Partial<Profile> = {}): Profile {
  return {
    application: 'None', behaviour: { ...DEFAULT_PROFILE_BEHAVIOUR },
    hotkeys: { enterProfile: '' }, controllerShortcut: null,
    ...layout, ...settings
  };
}

export function profilesFromSettings(settings: AppSettings): Profile[] {
  const layouts = settings.display.gamingProfiles ?? [];
  const configurations = settings.display.gamingProfileConfigurations ?? [];
  return layouts.map(layout => {
    const configured = configurations.find(entry => entry.profileName.toLowerCase() === layout.name.toLowerCase());
    const legacy = settings.display.gamingProfileBehaviours?.find(entry => entry.profileName.toLowerCase() === layout.name.toLowerCase());
    const persistedBehaviour = configured?.behaviour ?? legacy?.behaviour ?? settings.gaming;
    const behaviour = { ...persistedBehaviour, ...toGamingBehaviourState(persistedBehaviour) };
    return createProfile(layout, {
      behaviour,
      application: behaviour.startLauncher ? toLauncherType(configured?.launcher?.type ?? settings.launcher.type) : 'None',
      hotkeys: { enterProfile: normalizeHotkeyForDisplay(configured?.hotkeys?.enterProfile ?? settings.hotkeys?.gamingMode ?? '') },
      controllerShortcut: normalizeControllerShortcut(configured?.controllerShortcut ?? settings.controller?.gamingMode ?? null)
    });
  });
}

export function profileViews(profiles: Profile[]) {
  return {
    gamingProfileBehaviours: Object.fromEntries(profiles.map(p => [p.name, p.behaviour])),
    gamingProfileLaunchers: Object.fromEntries(profiles.map(p => [p.name, p.application])),
    gamingProfileHotkeys: Object.fromEntries(profiles.map(p => [p.name, p.hotkeys])),
    gamingProfileControllerShortcuts: Object.fromEntries(profiles.map(p => [p.name, p.controllerShortcut]))
  };
}

export function updateProfile(profiles: Profile[], name: string, patch: Partial<Profile>): Profile[] {
  return profiles.map(profile => profile.name.toLowerCase() === name.toLowerCase() ? { ...profile, ...patch } : profile);
}

export function profileLayout({ id, name, layout, displayTopology }: Profile): DisplayLayoutProfile {
  return { id, name, layout, displayTopology };
}
