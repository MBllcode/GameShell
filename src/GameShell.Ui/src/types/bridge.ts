export type BridgeCommand =
  | 'getStatus'
  | 'getSettings'
  | 'saveSettings'
  | 'getDependencies'
  | 'getAudioDevices'
  | 'getDisplays'
  | 'getDisplayModes'
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
  | 'startControllerShortcutCapture'
  | 'cancelControllerShortcutCapture'
  | 'acknowledgeControllerShortcutCapture'
  | 'getControllerStatus'
  | 'enterGameShell'
  | 'enterGameShellWithProfile'
  | 'enterWorkMode';

export type ModeState = 'Unknown' | 'WorkMode' | 'GameShell' | 'SwitchingToConsole' | 'SwitchingToWork' | 'Error';

export interface AppStatus {
  state: ModeState;
  currentMode: string | null;
  sessionState: string | null;
  isBusy: boolean;
  displayMode: string;
  selectedConsoleDisplay: string | null;
  hasPcGamingLayout: boolean;
  activeProfileName: string | null;
  activeLauncher: string | null;
  lastOperation: string | null;
  lastOperationDurationMs: number | null;
  lastError: string | null;
  settingsPath: string;
}

export interface DisplayIdentity {
  monitorDevicePath: string;
  friendlyName: string;
  lastKnownFriendlyName: string;
  edidManufacturerId: number;
  edidProductCodeId: number;
  connectorInstance: number;
  adapterLuid: number;
  targetId: number;
  sourceId: number;
  outputTechnology: string;
}

export interface DisplayDeviceInfo {
  id: string;
  name: string;
  isPrimary: boolean;
  isEnabled: boolean;
  isAvailable: boolean;
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
  currentModeAvailable: boolean;
  preferredWidth: number;
  preferredHeight: number;
}

export interface DisplayModeInfo {
  width: number;
  height: number;
  refreshRateNumerator: number;
  refreshRateDenominator: number;
  refreshRateHz: number;
}

export interface DisplayModeQueryResult {
  success: boolean;
  modes: DisplayModeInfo[];
}

export interface DisplayPathSnapshot {
  identity: DisplayIdentity;
  isActive: boolean;
  isAvailable: boolean;
  isPrimary: boolean;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  orientation: string;
  refreshRateNumerator: number;
  refreshRateDenominator: number;
  refreshRateHz: number;
  currentModeAvailable: boolean;
  preferredWidth: number;
  preferredHeight: number;
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
  displayTopology?: GamingProfileDisplayTopology | null;
}

export interface GamingProfileDisplayTopology {
  displays: ProfileDisplayTarget[];
}

export type RefreshPolicy = 'AutoHighest' | 'Fixed';

export interface ProfileDisplayTarget {
  identity: DisplayIdentity;
  enabled: boolean;
  isPrimary: boolean;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  refreshPolicy?: RefreshPolicy;
  fixedRefreshRateNumerator?: number;
  fixedRefreshRateDenominator?: number;
  /** Legacy exact-refresh fields; ignored for new AutoHighest drafts. */
  refreshRateNumerator: number;
  refreshRateDenominator: number;
  orientation: string;
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
  controllerShortcut: ControllerShortcutSettings | null;
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
    language: string;
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

export interface ControllerStatusInfo {
  sequenceNumber: number;
  isConnected: boolean;
  deviceName: string | null;
  isListening: boolean;
  capturePhase: 'Idle' | 'WaitingForRelease' | 'WaitingForFirstPress' | 'Recording' | 'WaitingForCombinationRelease' | 'Completed' | 'Cancelled' | 'TimedOut';
  captureSessionId: string | null;
  profileId: string | null;
  startedAt: string | null;
  capturedButtons: string[];
  completedCapture: CompletedControllerCapture | null;
  inputSource: string;
  gamepadButtonCount: number;
  joystickButtonCount: number;
  rawPressedButtons: number[];
  backend: string;
  controllerCount: number;
  controllerId: string | null;
  lastReadButtons: string[];
  lastReadAt: string | null;
  diagnosticStatus: string;
}

export interface CompletedControllerCapture {
  sessionId: string;
  profileId: string;
  buttons: string[];
  completedAt: string;
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
