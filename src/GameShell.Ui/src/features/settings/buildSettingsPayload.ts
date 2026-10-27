import { profileLayout, type Profile } from '../profiles/profileModel.ts';
import { normalizeLanguageCode } from '../../i18n/language.ts';
import { normalizeControllerShortcut } from '../shortcuts/controllers.ts';
import { normalizeHotkeyForDisplay } from '../shortcuts/hotkeys.ts';
import { getLauncherSettings, type LauncherType } from '../profiles/profileCommands.ts';
import { resolveGamingBehaviourProfileName } from '../profiles/utils.ts';
import type { AppSettings, DisplayIdentity, DisplayLayoutSnapshot } from '../../bridge/contracts';

type GamingBehaviourState = Pick<AppSettings['gaming'], 'hideDesktopIcons' | 'hideTaskbar' | 'closeVisibleWindows' | 'minimizeRemainingWindows' | 'moveCursorToCorner' | 'hideCursor' | 'startLauncher' | 'bringLauncherToForeground'>;
type ConsoleBehaviourState = Pick<AppSettings['gameShell'], 'hideDesktopIcons' | 'hideTaskbar' | 'closeVisibleWindows' | 'minimizeRemainingWindows' | 'moveCursorToCorner' | 'hideCursor' | 'startLauncher' | 'bringLauncherToForeground'>;
type WorkBehaviourState = Pick<AppSettings['workMode'], 'closeLauncher' | 'showTaskbars' | 'showDesktopIcons' | 'doNotMinimizeWindows'>;
type HotkeySettingsState = { consoleMode: string; gamingMode: string; workMode: string };
type FinishOptions = { startWithWindows: boolean; startMinimized: boolean; startMenuShortcut: boolean; desktopShortcut: boolean; startupBehavior: 'AlwaysWorkMode' | 'RestoreLastActiveMode' | 'AlwaysProfile'; startupProfileId: string | null };

export type SettingsPayloadOverrides = {
  gamingProfiles?: Profile[];
  activeGamingProfileName?: string | null;
  gamingLayout?: DisplayLayoutSnapshot | null;
};

