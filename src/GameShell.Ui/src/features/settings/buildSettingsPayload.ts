import { normalizeControllerShortcut } from '../../utils/controllers';
import { normalizeHotkeyForDisplay } from '../../utils/hotkeys';
import { getLauncherSettings, type LauncherType } from '../../utils/modes';
import { resolvePcGamingBehaviourProfileName, type PcGamingProfileBehaviourMap, type PcGamingProfileControllerShortcutMap, type PcGamingProfileHotkeysMap, type PcGamingProfileLauncherMap } from '../profiles/utils';
import type { AppSettings, DisplayIdentity, DisplayLayoutProfile, DisplayLayoutSnapshot } from '../../types/bridge';

type PcGamingBehaviourState = Pick<AppSettings['pcGaming'], 'hideDesktopIcons' | 'hideTaskbar' | 'closeVisibleWindows' | 'minimizeRemainingWindows' | 'moveCursorToCorner' | 'hideCursor' | 'startLauncher' | 'bringLauncherToForeground'>;
type ConsoleBehaviourState = Pick<AppSettings['gameShell'], 'hideDesktopIcons' | 'hideTaskbar' | 'closeVisibleWindows' | 'minimizeRemainingWindows' | 'moveCursorToCorner' | 'hideCursor' | 'startLauncher' | 'bringLauncherToForeground'>;
type WorkBehaviourState = Pick<AppSettings['workMode'], 'closeLauncher' | 'showTaskbars' | 'showDesktopIcons' | 'doNotMinimizeWindows'>;
type HotkeySettingsState = { consoleMode: string; pcGamingMode: string; workMode: string };
type FinishOptions = { startWithWindows: boolean; startMinimized: boolean; startMenuShortcut: boolean; desktopShortcut: boolean };

export type SettingsPayloadOverrides = {
  pcGamingProfiles?: DisplayLayoutProfile[];
  activePcGamingProfileName?: string | null;
  pcGamingLayout?: DisplayLayoutSnapshot | null;
  pcGamingProfileBehaviours?: PcGamingProfileBehaviourMap;
  pcGamingProfileLaunchers?: PcGamingProfileLauncherMap;
  pcGamingProfileHotkeys?: PcGamingProfileHotkeysMap;
  pcGamingProfileControllerShortcuts?: PcGamingProfileControllerShortcutMap;
};

