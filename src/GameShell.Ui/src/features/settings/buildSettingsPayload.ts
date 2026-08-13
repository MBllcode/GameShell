import { normalizeLanguageCode } from '../../i18n/language';
import { normalizeControllerShortcut } from '../../utils/controllers';
import { normalizeHotkeyForDisplay } from '../../utils/hotkeys';
import { getLauncherSettings, type LauncherType } from '../../utils/modes';
import { resolveGamingBehaviourProfileName, type GamingProfileBehaviourMap, type GamingProfileControllerShortcutMap, type GamingProfileHotkeysMap, type GamingProfileLauncherMap } from '../profiles/utils';
import type { AppSettings, DisplayIdentity, DisplayLayoutProfile, DisplayLayoutSnapshot } from '../../types/bridge';

type GamingBehaviourState = Pick<AppSettings['gaming'], 'hideDesktopIcons' | 'hideTaskbar' | 'closeVisibleWindows' | 'minimizeRemainingWindows' | 'moveCursorToCorner' | 'hideCursor' | 'startLauncher' | 'bringLauncherToForeground'>;
type ConsoleBehaviourState = Pick<AppSettings['gameShell'], 'hideDesktopIcons' | 'hideTaskbar' | 'closeVisibleWindows' | 'minimizeRemainingWindows' | 'moveCursorToCorner' | 'hideCursor' | 'startLauncher' | 'bringLauncherToForeground'>;
type WorkBehaviourState = Pick<AppSettings['workMode'], 'closeLauncher' | 'showTaskbars' | 'showDesktopIcons' | 'doNotMinimizeWindows'>;
type HotkeySettingsState = { consoleMode: string; gamingMode: string; workMode: string };
type FinishOptions = { startWithWindows: boolean; startMinimized: boolean; startMenuShortcut: boolean; desktopShortcut: boolean };

export type SettingsPayloadOverrides = {
  gamingProfiles?: DisplayLayoutProfile[];
  activeGamingProfileName?: string | null;
  gamingLayout?: DisplayLayoutSnapshot | null;
  gamingProfileBehaviours?: GamingProfileBehaviourMap;
  gamingProfileLaunchers?: GamingProfileLauncherMap;
  gamingProfileHotkeys?: GamingProfileHotkeysMap;
  gamingProfileControllerShortcuts?: GamingProfileControllerShortcutMap;
};

export type BuildSettingsPayloadState = {
  settings: AppSettings | null;
  selectedConsoleDisplay: DisplayIdentity | null;
  selectedConsoleDisplays: DisplayIdentity[];
  gamingLayout: DisplayLayoutSnapshot | null;
  gamingProfiles: DisplayLayoutProfile[];
  activeGamingProfileName: string | null;
  gamingProfileBehaviours: GamingProfileBehaviourMap;
  gamingProfileLaunchers: GamingProfileLauncherMap;
  gamingProfileHotkeys: GamingProfileHotkeysMap;
  gamingProfileControllerShortcuts: GamingProfileControllerShortcutMap;
  workModeControllerShortcut: AppSettings['controller']['workMode'];
  gamingBehaviour: GamingBehaviourState;
  launcher: LauncherType;
  language: string;
  hotkeys: HotkeySettingsState;
  consoleBehaviour: ConsoleBehaviourState;
  workBehaviour: WorkBehaviourState;
  finishOptions: FinishOptions;
};