export type BuildSettingsPayloadState = {
  settings: AppSettings | null;
  selectedConsoleDisplay: DisplayIdentity | null;
  selectedConsoleDisplays: DisplayIdentity[];
  gamingLayout: DisplayLayoutSnapshot | null;
  gamingProfiles: Profile[];
  activeGamingProfileName: string | null;
  workModeControllerShortcut: AppSettings['controller']['workMode'];
  workModeLayout: DisplayLayoutSnapshot | null;
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
  // A layout commit may still be pending while later wizard steps edit application/shortcuts.
  // Apply its membership/layout changes without restoring stale copies of those settings.
  const effectiveGamingProfiles = overrides?.gamingProfiles?.map(profile => {
    const current = state.gamingProfiles.find(candidate =>
      profile.id ? candidate.id === profile.id : candidate.name.toLowerCase() === profile.name.toLowerCase());
    return current ? { ...profile, application: current.application, behaviour: current.behaviour,
      hotkeys: current.hotkeys, controllerShortcut: current.controllerShortcut } : profile;
  }) ?? state.gamingProfiles;
  const effectiveActiveGamingProfileName = overrides?.activeGamingProfileName ?? state.activeGamingProfileName;
  const effectiveGamingLayout = overrides?.gamingLayout ?? state.gamingLayout;
  const activeProfileName = resolveGamingBehaviourProfileName(effectiveActiveGamingProfileName, effectiveGamingProfiles);
  const activeProfile = effectiveGamingProfiles.find(profile => profile.name === activeProfileName);
  const savedGamingBehaviour = activeProfile?.behaviour ?? state.gamingBehaviour;
  const savedLauncher = activeProfile?.application ?? state.launcher;
  const activeProfileControllerShortcut = activeProfile?.controllerShortcut;
  const launcherSettings = getLauncherSettings(savedLauncher);
  const savedHotkeys = activeProfile?.hotkeys ?? { enterProfile: state.hotkeys.gamingMode };

  return {
    ...state.settings,
    display: {
      ...state.settings?.display,
      consoleDisplay: state.selectedConsoleDisplay,
      consoleDisplays: state.selectedConsoleDisplays,
      gamingLayout: effectiveGamingLayout,
      gamingProfiles: effectiveGamingProfiles.map(profileLayout),
      activeGamingProfileName: effectiveActiveGamingProfileName,
      gamingProfileConfigurations: effectiveGamingProfiles.map(profile => {
        const savedName = state.settings?.display.gamingProfiles?.find(saved => profile.id
          ? saved.id === profile.id : saved.name === profile.name)?.name ?? profile.name;
        const previous = state.settings?.display.gamingProfileConfigurations?.find(saved => saved.profileName === savedName);
        const application = getLauncherSettings(profile.application);
        return {
          ...previous,
          profileName: profile.name,
          behaviour: { ...previous?.behaviour, ...profile.behaviour,
            startLauncher: profile.application !== 'None',
            bringLauncherToForeground: profile.application !== 'None' && profile.behaviour.bringLauncherToForeground },
          launcher: { ...(previous?.launcher.type === application.type ? previous.launcher : application),
            targetDisplayId: state.selectedConsoleDisplay?.monitorDevicePath ?? null },
          hotkeys: { ...previous?.hotkeys, enterProfile: normalizeHotkeyForDisplay(profile.hotkeys.enterProfile), enterWorkMode: previous?.hotkeys?.enterWorkMode ?? '' },
          controllerShortcut: normalizeControllerShortcut(profile.controllerShortcut)
        };
      }),
      gamingProfileBehaviours: effectiveGamingProfiles.map(profile => ({ profileName: profile.name, behaviour: profile.behaviour })),
      knownPhysicalDisplays: state.settings?.display?.knownPhysicalDisplays ?? []
    },
    launcher: { type: launcherSettings.type, executablePath: launcherSettings.executablePath, arguments: launcherSettings.arguments, targetDisplayId: state.selectedConsoleDisplay?.monitorDevicePath ?? null },
    gameShell: { ...state.settings?.gameShell, ...state.consoleBehaviour, completionDelaySeconds: state.settings?.gameShell.completionDelaySeconds ?? 5 },
    gaming: { ...state.settings?.gaming, ...savedGamingBehaviour, startLauncher: savedLauncher !== 'None', bringLauncherToForeground: savedLauncher !== 'None' && savedGamingBehaviour.bringLauncherToForeground, protectedProcessNames: state.settings?.gaming?.protectedProcessNames ?? [], closeOnlyProcessNames: state.settings?.gaming?.closeOnlyProcessNames ?? [], completionDelaySeconds: state.settings?.gaming?.completionDelaySeconds ?? 5 },
    workMode: { ...state.settings?.workMode, ...state.workBehaviour, defaultDisplayLayout: state.workModeLayout, taskbarRestoreDelaySeconds: state.settings?.workMode.taskbarRestoreDelaySeconds ?? 3, desktopIconsDelaySeconds: state.settings?.workMode.desktopIconsDelaySeconds ?? 15, completionDelaySeconds: state.settings?.workMode.completionDelaySeconds ?? 20 },
    controller: { ...state.settings?.controller, enabled: effectiveGamingProfiles.some(profile => (profile.controllerShortcut?.buttons.length ?? 0) > 0) || (state.workModeControllerShortcut?.buttons.length ?? 0) > 0 || state.settings?.controller?.enabled || false, consoleMode: state.settings?.controller?.consoleMode ?? null, gamingMode: activeProfileName ? normalizeControllerShortcut(activeProfileControllerShortcut) : normalizeControllerShortcut(state.settings?.controller?.gamingMode ?? null), workMode: normalizeControllerShortcut(state.workModeControllerShortcut) },
    hotkeys: { ...state.settings?.hotkeys, consoleMode: normalizeHotkeyForDisplay(state.hotkeys.consoleMode), gamingMode: normalizeHotkeyForDisplay(savedHotkeys.enterProfile || state.hotkeys.gamingMode), workMode: normalizeHotkeyForDisplay(state.hotkeys.workMode) },
    startup: { ...state.settings?.startup, startWithWindows: state.finishOptions.startWithWindows, startMinimizedToTray: state.finishOptions.startMinimized, createStartMenuShortcut: state.finishOptions.startMenuShortcut, createDesktopShortcut: state.finishOptions.desktopShortcut, startupBehavior: state.finishOptions.startupBehavior, startupProfileId: state.finishOptions.startupBehavior === 'AlwaysProfile' ? state.finishOptions.startupProfileId : null },
    ui: { ...state.settings?.ui, closeToTray: true, isFirstRunCompleted, language: normalizeLanguageCode(state.language) }
  };
}
