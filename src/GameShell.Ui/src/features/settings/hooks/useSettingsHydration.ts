import type { AppSettings, DisplayIdentity } from '../../../types/bridge';
import { normalizeControllerShortcut } from '../../../utils/controllers';
import { normalizeHotkeyForDisplay } from '../../../utils/hotkeys';
import { toLauncherType } from '../../../utils/modes';
import { buildProfileBehaviourMap, buildProfileControllerShortcutMap, buildProfileHotkeysMap, buildProfileLauncherMap, resolveActivePcGamingProfile, toPcGamingBehaviourState, type PcGamingProfileBehaviourMap, type PcGamingProfileControllerShortcutMap, type PcGamingProfileHotkeysMap, type PcGamingProfileLauncherMap } from '../../profiles/utils';
import { defaultConsoleBehaviour, defaultFinishOptions, defaultPcGamingBehaviour, defaultWorkBehaviour } from './useSettingsFormState';
import type { SettingsPayloadOverrides } from '../buildSettingsPayload';

type Options = {
  setSelectedConsoleDisplays: (value: DisplayIdentity[]) => void;
  setSelectedConsoleDisplay: (value: DisplayIdentity | null) => void;
  setProfiles: (value: NonNullable<AppSettings['display']['pcGamingProfiles']>) => void;
  setActiveProfileName: (value: string | null) => void;
  setProfileBehaviours: (value: PcGamingProfileBehaviourMap) => void;
  setProfileLaunchers: (value: PcGamingProfileLauncherMap) => void;
  setProfileHotkeys: (value: PcGamingProfileHotkeysMap) => void;
  setProfileControllerShortcuts: (value: PcGamingProfileControllerShortcutMap) => void;
  setWorkModeControllerShortcut: (value: AppSettings['controller']['workMode']) => void;
  setSelectedProfileName: (value: string) => void;
  setLayout: (value: AppSettings['display']['pcGamingLayout']) => void;
  setHotkeys: (value: { consoleMode: string; pcGamingMode: string; workMode: string }) => void;
  setLauncher: (value: ReturnType<typeof toLauncherType>) => void;
  setConsoleBehaviour: (value: Pick<AppSettings['gameShell'], 'hideDesktopIcons' | 'hideTaskbar' | 'closeVisibleWindows' | 'minimizeRemainingWindows' | 'moveCursorToCorner' | 'hideCursor' | 'startLauncher' | 'bringLauncherToForeground'>) => void;
  setPcGamingBehaviour: (value: ReturnType<typeof toPcGamingBehaviourState>) => void;
  setWorkBehaviour: (value: Pick<AppSettings['workMode'], 'closeLauncher' | 'showTaskbars' | 'showDesktopIcons' | 'doNotMinimizeWindows'>) => void;
  setFinishOptions: (value: { startWithWindows: boolean; startMinimized: boolean; startMenuShortcut: boolean; desktopShortcut: boolean }) => void;
  setHasUnsavedChanges: (value: boolean) => void;
  pendingSettingsOverrideRef: { current: SettingsPayloadOverrides | null };
};