export type BuildSettingsPayloadState = {
  settings: AppSettings | null;
  selectedConsoleDisplay: DisplayIdentity | null;
  selectedConsoleDisplays: DisplayIdentity[];
  pcGamingLayout: DisplayLayoutSnapshot | null;
  pcGamingProfiles: DisplayLayoutProfile[];
  activePcGamingProfileName: string | null;
  pcGamingProfileBehaviours: PcGamingProfileBehaviourMap;
  pcGamingProfileLaunchers: PcGamingProfileLauncherMap;
  pcGamingProfileHotkeys: PcGamingProfileHotkeysMap;
  pcGamingProfileControllerShortcuts: PcGamingProfileControllerShortcutMap;
  workModeControllerShortcut: AppSettings['controller']['workMode'];
  pcGamingBehaviour: PcGamingBehaviourState;
  launcher: LauncherType;
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
  const effectivePcGamingProfiles = overrides?.pcGamingProfiles ?? state.pcGamingProfiles;
  const effectiveActivePcGamingProfileName = overrides?.activePcGamingProfileName ?? state.activePcGamingProfileName;
  const effectivePcGamingLayout = overrides?.pcGamingLayout ?? state.pcGamingLayout;
  const effectivePcGamingProfileBehaviours = { ...overrides?.pcGamingProfileBehaviours, ...state.pcGamingProfileBehaviours };
  const effectivePcGamingProfileLaunchers = { ...overrides?.pcGamingProfileLaunchers, ...state.pcGamingProfileLaunchers };
  const effectivePcGamingProfileHotkeys = { ...overrides?.pcGamingProfileHotkeys, ...state.pcGamingProfileHotkeys };
  const effectivePcGamingProfileControllerShortcuts = { ...overrides?.pcGamingProfileControllerShortcuts, ...state.pcGamingProfileControllerShortcuts };
  const activeProfileName = resolvePcGamingBehaviourProfileName(effectiveActivePcGamingProfileName, effectivePcGamingProfiles);
  const activeProfileBehaviour = activeProfileName ? effectivePcGamingProfileBehaviours[activeProfileName] : null;
  const activeProfileLauncher = activeProfileName ? effectivePcGamingProfileLaunchers[activeProfileName] : null;
  const activeProfileHotkeys = activeProfileName ? effectivePcGamingProfileHotkeys[activeProfileName] : null;
  const activeProfileControllerShortcut = activeProfileName ? effectivePcGamingProfileControllerShortcuts[activeProfileName] : null;
  const savedPcGamingBehaviour = activeProfileBehaviour ?? state.pcGamingBehaviour;
  const savedLauncher = activeProfileLauncher ?? state.launcher;
  const launcherSettings = getLauncherSettings(savedLauncher);
  const savedHotkeys = activeProfileHotkeys ?? { enterProfile: state.hotkeys.pcGamingMode };

  return {
    display: {
      consoleDisplay: state.selectedConsoleDisplay,
      consoleDisplays: state.selectedConsoleDisplays,
      pcGamingLayout: effectivePcGamingLayout,
      pcGamingProfiles: effectivePcGamingProfiles,
      activePcGamingProfileName: effectiveActivePcGamingProfileName,
      pcGamingProfileConfigurations: effectivePcGamingProfiles.map((profile) => ({
        profileName: profile.name,
        behaviour: effectivePcGamingProfileBehaviours[profile.name] ?? savedPcGamingBehaviour,
        launcher: { ...getLauncherSettings(effectivePcGamingProfileLaunchers[profile.name] ?? savedLauncher), targetDisplayId: state.selectedConsoleDisplay?.monitorDevicePath ?? null },
        hotkeys: { enterProfile: normalizeHotkeyForDisplay(effectivePcGamingProfileHotkeys[profile.name]?.enterProfile ?? savedHotkeys.enterProfile ?? ''), enterWorkMode: '' },
        controllerShortcut: normalizeControllerShortcut(effectivePcGamingProfileControllerShortcuts[profile.name] ?? null)
      })),
      pcGamingProfileBehaviours: effectivePcGamingProfiles.map((profile) => ({ profileName: profile.name, behaviour: effectivePcGamingProfileBehaviours[profile.name] ?? savedPcGamingBehaviour }))
    },
    launcher: { type: launcherSettings.type, executablePath: launcherSettings.executablePath, arguments: launcherSettings.arguments, targetDisplayId: state.selectedConsoleDisplay?.monitorDevicePath ?? null },
    gameShell: { ...state.consoleBehaviour, completionDelaySeconds: state.settings?.gameShell.completionDelaySeconds ?? 5 },
    pcGaming: { ...savedPcGamingBehaviour, protectedProcessNames: state.settings?.pcGaming?.protectedProcessNames ?? [], closeOnlyProcessNames: state.settings?.pcGaming?.closeOnlyProcessNames ?? [], completionDelaySeconds: state.settings?.pcGaming?.completionDelaySeconds ?? 5 },
    workMode: { ...state.workBehaviour, taskbarRestoreDelaySeconds: state.settings?.workMode.taskbarRestoreDelaySeconds ?? 3, desktopIconsDelaySeconds: state.settings?.workMode.desktopIconsDelaySeconds ?? 15, completionDelaySeconds: state.settings?.workMode.completionDelaySeconds ?? 20 },
    audio: { switchAudioInConsoleMode: state.settings?.audio?.switchAudioInConsoleMode ?? false, restoreAudioInWorkMode: state.settings?.audio?.restoreAudioInWorkMode ?? false, consoleAudioDeviceId: state.settings?.audio?.consoleAudioDeviceId ?? null, consoleAudioDeviceName: state.settings?.audio?.consoleAudioDeviceName ?? null },
    controller: { enabled: Object.values(effectivePcGamingProfileControllerShortcuts).some((shortcut) => (shortcut?.buttons.length ?? 0) > 0) || (state.workModeControllerShortcut?.buttons.length ?? 0) > 0 || state.settings?.controller?.enabled || false, consoleMode: state.settings?.controller?.consoleMode ?? null, pcGamingMode: activeProfileName ? normalizeControllerShortcut(activeProfileControllerShortcut) : normalizeControllerShortcut(state.settings?.controller?.pcGamingMode ?? null), workMode: normalizeControllerShortcut(state.workModeControllerShortcut) },
    hotkeys: { consoleMode: normalizeHotkeyForDisplay(state.hotkeys.consoleMode), pcGamingMode: normalizeHotkeyForDisplay(savedHotkeys.enterProfile || state.hotkeys.pcGamingMode), workMode: normalizeHotkeyForDisplay(state.hotkeys.workMode) },
    startup: { startWithWindows: state.finishOptions.startWithWindows, startMinimizedToTray: state.finishOptions.startMinimized, createStartMenuShortcut: state.finishOptions.startMenuShortcut, createDesktopShortcut: state.finishOptions.desktopShortcut },
    ui: { closeToTray: true, isFirstRunCompleted }
  };
}
