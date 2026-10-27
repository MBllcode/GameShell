export type BridgeCommand =
  | 'getStatus'
  | 'getSettings'
  | 'saveSettings'
  | 'getDependencies'
  | 'getDisplays'
  | 'getDisplayModes'
  | 'captureDisplayLayout'
  | 'saveConsoleDisplay'
  | 'saveConsoleDisplays'
  | 'saveGamingDisplays'
  | 'captureGamingLayout'
  | 'capturePrimaryGamingLayout'
  | 'setActiveGamingProfile'
  | 'clearGamingLayout'
  | 'testConsoleDisplay'
  | 'testGamingLayout'
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

export type TransientOperationKind = 'None' | 'Startup' | 'EnterProfile' | 'ExitToWorkMode' | 'DisplayDiscovery' | 'DisplayTransition' | 'LauncherStartup';
export type TransientOperationPhase = 'None' | 'PreparingDisplays' | 'ApplyingDisplayTopology' | 'SynchronizingDisplays' | 'StartingLauncher' | 'ActivatingLauncher' | 'RestoringDisplays' | 'RestoringDesktop' | 'Completed' | 'Failed';

export interface TransientOperationState {
  kind: TransientOperationKind;
  phase: TransientOperationPhase;
  isActive: boolean;
  messageKey: string | null;
  startedAt: string | null;
  transitionId: string | null;
}

export interface AppStatus {
  state: ModeState;
  currentMode: string | null;
  sessionState: string | null;
  isBusy: boolean;
  displayMode: string;
  selectedConsoleDisplay: string | null;
  hasGamingProfiles: boolean;
  activeProfileName: string | null;
  activeLauncher: string | null;
  lastOperation: string | null;
  lastOperationDurationMs: number | null;
  lastError: string | null;
  transientOperation: TransientOperationState;
  settingsPath: string;
}

export interface DisplayIdentity {
  monitorDevicePath: string;
  friendlyName: string;
  edidIdsValid?: boolean;
  edidManufacturerId: number;
  edidProductCodeId: number;
  connectorInstance: number;
  adapterLuid: number;
  targetId: number;
  sourceId: number;
  outputTechnology: string;
}

export interface DisplayCapabilitySnapshot {
  width: number;
  height: number;
  maxRefreshRateNumerator: number;
  maxRefreshRateDenominator: number;
  maxRefreshRateHz: number;
}

export type DisplayCatalogState = 'Current' | 'Degraded' | 'InactiveObservation' | 'Unavailable' | 'IdentityConflict';
export type DisplayCapabilityState = 'UnknownUntilActive' | 'Available';
export type DisplayDiscoveryState = 'Idle' | 'Loading' | 'Settling' | 'Ready' | 'Error';

export interface CurrentDisplayObservation {
  identity: DisplayIdentity;
  currentObservedName: string;
  currentSourceDeviceName: string;
  currentConnectionTechnology: string;
  /** Compatibility with payloads produced by older desktop builds. */
  currentFriendlyName?: string;
  currentCapability: DisplayCapabilitySnapshot | null;
  enumeratedModes: DisplayModeInfo[];
  capabilityState: DisplayCapabilityState;
  isActive: boolean;
  isAvailable: boolean;
  state: DisplayCatalogState;
}

export interface KnownPhysicalDisplay {
  id?: string;
  physicalIdentity: DisplayIdentity;
  canonicalFriendlyName: string;
  optionalUserLabel?: string;
  bestKnownCapability: DisplayCapabilitySnapshot | null;
  lastVerifiedActiveCapability?: DisplayCapabilitySnapshot | null;
  capabilityVerificationSource?: string;
  /** Legacy settings payload accepted during migration. */
  lastKnownGoodCapability?: DisplayCapabilitySnapshot | null;
}

export interface DisplayDeviceInfo {
  id: string;
  knownPhysicalDisplayId?: string;
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
  isRemembered: boolean;
  catalogState: DisplayCatalogState;
  currentObservation: CurrentDisplayObservation | null;
  enumeratedModes: DisplayModeInfo[];
  lastKnownGoodCapability: DisplayCapabilitySnapshot | null;
  lastVerifiedActiveCapability?: DisplayCapabilitySnapshot | null;
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
  knownPhysicalDisplayId?: string;
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
  id?: string | null;
  name: string;
  layout: DisplayLayoutSnapshot;
  displayTopology?: GamingProfileDisplayTopology | null;
}

export interface GamingProfileDisplayTopology {
  displays: ProfileDisplayTarget[];
}

export type RefreshPolicy = 'AutoHighest' | 'Fixed';

export interface ProfileDisplayTarget {
  knownPhysicalDisplayId?: string;
  identity: DisplayIdentity;
  capabilityExpectation?: DisplayCapabilityExpectation | null;
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

export interface DisplayCapabilityExpectation {
  width: number;
  height: number;
  lastKnownGoodMaxRefreshNumerator: number;
  lastKnownGoodMaxRefreshDenominator: number;
}

export interface GamingProfileBehaviour {
  profileName: string;
  behaviour: Partial<AppSettings['gaming']>;
}

export interface GamingProfileHotkeys {
  enterProfile: string;
  enterWorkMode: string;
}

export interface GamingProfileConfiguration {
  profileName: string;
  behaviour: Partial<AppSettings['gaming']>;
  launcher: AppSettings['launcher'];
  hotkeys: GamingProfileHotkeys;
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
    gamingLayout: DisplayLayoutSnapshot | null;
    gamingProfiles?: DisplayLayoutProfile[];
    activeGamingProfileName?: string | null;
    gamingProfileConfigurations?: GamingProfileConfiguration[];
    gamingProfileBehaviours?: GamingProfileBehaviour[];
    knownPhysicalDisplays?: KnownPhysicalDisplay[];
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
  gaming: {
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
    defaultDisplayLayout: DisplayLayoutSnapshot | null;
    closeLauncher: boolean;
    showTaskbars: boolean;
    showDesktopIcons: boolean;
    taskbarRestoreDelaySeconds: number;
    desktopIconsDelaySeconds: number;
    doNotMinimizeWindows: boolean;
    completionDelaySeconds: number;
  };
  controller: ControllerSettings;
  hotkeys: {
    consoleMode: string;
    gamingMode: string;
    workMode: string;
  };
  startup: {
    startWithWindows: boolean;
    startMinimizedToTray: boolean;
    createStartMenuShortcut: boolean;
    createDesktopShortcut: boolean;
    startupBehavior: 'AlwaysWorkMode' | 'RestoreLastActiveMode' | 'AlwaysProfile';
    startupProfileId: string | null;
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
  gamingMode: ControllerShortcutSettings | null;
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