export function useSettingsHydration(options: Options) {
  return function applySettingsToState(loadedSettings: AppSettings) {
    const consoleDisplays = loadedSettings.display?.consoleDisplays?.length ? loadedSettings.display.consoleDisplays : loadedSettings.display?.consoleDisplay ? [loadedSettings.display.consoleDisplay] : [];
    const loadedProfiles = loadedSettings.display?.pcGamingProfiles ?? [];
    const activeProfile = resolveActivePcGamingProfile(loadedSettings.display?.activePcGamingProfileName ?? null, loadedProfiles);
    const configurations = loadedSettings.display?.pcGamingProfileConfigurations ?? [];
    const savedBehaviours = loadedSettings.display?.pcGamingProfileBehaviours ?? [];
    const defaultBehaviour = toPcGamingBehaviourState(loadedSettings.pcGaming);
    const defaultLauncher = toLauncherType(loadedSettings.launcher?.type || 'Playnite');
    const nextProfileBehaviours = buildProfileBehaviourMap(loadedProfiles, configurations, savedBehaviours, defaultBehaviour);
    const nextProfileLaunchers = buildProfileLauncherMap(loadedProfiles, configurations, defaultLauncher);
    const nextProfileHotkeys = buildProfileHotkeysMap(loadedProfiles, configurations, { enterProfile: normalizeHotkeyForDisplay(loadedSettings.hotkeys?.pcGamingMode ?? '') });
    const nextProfileControllerShortcuts = buildProfileControllerShortcutMap(loadedProfiles, configurations, loadedSettings.controller?.pcGamingMode ?? null);
    const selectedProfileName = activeProfile?.name ?? loadedProfiles[0]?.name ?? '';
    options.setSelectedConsoleDisplays(consoleDisplays); options.setSelectedConsoleDisplay(consoleDisplays[0] ?? null); options.setProfiles(loadedProfiles); options.setActiveProfileName(activeProfile?.name ?? null); options.setProfileBehaviours(nextProfileBehaviours); options.setProfileLaunchers(nextProfileLaunchers); options.setProfileHotkeys(nextProfileHotkeys); options.setProfileControllerShortcuts(nextProfileControllerShortcuts); options.setWorkModeControllerShortcut(normalizeControllerShortcut(loadedSettings.controller?.workMode ?? null)); options.setSelectedProfileName(selectedProfileName); options.setLayout(activeProfile?.layout ?? loadedSettings.display?.pcGamingLayout ?? null);
    options.setHotkeys({ consoleMode: normalizeHotkeyForDisplay(loadedSettings.hotkeys?.consoleMode || ''), pcGamingMode: normalizeHotkeyForDisplay(loadedSettings.hotkeys?.pcGamingMode || ''), workMode: normalizeHotkeyForDisplay(loadedSettings.hotkeys?.workMode || '') });
    options.setLauncher(nextProfileLaunchers[selectedProfileName] ?? defaultLauncher);
    const isFirstRun = !(loadedSettings.ui?.isFirstRunCompleted ?? false);
    options.setConsoleBehaviour(isFirstRun ? defaultConsoleBehaviour : { hideDesktopIcons: loadedSettings.gameShell?.hideDesktopIcons ?? true, hideTaskbar: loadedSettings.gameShell?.hideTaskbar ?? true, closeVisibleWindows: loadedSettings.gameShell?.closeVisibleWindows ?? true, minimizeRemainingWindows: loadedSettings.gameShell?.minimizeRemainingWindows ?? true, moveCursorToCorner: loadedSettings.gameShell?.moveCursorToCorner ?? true, hideCursor: loadedSettings.gameShell?.hideCursor ?? true, startLauncher: loadedSettings.gameShell?.startLauncher ?? true, bringLauncherToForeground: loadedSettings.gameShell?.bringLauncherToForeground ?? true });
    options.setPcGamingBehaviour(isFirstRun ? defaultPcGamingBehaviour : nextProfileBehaviours[selectedProfileName] ?? defaultBehaviour);
    options.setWorkBehaviour(isFirstRun ? defaultWorkBehaviour : { closeLauncher: loadedSettings.workMode?.closeLauncher ?? true, showTaskbars: loadedSettings.workMode?.showTaskbars ?? true, showDesktopIcons: loadedSettings.workMode?.showDesktopIcons ?? true, doNotMinimizeWindows: loadedSettings.workMode?.doNotMinimizeWindows ?? true });
    options.setFinishOptions(isFirstRun ? defaultFinishOptions : { startWithWindows: loadedSettings.startup?.startWithWindows ?? true, startMinimized: loadedSettings.startup?.startMinimizedToTray ?? false, startMenuShortcut: loadedSettings.startup?.createStartMenuShortcut ?? true, desktopShortcut: loadedSettings.startup?.createDesktopShortcut ?? true });
    options.setHasUnsavedChanges(false); options.pendingSettingsOverrideRef.current = null;
  };
}