export function buildSettingsPayload(
  state: BuildSettingsPayloadState,
  isFirstRunCompleted: boolean,
  overrides?: SettingsPayloadOverrides
): AppSettings {
  const effectiveGamingProfiles = overrides?.gamingProfiles ?? state.gamingProfiles;
  const effectiveActiveGamingProfileName = overrides?.activeGamingProfileName ?? state.activeGamingProfileName;
  const effectiveGamingLayout = overrides?.gamingLayout ?? state.gamingLayout;
  const effectiveGamingProfileBehaviours = { ...overrides?.gamingProfileBehaviours, ...state.gamingProfileBehaviours };
  const effectiveGamingProfileLaunchers = { ...overrides?.gamingProfileLaunchers, ...state.gamingProfileLaunchers };
  const effectiveGamingProfileHotkeys = { ...overrides?.gamingProfileHotkeys, ...state.gamingProfileHotkeys };
  const effectiveGamingProfileControllerShortcuts = { ...overrides?.gamingProfileControllerShortcuts, ...state.gamingProfileControllerShortcuts };
  const activeProfileName = resolveGamingBehaviourProfileName(effectiveActiveGamingProfileName, effectiveGamingProfiles);
  const activeProfileBehaviour = activeProfileName ? effectiveGamingProfileBehaviours[activeProfileName] : null;
  const activeProfileLauncher = activeProfileName ? effectiveGamingProfileLaunchers[activeProfileName] : null;
  const activeProfileHotkeys = activeProfileName ? effectiveGamingProfileHotkeys[activeProfileName] : null;
  const activeProfileControllerShortcut = activeProfileName ? effectiveGamingProfileControllerShortcuts[activeProfileName] : null;
  const savedGamingBehaviour = activeProfileBehaviour ?? state.gamingBehaviour;
  const savedLauncher = activeProfileLauncher ?? state.launcher;
  const launcherSettings = getLauncherSettings(savedLauncher);
  const savedHotkeys = activeProfileHotkeys ?? { enterProfile: state.hotkeys.gamingMode };

  return {
    display: {
      consoleDisplay: state.selectedConsoleDisplay,
      consoleDisplays: state.selectedConsoleDisplays,
      gamingLayout: effectiveGamingLayout,
      gamingProfiles: effectiveGamingProfiles,
      activeGamingProfileName: effectiveActiveGamingProfileName,
      gamingProfileConfigurations: effectiveGamingProfiles.map((profile) => ({
        profileName: profile.name,
        behaviour: effectiveGamingProfileBehaviours[profile.name] ?? savedGamingBehaviour,
        launcher: { ...getLauncherSettings(effectiveGamingProfileLaunchers[profile.name] ?? savedLauncher), targetDisplayId: state.selectedConsoleDisplay?.monitorDevicePath ?? null },
        hotkeys: { enterProfile: normalizeHotkeyForDisplay(effectiveGamingProfileHotkeys[profile.name]?.enterProfile ?? savedHotkeys.enterProfile ?? ''), enterWorkMode: '' },
        controllerShortcut: normalizeControllerShortcut(effectiveGamingProfileControllerShortcuts[profile.name] ?? null)
      })),
      gamingProfileBehaviours: effectiveGamingProfiles.map((profile) => ({ profileName: profile.name, behaviour: effectiveGamingProfileBehaviours[profile.name] ?? savedGamingBehaviour }))
    },
    launcher: { type: launcherSettings.type, executablePath: launcherSettings.executablePath, arguments: launcherSettings.arguments, targetDisplayId: state.selectedConsoleDisplay?.monitorDevicePath ?? null },
    gameShell: { ...state.consoleBehaviour, completionDelaySeconds: state.settings?.gameShell.completionDelaySeconds ?? 5 },
    gaming: { ...savedGamingBehaviour, protectedProcessNames: state.settings?.gaming?.protectedProcessNames ?? [], closeOnlyProcessNames: state.settings?.gaming?.closeOnlyProcessNames ?? [], completionDelaySeconds: state.settings?.gaming?.completionDelaySeconds ?? 5 },
    workMode: { ...state.workBehaviour, taskbarRestoreDelaySeconds: state.settings?.workMode.taskbarRestoreDelaySeconds ?? 3, desktopIconsDelaySeconds: state.settings?.workMode.desktopIconsDelaySeconds ?? 15, completionDelaySeconds: state.settings?.workMode.completionDelaySeconds ?? 20 },
    audio: { switchAudioInConsoleMode: state.settings?.audio?.switchAudioInConsoleMode ?? false, restoreAudioInWorkMode: state.settings?.audio?.restoreAudioInWorkMode ?? false, consoleAudioDeviceId: state.settings?.audio?.consoleAudioDeviceId ?? null, consoleAudioDeviceName: state.settings?.audio?.consoleAudioDeviceName ?? null },
    controller: { enabled: Object.values(effectiveGamingProfileControllerShortcuts).some((shortcut) => (shortcut?.buttons.length ?? 0) > 0) || (state.workModeControllerShortcut?.buttons.length ?? 0) > 0 || state.settings?.controller?.enabled || false, consoleMode: state.settings?.controller?.consoleMode ?? null, gamingMode: activeProfileName ? normalizeControllerShortcut(activeProfileControllerShortcut) : normalizeControllerShortcut(state.settings?.controller?.gamingMode ?? null), workMode: normalizeControllerShortcut(state.workModeControllerShortcut) },
    hotkeys: { consoleMode: normalizeHotkeyForDisplay(state.hotkeys.consoleMode), gamingMode: normalizeHotkeyForDisplay(savedHotkeys.enterProfile || state.hotkeys.gamingMode), workMode: normalizeHotkeyForDisplay(state.hotkeys.workMode) },
    startup: { startWithWindows: state.finishOptions.startWithWindows, startMinimizedToTray: state.finishOptions.startMinimized, createStartMenuShortcut: state.finishOptions.startMenuShortcut, createDesktopShortcut: state.finishOptions.desktopShortcut },
    ui: { closeToTray: true, isFirstRunCompleted, language: normalizeLanguageCode(state.language) }
  };
}
