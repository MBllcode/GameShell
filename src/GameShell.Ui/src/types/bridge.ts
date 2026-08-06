export type BridgeCommand =
  | 'getStatus'
  | 'getSettings'
  | 'saveSettings'
  | 'getDependencies'
  | 'getAudioDevices'
  | 'getDisplays'
  | 'captureDisplayLayout'
  | 'saveConsoleDisplay'
  | 'saveConsoleDisplays'
  | 'savePcGamingDisplays'
  | 'saveConsoleAudioDevice'
  | 'capturePcGamingLayout'
  | 'capturePrimaryPcGamingLayout'
  | 'setActivePcGamingProfile'
  | 'clearPcGamingLayout'
  | 'testConsoleDisplay'
  | 'testPcGamingLayout'
  | 'confirmDisplayTest'
  | 'restoreDisplayTest'
  | 'restorePreviousDisplayLayout'
  | 'openDependencyDownloadPage'
  | 'selectDependencyPath'
  | 'setHotkeyCaptureActive'
  | 'enterGameShell'
  | 'enterGameShellWithProfile'
  | 'enterWorkMode';

export type ModeState = 'Unknown' | 'WorkMode' | 'GameShell' | 'SwitchingToConsole' | 'SwitchingToWork' | 'Error';

export interface AppStatus {
  state: ModeState;
  currentMode: string | null;
  isBusy: boolean;
  displayMode: string;
  selectedConsoleDisplay: string | null;
  hasPcGamingLayout: boolean;
  activeLauncher: string;
  lastOperation: string | null;
  lastOperationDurationMs: number | null;
  lastError: string | null;
  settingsPath: string;
}

export interface DisplayIdentity {
  monitorDevicePath: string;
  friendlyName: string;
  edidManufacturerId: number;
  edidProductCodeId: number;
  connectorInstance: number;
  adapterLuid: number;
  targetId: number;
}

export interface DisplayDeviceInfo {
  id: string;
  name: string;
  isPrimary: boolean;
  isEnabled: boolean;
  identity: DisplayIdentity;
  sourceDeviceName: string;
  sourceId: number;
  targetId: number;
  adapterLuid: number;
  connectionTechnology: string;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  orientation: string;
  refreshRateNumerator: number;
  refreshRateDenominator: number;
  refreshRateHz: number;
}

export interface DisplayPathSnapshot {
  identity: DisplayIdentity;
  isActive: boolean;
  isPrimary: boolean;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  orientation: string;
  refreshRateNumerator: number;
  refreshRateDenominator: number;
  refreshRateHz: number;
  sourceId: number;
  targetId: number;
  adapterLuid: number;
  sourceDeviceName: string;
  connectionTechnology: string;
}

export interface DisplayLayoutSnapshot {
  id: string;
  capturedAt: string;
  displays: DisplayPathSnapshot[];
}

export interface DisplayLayoutProfile {
  name: string;
  layout: DisplayLayoutSnapshot;
}

export interface PcGamingProfileBehaviour {
  profileName: string;
  behaviour: Partial<AppSettings['pcGaming']>;
}

export interface PcGamingProfileHotkeys {
  enterProfile: string;
  enterWorkMode: string;
}

export interface PcGamingProfileConfiguration {
  profileName: string;
  behaviour: Partial<AppSettings['pcGaming']>;
  launcher: AppSettings['launcher'];
  hotkeys: PcGamingProfileHotkeys;
}

export interface DependencyStatus {
  name: string;
  isRequired: boolean;
  isDetected: boolean;
  detectedPath: string | null;
  version: string | null;
  message: string;
  downloadUrl: string | null;
}

export interface AudioDeviceSnapshot {
  id: string;
  name: string;
  state: string;
  isActive: boolean;
}

export interface ModeResult {
  status: string;
  currentState: ModeState;
  message: string;
  durationMs: number;
  success: boolean;
}

export interface DisplayTransitionResult {
  success: boolean;
  rolledBack: boolean;
  errorCode: string | null;
  userMessage: string | null;
}

export interface AppSettings {
  display: {
    consoleDisplay: DisplayIdentity | null;
    consoleDisplays?: DisplayIdentity[];
    pcGamingLayout: DisplayLayoutSnapshot | null;
    pcGamingProfiles?: DisplayLayoutProfile[];
    activePcGamingProfileName?: string | null;
    pcGamingProfileConfigurations?: PcGamingProfileConfiguration[];
    pcGamingProfileBehaviours?: PcGamingProfileBehaviour[];
  };
  launcher: {
    type: string;
    executablePath: string;
    arguments: string;
    targetDisplayId: string | null;
  };
  gameShell: {
    hideDesktopIcons: boolean;
    hideTaskbar: boolean;
    closeVisibleWindows: boolean;
    minimizeRemainingWindows: boolean;
    moveCursorToCorner: boolean;
    hideCursor: boolean;
    startLauncher: boolean;
    bringLauncherToForeground: boolean;
    protectedProcessNames?: string[];
    closeOnlyProcessNames?: string[];
    completionDelaySeconds: number;
  };
  pcGaming: {
    hideDesktopIcons: boolean;
    hideTaskbar: boolean;
    closeVisibleWindows: boolean;
    minimizeRemainingWindows: boolean;
    moveCursorToCorner: boolean;
    hideCursor: boolean;
    startLauncher: boolean;
    bringLauncherToForeground: boolean;
    protectedProcessNames?: string[];
    closeOnlyProcessNames?: string[];
    completionDelaySeconds: number;
  };
  workMode: {
    closeLauncher: boolean;
    showTaskbars: boolean;
    showDesktopIcons: boolean;
    taskbarRestoreDelaySeconds: number;
    desktopIconsDelaySeconds: number;
    doNotMinimizeWindows: boolean;
    completionDelaySeconds: number;
  };
  audio: {
    switchAudioInConsoleMode: boolean;
    restoreAudioInWorkMode: boolean;
    consoleAudioDeviceId: string | null;
    consoleAudioDeviceName: string | null;
  };
  controller: ControllerSettings;
  hotkeys: {
    consoleMode: string;
    pcGamingMode: string;
    workMode: string;
  };
  startup: {
    startWithWindows: boolean;
    startMinimizedToTray: boolean;
    createStartMenuShortcut: boolean;
    createDesktopShortcut: boolean;
  };
  ui: {
    closeToTray: boolean;
    isFirstRunCompleted: boolean;
    windowLeft?: number | null;
    windowTop?: number | null;
    windowWidth?: number | null;
    windowHeight?: number | null;
  };
}

export interface ControllerShortcutSettings {
  buttons: string[];
  holdDurationMs: number;
}

export interface ControllerSettings {
  enabled: boolean;
  consoleMode: ControllerShortcutSettings | null;
  pcGamingMode: ControllerShortcutSettings | null;
  workMode: ControllerShortcutSettings | null;
}

export interface BridgeRequest {
  type: BridgeCommand;
  requestId: string;
  payload?: unknown;
}

export interface BridgeResponse<T> {
  requestId: string;
  success: boolean;
  data?: T;
  error?: string;
}
