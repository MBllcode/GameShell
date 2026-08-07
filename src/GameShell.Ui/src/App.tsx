import { useCallback, useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { sendBridgeCommand, subscribeBridgeEvent } from './services/webViewBridge';
import { ControllerFamily, detectControllerFamily, getControllerButtonDisplayName, mapControllerButtons } from './controllerDisplayMapper';
import type {
  AppSettings,
  AppStatus,
  ControllerShortcutSettings,
  ControllerStatusInfo,
  DisplayDeviceInfo,
  DisplayIdentity,
  DisplayLayoutProfile,
  DisplayLayoutSnapshot,
  ModeResult
} from './types/bridge';

type StepId =
  | 'welcome'
  | 'gaming-display'
  | 'launcher'
  | 'console-behaviour'
  | 'work-behaviour'
  | 'work-shortcuts'
  | 'controller'
  | 'app-behaviour'
  | 'finish';

type LauncherType = 'Playnite Fullscreen' | 'Steam Big Picture' | 'Xbox App';
type ModeCommand = 'enterGameShell' | 'enterGameShellWithProfile' | 'enterWorkMode';
type HotkeySettingsState = {
  consoleMode: string;
  pcGamingMode: string;
  workMode: string;
};

type ProfileHotkeysState = {
  enterProfile: string;
};

type PcGamingProfileControllerShortcutMap = Record<string, ControllerShortcutSettings | null>;

type SetupStep = {
  id: StepId;
  title: string;
  subtitle: string;
};

type PendingNavigationAction = 'goBack' | 'goNext' | 'returnToDashboard';

type DisplayProfileDraft = {
  profileName: string;
  identities: DisplayIdentity[];
};

type DisplayValidationState = {
  showNameError: boolean;
  showSelectionError: boolean;
};

type OverwriteProfileAction = 'next' | 'save' | 'save-then-continue';

type SettingsPayloadOverrides = {
  pcGamingProfiles?: DisplayLayoutProfile[];
  activePcGamingProfileName?: string | null;
  pcGamingLayout?: DisplayLayoutSnapshot | null;
  pcGamingProfileBehaviours?: PcGamingProfileBehaviourMap;
  pcGamingProfileLaunchers?: PcGamingProfileLauncherMap;
  pcGamingProfileHotkeys?: PcGamingProfileHotkeysMap;
  pcGamingProfileControllerShortcuts?: PcGamingProfileControllerShortcutMap;
};

const MAX_PROFILE_DISPLAYS = 2;
const PROFILE_DISPLAY_LIMIT_MESSAGE = 'Profil może obecnie zawierać maksymalnie 2 ekrany.';

const initialStatus: AppStatus = {
  state: 'Unknown',
  currentMode: null,
  isBusy: false,
  displayMode: 'NativeDisplayConfig',
  selectedConsoleDisplay: null,
  hasPcGamingLayout: false,
  activeLauncher: 'Playnite',
  lastOperation: null,
  lastOperationDurationMs: null,
  lastError: null,
  settingsPath: ''
};

const steps: SetupStep[] = [
  { id: 'welcome', title: 'Start', subtitle: 'Co skonfigurujemy' },
  { id: 'gaming-display', title: 'Ekrany', subtitle: 'Konfiguracja profili ekranów' },
  { id: 'launcher', title: 'Launcher profilu', subtitle: 'Launcher dla każdego profilu' },
  { id: 'console-behaviour', title: 'Zachowanie profilu', subtitle: 'Pulpit i okna dla profilu' },
  { id: 'controller', title: 'Skróty profilu', subtitle: 'Skróty klawiaturowe profilu' },
  { id: 'work-behaviour', title: 'Zachowanie Work Mode', subtitle: 'Przywracanie układu roboczego' },
  { id: 'work-shortcuts', title: 'Skróty Work Mode', subtitle: 'Skróty klawiaturowe i kontrolera' },
  { id: 'app-behaviour', title: 'Zachowanie aplikacji', subtitle: 'Ustawienia globalne' },
  { id: 'finish', title: 'Zakończenie', subtitle: 'Podsumowanie profilu' }
];

const profileSettingsStepIds: StepId[] = [
  'gaming-display',
  'launcher',
  'console-behaviour',
  'controller'
];

const globalSettingsStepIds: StepId[] = [
  'work-behaviour',
  'work-shortcuts',
  'app-behaviour'
];

function isGlobalSettingsStep(stepId: StepId) {
  return globalSettingsStepIds.includes(stepId);
}

const defaultConsoleBehaviour = {
  hideDesktopIcons: true,
  hideTaskbar: true,
  closeVisibleWindows: true,
  minimizeRemainingWindows: true,
  moveCursorToCorner: true,
  hideCursor: true,
  startLauncher: true,
  bringLauncherToForeground: true
};

const defaultPcGamingBehaviour = {
  hideDesktopIcons: true,
  hideTaskbar: true,
  closeVisibleWindows: false,
  minimizeRemainingWindows: false,
  moveCursorToCorner: true,
  hideCursor: false,
  startLauncher: true,
  bringLauncherToForeground: true
};

type PcGamingBehaviourState = typeof defaultPcGamingBehaviour;
type PcGamingBehaviourKey = keyof PcGamingBehaviourState;
type PcGamingProfileBehaviourMap = Record<string, PcGamingBehaviourState>;
type PcGamingProfileLauncherMap = Record<string, LauncherType>;
type PcGamingProfileHotkeysMap = Record<string, ProfileHotkeysState>;

const pcGamingBehaviourOptions: Array<{ key: PcGamingBehaviourKey; label: string }> = [
  { key: 'hideDesktopIcons', label: 'Ukryj ikony pulpitu' },
  { key: 'hideTaskbar', label: 'Ukryj pasek zadań' },
  { key: 'closeVisibleWindows', label: 'Zamknij widoczne okna aplikacji' },
  { key: 'minimizeRemainingWindows', label: 'Zminimalizuj pozostałe okna' },
  { key: 'moveCursorToCorner', label: 'Przesuń kursor w róg' },
  { key: 'hideCursor', label: 'Ukryj kursor' },
  { key: 'startLauncher', label: 'Uruchom launcher' },
  { key: 'bringLauncherToForeground', label: 'Przenieś launcher na pierwszy plan' }
];

const defaultWorkBehaviour = {
  closeLauncher: true,
  showTaskbars: true,
  showDesktopIcons: true,
  doNotMinimizeWindows: true
};

const launcherOptions: Array<{ value: LauncherType; title: string; description: string; badge: string }> = [
  {
    value: 'Playnite Fullscreen',
    title: 'Playnite Fullscreen',
    description: 'Polecany wybór dla wielu launcherów i wygodnej obsługi padem.',
    badge: 'Polecany'
  },
  {
    value: 'Steam Big Picture',
    title: 'Steam Big Picture',
    description: 'Uruchamia Steam od razu w trybie Big Picture.',
    badge: 'Steam'
  },
  {
    value: 'Xbox App',
    title: 'Xbox App',
    description: 'Szybkie przejście do aplikacji Xbox i biblioteki Game Pass.',
    badge: 'Xbox'
  }
];

const defaultProfileHotkeys: ProfileHotkeysState = {
  enterProfile: ''
};

const defaultControllerShortcutHoldDurationMs = 700;
const workModeControllerCaptureId = 'WorkMode';

export function App() {
  const [status, setStatus] = useState<AppStatus>(initialStatus);
  const [settings, setSettings] = useState<AppSettings | null>(null);
  const [stepIndex, setStepIndex] = useState(0);
  const [message, setMessage] = useState('Gotowe.');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [isRemovingProfile, setIsRemovingProfile] = useState(false);
  const [activeModeAction, setActiveModeAction] = useState<string | null>(null);
  const [isInitialLoading, setIsInitialLoading] = useState(true);
  const [isVeryFirstConfiguration, setIsVeryFirstConfiguration] = useState(true);
  const [isSetupComplete, setIsSetupComplete] = useState(false);
  const [isEditingSection, setIsEditingSection] = useState(false);
  const [isCreatingProfileFlow, setIsCreatingProfileFlow] = useState(false);
  const [hasAdvancedInCreateProfileFlow, setHasAdvancedInCreateProfileFlow] = useState(false);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const [pendingNavigationAction, setPendingNavigationAction] = useState<PendingNavigationAction | null>(null);
  const [showUnsavedDialog, setShowUnsavedDialog] = useState(false);
  const [showOverwriteProfileDialog, setShowOverwriteProfileDialog] = useState(false);
  const [pendingOverwriteProfileAction, setPendingOverwriteProfileAction] = useState<OverwriteProfileAction | null>(null);
  const [displayProfileDraft, setDisplayProfileDraft] = useState<DisplayProfileDraft>({ profileName: '', identities: [] });
  const [displayValidationState, setDisplayValidationState] = useState<DisplayValidationState>({
    showNameError: false,
    showSelectionError: false
  });
  const displayProfileDraftRef = useRef<DisplayProfileDraft>({ profileName: '', identities: [] });
  const pendingSettingsOverrideRef = useRef<SettingsPayloadOverrides | null>(null);
  const [displays, setDisplays] = useState<DisplayDeviceInfo[]>([]);
  const [isRefreshingDisplays, setIsRefreshingDisplays] = useState(false);
  const [displayListError, setDisplayListError] = useState<string | null>(null);
  const [selectedConsoleDisplay, setSelectedConsoleDisplay] = useState<DisplayIdentity | null>(null);
  const [selectedConsoleDisplays, setSelectedConsoleDisplays] = useState<DisplayIdentity[]>([]);
  const [pcGamingLayout, setPcGamingLayout] = useState<DisplayLayoutSnapshot | null>(null);
  const [pcGamingProfiles, setPcGamingProfiles] = useState<DisplayLayoutProfile[]>([]);
  const [activePcGamingProfileName, setActivePcGamingProfileName] = useState<string | null>(null);
  const [pcGamingProfileBehaviours, setPcGamingProfileBehaviours] = useState<PcGamingProfileBehaviourMap>({});
  const [pcGamingProfileLaunchers, setPcGamingProfileLaunchers] = useState<PcGamingProfileLauncherMap>({});
  const [pcGamingProfileHotkeys, setPcGamingProfileHotkeys] = useState<PcGamingProfileHotkeysMap>({});
  const [pcGamingProfileControllerShortcuts, setPcGamingProfileControllerShortcuts] = useState<PcGamingProfileControllerShortcutMap>({});
  const [workModeControllerShortcut, setWorkModeControllerShortcut] = useState<ControllerShortcutSettings | null>(null);
  const [controllerShortcutStatus, setControllerShortcutStatus] = useState<ControllerStatusInfo>({
    isConnected: false,
    deviceName: null,
    isListening: false,
    capturePhase: 'Idle',
    captureSessionId: null,
    profileId: null,
    startedAt: null,
    capturedButtons: [],
    completedCapture: null,
    inputSource: 'None',
    gamepadButtonCount: 0,
    joystickButtonCount: 0,
    rawPressedButtons: [],
    backend: 'SDL3',
    controllerCount: 0,
    controllerId: null,
    lastReadButtons: [],
    lastReadAt: null,
    diagnosticStatus: 'Brak zgodnego kontrolera'
  });
  const controllerSessionRef = useRef<string | null>(null);
  const controllerCaptureActiveRef = useRef(false);
  const acknowledgedCaptureRef = useRef<string | null>(null);
  const controllerCaptureCompletionTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [controllerCaptureSuccessButtons, setControllerCaptureSuccessButtons] = useState<string[] | null>(null);
  const [controllerNotice, setControllerNotice] = useState<'missing' | 'disconnected' | null>(null);
  const [profileHotkeyValidationError, setProfileHotkeyValidationError] = useState<string | null>(null);
  const [profileControllerValidationError, setProfileControllerValidationError] = useState<string | null>(null);
  const [selectedPcGamingBehaviourProfileName, setSelectedPcGamingBehaviourProfileName] = useState('');
  const [hotkeys, setHotkeys] = useState<HotkeySettingsState>({ consoleMode: '', pcGamingMode: '', workMode: '' });
  const [launcher, setLauncher] = useState<LauncherType>('Playnite Fullscreen');
  const [consoleBehaviour, setConsoleBehaviour] = useState({ ...defaultConsoleBehaviour });
  const [pcGamingBehaviour, setPcGamingBehaviour] = useState<PcGamingBehaviourState>({ ...defaultPcGamingBehaviour });
  const [workBehaviour, setWorkBehaviour] = useState({ ...defaultWorkBehaviour });
  const [finishOptions, setFinishOptions] = useState({
    startWithWindows: false,
    startMinimized: false,
    startMenuShortcut: true,
    desktopShortcut: false
  });

  const syncDisplayProfileDraft = useCallback((nextDraft: DisplayProfileDraft) => {
    displayProfileDraftRef.current = nextDraft;
    setDisplayProfileDraft(nextDraft);
  }, []);

  const handleDisplayDraftChange = useCallback((profileName: string, identities: DisplayIdentity[]) => {
    syncDisplayProfileDraft({ profileName, identities });
  }, [syncDisplayProfileDraft]);

  const handleDisplayValidationStateChange = useCallback((state: DisplayValidationState) => {
    setDisplayValidationState(state);
  }, []);

  const applyControllerStatus = (status: ControllerStatusInfo) => {
    const normalizedStatus = normalizeControllerStatus(status);
    const wasCapturing = controllerCaptureActiveRef.current;
    controllerCaptureActiveRef.current = isControllerCapturePhase(normalizedStatus.capturePhase);

    if (normalizedStatus.controllerCount > 0) {
      setControllerNotice(null);
    } else if (wasCapturing) {
      setControllerNotice('disconnected');
    }

    controllerSessionRef.current = normalizedStatus.captureSessionId;
    setControllerShortcutStatus(normalizedStatus);
    return normalizedStatus;
  };

  const startControllerCapture = async (profileId: string) => {
    setControllerCaptureSuccessButtons(null);

    try {
      const currentStatus = applyControllerStatus(await sendBridgeCommand<ControllerStatusInfo>('getControllerStatus'));
      if (currentStatus.controllerCount === 0) {
        setControllerNotice('missing');
        return;
      }

      setControllerNotice(null);
      await sendBridgeCommand('startControllerShortcutCapture', { profileId });
      applyControllerStatus(await sendBridgeCommand<ControllerStatusInfo>('getControllerStatus'));
    } catch {
      setError('Nie udało się rozpocząć nasłuchiwania kontrolera.');
    }
  };

  useEffect(() => {
    setProfileHotkeyValidationError(null);
    setProfileControllerValidationError(null);
  }, [selectedPcGamingBehaviourProfileName]);

  useEffect(() => {
    let isMounted = true;
    Promise.all([
      sendBridgeCommand<AppStatus>('getStatus'),
      sendBridgeCommand<AppSettings>('getSettings'),
      sendBridgeCommand<DisplayDeviceInfo[]>('getDisplays').catch((reason: unknown) => {
        if (isMounted) {
          setDisplayListError(reason instanceof Error ? reason.message : 'Nie udało się odczytać konfiguracji ekranów.');
        }

        return [];
      }),
      sendBridgeCommand<ControllerStatusInfo>('getControllerStatus').catch((): ControllerStatusInfo => ({
        isConnected: false,
        deviceName: null,
        isListening: false,
        capturePhase: 'Idle',
        captureSessionId: null,
        profileId: null,
        startedAt: null,
        capturedButtons: [],
        completedCapture: null,
        inputSource: 'None',
        gamepadButtonCount: 0,
        joystickButtonCount: 0,
        rawPressedButtons: [],
        backend: 'SDL3',
        controllerCount: 0,
        controllerId: null,
        lastReadButtons: [],
        lastReadAt: null,
        diagnosticStatus: 'Brak zgodnego kontrolera'
      }))
    ])
      .then(([nextStatus, loadedSettings, detectedDisplays, nextControllerStatus]) => {
        if (!isMounted) {
          return;
        }

        setStatus(nextStatus);
        setSettings(loadedSettings);
        setDisplays(detectedDisplays);
        applyControllerStatus(nextControllerStatus);
        applySettingsToState(loadedSettings);
        setIsInitialLoading(false);
      })
      .catch((reason: unknown) => {
        if (isMounted) {
          setError(reason instanceof Error ? reason.message : 'Nie udało się odczytać statusu aplikacji.');
          setIsInitialLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  useEffect(() => {
    return subscribeBridgeEvent('statusChanged', async () => {
      try {
        setStatus(await sendBridgeCommand<AppStatus>('getStatus'));
      } catch (reason: unknown) {
        setError(reason instanceof Error ? reason.message : 'Nie udało się odświeżyć statusu aplikacji.');
      }
    });
  }, []);

  useEffect(() => {
    return subscribeBridgeEvent('controllerStatusChanged', async () => {
      try {
        const nextStatus = await sendBridgeCommand<ControllerStatusInfo>('getControllerStatus');
        const normalizedStatus = applyControllerStatus(nextStatus);
        console.info(`[GameShell] UI capture phase: ${normalizedStatus.capturePhase}`);
      } catch (reason: unknown) {
        setError(reason instanceof Error ? reason.message : 'Nie udało się odświeżyć statusu kontrolera.');
      }
    });
  }, []);

  useEffect(() => {
    const phase = controllerShortcutStatus.capturePhase;
    const isCaptureTracked = phase === 'WaitingForRelease'
      || phase === 'WaitingForFirstPress'
      || phase === 'Recording'
      || phase === 'WaitingForCombinationRelease'
      || phase === 'Completed';
    if (!isCaptureTracked) {
      return;
    }

    let isDisposed = false;
    const refreshControllerStatus = async () => {
      try {
        const nextStatus = normalizeControllerStatus(await sendBridgeCommand<ControllerStatusInfo>('getControllerStatus'));
        if (isDisposed) {
          return;
        }

        const normalizedStatus = applyControllerStatus(nextStatus);
        console.info(`[GameShell] UI capture phase: ${normalizedStatus.capturePhase}`);
      } catch {
        // The push event remains the primary update path; polling is a short-lived recovery path.
      }
    };

    const pollingTimer = setInterval(() => {
      void refreshControllerStatus();
    }, 100);
    return () => {
      isDisposed = true;
      clearInterval(pollingTimer);
    };
  }, [controllerShortcutStatus.capturePhase]);

  useEffect(() => {
    const completedCapture = controllerShortcutStatus.completedCapture;
    if (!completedCapture || completedCapture.sessionId !== controllerShortcutStatus.captureSessionId) {
      return;
    }

    const profileName = completedCapture.profileId;
    const nextButtons = normalizeControllerButtons(completedCapture.buttons);
    if (nextButtons.length === 0) {
      return;
    }

    const currentButtons = profileName === workModeControllerCaptureId
      ? workModeControllerShortcut?.buttons ?? []
      : pcGamingProfileControllerShortcuts[profileName]?.buttons ?? [];
    const isSameSelection = currentButtons.length === nextButtons.length && currentButtons.every((button, index) => button === nextButtons[index]);
    if (!isSameSelection) {
      if (profileName === workModeControllerCaptureId) {
        updateWorkModeControllerShortcut(nextButtons);
      } else {
        updateProfileControllerShortcut(profileName, nextButtons);
      }
    }
    if (acknowledgedCaptureRef.current === completedCapture.sessionId) {
      return;
    }

    acknowledgedCaptureRef.current = completedCapture.sessionId;
    setControllerCaptureSuccessButtons(nextButtons);
    console.info(`[GameShell] UI success shown: ${nextButtons.join('+')}`);
    console.info('[GameShell] UI ACK sent');
    void sendBridgeCommand<ControllerStatusInfo>('acknowledgeControllerShortcutCapture', { sessionId: completedCapture.sessionId })
      .then((nextStatus) => {
        applyControllerStatus(nextStatus);
      })
      .catch(() => {
        acknowledgedCaptureRef.current = null;
        setError('Nie udało się potwierdzić zapisania kombinacji kontrolera.');
      });
    controllerCaptureCompletionTimerRef.current = setTimeout(() => {
      setControllerCaptureSuccessButtons(null);
      console.info('[GameShell] UI success dismissed');
    }, 1500);

  }, [controllerShortcutStatus.completedCapture, controllerShortcutStatus.captureSessionId, pcGamingProfileControllerShortcuts, workModeControllerShortcut]);

  useEffect(() => () => {
    if (controllerCaptureCompletionTimerRef.current) {
      clearTimeout(controllerCaptureCompletionTimerRef.current);
    }
  }, []);

  const previousControllerProfileRef = useRef(selectedPcGamingBehaviourProfileName);
  useEffect(() => {
    const previousProfile = previousControllerProfileRef.current;
    previousControllerProfileRef.current = selectedPcGamingBehaviourProfileName;
    if (previousProfile && previousProfile !== selectedPcGamingBehaviourProfileName && controllerSessionRef.current) {
      void sendBridgeCommand('cancelControllerShortcutCapture', { reason: 'ProfileChanged' }).catch(() => undefined);
    }
  }, [selectedPcGamingBehaviourProfileName]);

  const indexedStep = steps[stepIndex] ?? steps[0];
  const isNonFirstRunEditingSession = !isVeryFirstConfiguration && isEditingSection;
  const activeStepIds = isVeryFirstConfiguration
    ? steps.map((step) => step.id)
    : isCreatingProfileFlow
      ? profileSettingsStepIds
      : isEditingSection
        ? isGlobalSettingsStep(indexedStep.id)
          ? globalSettingsStepIds
          : profileSettingsStepIds
        : steps.map((step) => step.id);
  const activeSteps = activeStepIds
    .map((id) => steps.find((step) => step.id === id) ?? null)
    .filter((step): step is SetupStep => step !== null);
  const activeStepIndexes = activeSteps.map((step) => steps.findIndex((entry) => entry.id === step.id));
  const fallbackStepIndex = activeStepIndexes[0] ?? 0;
  const currentFlowPosition = activeStepIndexes.indexOf(stepIndex);
  const resolvedFlowPosition = currentFlowPosition >= 0 ? currentFlowPosition : 0;
  const resolvedStepIndex = currentFlowPosition >= 0 ? stepIndex : fallbackStepIndex;
  const currentStep = steps[resolvedStepIndex] ?? steps[0];
  const previousStepRef = useRef<StepId | null>(null);
  useEffect(() => {
    const previousStep = previousStepRef.current;
    previousStepRef.current = currentStep.id;
    if ((previousStep === 'controller' || previousStep === 'work-shortcuts')
      && currentStep.id !== 'controller'
      && currentStep.id !== 'work-shortcuts'
      && controllerSessionRef.current) {
      void sendBridgeCommand('cancelControllerShortcutCapture', { reason: 'SectionLeft' }).catch(() => undefined);
    }
  }, [currentStep.id]);

  const isLastStep = resolvedFlowPosition === activeStepIndexes.length - 1;
  const canGoBack = resolvedFlowPosition > 0;
  const isModeSwitching = activeModeAction !== null;
  const footerMessage = error || status.lastError || message;
  const canSelectSidebarStep = !isVeryFirstConfiguration && !isSaving;

  useEffect(() => {
    syncSelectionsWithActiveDisplays(displays);
  }, [displays]);

  useEffect(() => {
    const hasSelectedProfile = selectedPcGamingBehaviourProfileName.length > 0
      && pcGamingProfiles.some((profile) => profile.name === selectedPcGamingBehaviourProfileName);

    if (!hasSelectedProfile) {
      const fallbackProfileName = activePcGamingProfileName
        && pcGamingProfiles.some((profile) => profile.name === activePcGamingProfileName)
        ? activePcGamingProfileName
        : pcGamingProfiles[0]?.name ?? '';

      setSelectedPcGamingBehaviourProfileName(fallbackProfileName);
      if (fallbackProfileName) {
        setPcGamingBehaviour(pcGamingProfileBehaviours[fallbackProfileName] ?? pcGamingBehaviour);
        setLauncher(pcGamingProfileLaunchers[fallbackProfileName] ?? launcher);
        setHotkeys((current) => {
          const nextHotkeys = pcGamingProfileHotkeys[fallbackProfileName] ?? defaultProfileHotkeys;
          return {
            ...current,
            pcGamingMode: normalizeHotkeyForDisplay(nextHotkeys.enterProfile)
          };
        });
      }

      return;
    }

    setPcGamingBehaviour(pcGamingProfileBehaviours[selectedPcGamingBehaviourProfileName] ?? pcGamingBehaviour);
    setLauncher(pcGamingProfileLaunchers[selectedPcGamingBehaviourProfileName] ?? launcher);
    setHotkeys((current) => {
      const nextHotkeys = pcGamingProfileHotkeys[selectedPcGamingBehaviourProfileName] ?? defaultProfileHotkeys;
      return {
        ...current,
        pcGamingMode: normalizeHotkeyForDisplay(nextHotkeys.enterProfile)
      };
    });
  }, [
    activePcGamingProfileName,
    launcher,
    pcGamingBehaviour,
    pcGamingProfileBehaviours,
    pcGamingProfileHotkeys,
    pcGamingProfileLaunchers,
    pcGamingProfiles,
    selectedPcGamingBehaviourProfileName
  ]);

  function applySettingsToState(loadedSettings: AppSettings) {
    const consoleDisplays = loadedSettings.display?.consoleDisplays?.length
      ? loadedSettings.display.consoleDisplays
      : loadedSettings.display?.consoleDisplay ? [loadedSettings.display.consoleDisplay] : [];
    const loadedProfiles = loadedSettings.display?.pcGamingProfiles ?? [];
    const activeProfile = resolveActivePcGamingProfile(loadedSettings.display?.activePcGamingProfileName ?? null, loadedProfiles);
    const loadedProfileConfigurations = loadedSettings.display?.pcGamingProfileConfigurations ?? [];
    const loadedProfileBehaviours = loadedSettings.display?.pcGamingProfileBehaviours ?? [];
    const defaultBehaviour = toPcGamingBehaviourState(loadedSettings.pcGaming);
    const defaultLauncher = toLauncherType(loadedSettings.launcher?.type || 'Playnite');
    const nextProfileBehaviours = buildProfileBehaviourMap(loadedProfiles, loadedProfileConfigurations, loadedProfileBehaviours, defaultBehaviour);
    const nextProfileLaunchers = buildProfileLauncherMap(loadedProfiles, loadedProfileConfigurations, defaultLauncher);
    const nextProfileHotkeys = buildProfileHotkeysMap(loadedProfiles, loadedProfileConfigurations, {
      enterProfile: normalizeHotkeyForDisplay(loadedSettings.hotkeys?.pcGamingMode ?? '')
    });
    const nextProfileControllerShortcuts = buildProfileControllerShortcutMap(
      loadedProfiles,
      loadedProfileConfigurations,
      loadedSettings.controller?.pcGamingMode ?? null
    );
    const selectedProfileName = activeProfile?.name ?? loadedProfiles[0]?.name ?? '';

    setSelectedConsoleDisplays(consoleDisplays);
    setSelectedConsoleDisplay(consoleDisplays[0] ?? null);
    setPcGamingProfiles(loadedProfiles);
    setActivePcGamingProfileName(activeProfile?.name ?? null);
    setPcGamingProfileBehaviours(nextProfileBehaviours);
    setPcGamingProfileLaunchers(nextProfileLaunchers);
    setPcGamingProfileHotkeys(nextProfileHotkeys);
    setPcGamingProfileControllerShortcuts(nextProfileControllerShortcuts);
    setWorkModeControllerShortcut(normalizeControllerShortcut(loadedSettings.controller?.workMode ?? null));
    setSelectedPcGamingBehaviourProfileName(selectedProfileName);
    setPcGamingLayout(activeProfile?.layout ?? loadedSettings.display?.pcGamingLayout ?? null);
    setHotkeys({
      consoleMode: normalizeHotkeyForDisplay(loadedSettings.hotkeys?.consoleMode || ''),
      pcGamingMode: normalizeHotkeyForDisplay(loadedSettings.hotkeys?.pcGamingMode || ''),
      workMode: normalizeHotkeyForDisplay(loadedSettings.hotkeys?.workMode || '')
    });
    setLauncher(nextProfileLaunchers[selectedProfileName] ?? defaultLauncher);
    setConsoleBehaviour({
      hideDesktopIcons: loadedSettings.gameShell?.hideDesktopIcons ?? true,
      hideTaskbar: loadedSettings.gameShell?.hideTaskbar ?? true,
      closeVisibleWindows: loadedSettings.gameShell?.closeVisibleWindows ?? true,
      minimizeRemainingWindows: loadedSettings.gameShell?.minimizeRemainingWindows ?? true,
      moveCursorToCorner: loadedSettings.gameShell?.moveCursorToCorner ?? true,
      hideCursor: loadedSettings.gameShell?.hideCursor ?? true,
      startLauncher: loadedSettings.gameShell?.startLauncher ?? true,
      bringLauncherToForeground: loadedSettings.gameShell?.bringLauncherToForeground ?? true
    });
    setPcGamingBehaviour(nextProfileBehaviours[selectedProfileName] ?? defaultBehaviour);
    setWorkBehaviour({
      closeLauncher: loadedSettings.workMode?.closeLauncher ?? true,
      showTaskbars: loadedSettings.workMode?.showTaskbars ?? true,
      showDesktopIcons: loadedSettings.workMode?.showDesktopIcons ?? true,
      doNotMinimizeWindows: loadedSettings.workMode?.doNotMinimizeWindows ?? true
    });
    setFinishOptions({
      startWithWindows: loadedSettings.startup?.startWithWindows ?? false,
      startMinimized: loadedSettings.startup?.startMinimizedToTray ?? false,
      startMenuShortcut: loadedSettings.startup?.createStartMenuShortcut ?? true,
      desktopShortcut: loadedSettings.startup?.createDesktopShortcut ?? false
    });
    setIsVeryFirstConfiguration(!loadedSettings.ui?.isFirstRunCompleted);
    setIsSetupComplete(loadedSettings.ui?.isFirstRunCompleted ?? false);
    setHasUnsavedChanges(false);
    pendingSettingsOverrideRef.current = null;
  }

  function markUnsavedChanges() {
    setHasUnsavedChanges(true);
  }

  function syncSelectionsWithActiveDisplays(detectedDisplays: DisplayDeviceInfo[]) {
    const activeDisplays = detectedDisplays.filter((display) => display.isEnabled);
    const nextConsoleDisplays = activeDisplays.map((display) => display.identity);
    const primaryDisplay = activeDisplays.find((display) => display.isPrimary) ?? activeDisplays[0] ?? null;

    setSelectedConsoleDisplays(nextConsoleDisplays);
    setSelectedConsoleDisplay(primaryDisplay?.identity ?? null);
  }

  function setPcGamingBehaviourForProfile(profileName: string, behaviour: PcGamingBehaviourState) {
    setPcGamingProfileBehaviours((current) => ({
      ...current,
      [profileName]: behaviour
    }));
    setPcGamingBehaviour(behaviour);
    markUnsavedChanges();
  }

  function updatePcGamingBehaviourFlag(key: PcGamingBehaviourKey, value: boolean) {
    const profileName = selectedPcGamingBehaviourProfileName;
    if (!profileName) {
      setPcGamingBehaviour((current) => ({
        ...current,
        [key]: value
      }));
      return;
    }

    const currentProfileBehaviour = pcGamingProfileBehaviours[profileName] ?? pcGamingBehaviour;
    setPcGamingBehaviourForProfile(profileName, {
      ...currentProfileBehaviour,
      [key]: value
    });
  }

  function setProfileLauncher(profileName: string, nextLauncher: LauncherType) {
    setPcGamingProfileLaunchers((current) => ({
      ...current,
      [profileName]: nextLauncher
    }));
    setLauncher(nextLauncher);
    markUnsavedChanges();
  }

  function updateProfileHotkey(profileName: string, key: keyof ProfileHotkeysState, value: string) {
    const normalizedValue = normalizeHotkeyForDisplay(value);

    if (key === 'enterProfile' && (isDuplicateProfileEnterHotkey(profileName, normalizedValue, pcGamingProfileHotkeys)
      || isSameHotkey(normalizedValue, hotkeys.workMode))) {
      setProfileHotkeyValidationError('Ten skrót jest już przypisany do innego profilu lub Work Mode. Wybierz inną kombinację.');
      return;
    }

    setProfileHotkeyValidationError(null);
    setError(null);
    setPcGamingProfileHotkeys((current) => ({
      ...current,
      [profileName]: {
        ...(current[profileName] ?? defaultProfileHotkeys),
        [key]: normalizedValue
      }
    }));
    markUnsavedChanges();

    if (selectedPcGamingBehaviourProfileName === profileName) {
      setHotkeys((current) => ({
        ...current,
        pcGamingMode: key === 'enterProfile' ? normalizedValue : current.pcGamingMode
      }));
    }
  }

  function updateProfileControllerShortcut(profileName: string, buttons: string[]) {
    const normalizedButtons = normalizeControllerButtons(buttons);

    if (isDuplicateProfileControllerShortcut(profileName, normalizedButtons, pcGamingProfileControllerShortcuts)
      || isSameControllerShortcut(normalizedButtons, workModeControllerShortcut?.buttons)) {
      setProfileControllerValidationError('Ta kombinacja przyciskow jest juz przypisana do innego profilu lub Work Mode. Wybierz inna kombinacje.');
      return;
    }

    setProfileControllerValidationError(null);
    setError(null);
    setPcGamingProfileControllerShortcuts((current) => ({
      ...current,
      [profileName]: normalizedButtons.length === 0
        ? null
        : {
          buttons: normalizedButtons,
          holdDurationMs: current[profileName]?.holdDurationMs ?? defaultControllerShortcutHoldDurationMs
        }
    }));
    markUnsavedChanges();
  }

  function updateWorkModeControllerShortcut(buttons: string[]) {
    const normalizedButtons = normalizeControllerButtons(buttons);
    if (isDuplicateWorkModeControllerShortcut(normalizedButtons, pcGamingProfileControllerShortcuts)) {
      setProfileControllerValidationError('Ta kombinacja przyciskow jest juz przypisana do profilu. Wybierz inna kombinacje.');
      return;
    }

    setProfileControllerValidationError(null);
    setError(null);
    setWorkModeControllerShortcut(normalizedButtons.length === 0
      ? null
      : {
        buttons: normalizedButtons,
        holdDurationMs: workModeControllerShortcut?.holdDurationMs ?? defaultControllerShortcutHoldDurationMs
      });
    markUnsavedChanges();
  }

  function updateWorkModeHotkey(value: string) {
    const normalizedValue = normalizeHotkeyForDisplay(value);
    if (isDuplicateWorkModeHotkey(normalizedValue, pcGamingProfileHotkeys)) {
      setProfileHotkeyValidationError('Ten skrót jest już przypisany do profilu. Wybierz inną kombinację.');
      return;
    }

    setProfileHotkeyValidationError(null);
    setError(null);
    setHotkeys((current) => ({ ...current, workMode: normalizedValue }));
    markUnsavedChanges();
  }

  function buildSettingsPayload(
    isFirstRunCompleted = isSetupComplete,
    overrides?: SettingsPayloadOverrides
  ): AppSettings {
    const effectivePcGamingProfiles = overrides?.pcGamingProfiles ?? pcGamingProfiles;
    const effectiveActivePcGamingProfileName = overrides?.activePcGamingProfileName ?? activePcGamingProfileName;
    const effectivePcGamingLayout = overrides?.pcGamingLayout ?? pcGamingLayout;
    const effectivePcGamingProfileBehaviours: PcGamingProfileBehaviourMap = {
      ...(overrides?.pcGamingProfileBehaviours ?? {}),
      ...pcGamingProfileBehaviours
    };
    const effectivePcGamingProfileLaunchers: PcGamingProfileLauncherMap = {
      ...(overrides?.pcGamingProfileLaunchers ?? {}),
      ...pcGamingProfileLaunchers
    };
    const effectivePcGamingProfileHotkeys: PcGamingProfileHotkeysMap = {
      ...(overrides?.pcGamingProfileHotkeys ?? {}),
      ...pcGamingProfileHotkeys
    };
    const effectivePcGamingProfileControllerShortcuts: PcGamingProfileControllerShortcutMap = {
      ...(overrides?.pcGamingProfileControllerShortcuts ?? {}),
      ...pcGamingProfileControllerShortcuts
    };

    const activeProfileName = resolvePcGamingBehaviourProfileName(effectiveActivePcGamingProfileName, effectivePcGamingProfiles);
    const activeProfileBehaviour = activeProfileName ? effectivePcGamingProfileBehaviours[activeProfileName] : null;
    const activeProfileLauncher = activeProfileName ? effectivePcGamingProfileLaunchers[activeProfileName] : null;
    const activeProfileHotkeys = activeProfileName ? effectivePcGamingProfileHotkeys[activeProfileName] : null;
    const activeProfileControllerShortcut = activeProfileName ? effectivePcGamingProfileControllerShortcuts[activeProfileName] : null;
    const savedPcGamingBehaviour = activeProfileBehaviour ?? pcGamingBehaviour;
    const savedLauncher = activeProfileLauncher ?? launcher;
    const launcherSettings = getLauncherSettings(savedLauncher);
    const savedHotkeys = activeProfileHotkeys ?? {
      enterProfile: hotkeys.pcGamingMode
    };

    return {
      display: {
        consoleDisplay: selectedConsoleDisplay,
        consoleDisplays: selectedConsoleDisplays,
        pcGamingLayout: effectivePcGamingLayout,
        pcGamingProfiles: effectivePcGamingProfiles,
        activePcGamingProfileName: effectiveActivePcGamingProfileName,
        pcGamingProfileConfigurations: effectivePcGamingProfiles.map((profile) => ({
          profileName: profile.name,
          behaviour: effectivePcGamingProfileBehaviours[profile.name] ?? savedPcGamingBehaviour,
          launcher: {
            ...getLauncherSettings(effectivePcGamingProfileLaunchers[profile.name] ?? savedLauncher),
            targetDisplayId: selectedConsoleDisplay?.monitorDevicePath ?? null
          },
          hotkeys: {
            enterProfile: normalizeHotkeyForDisplay(effectivePcGamingProfileHotkeys[profile.name]?.enterProfile ?? savedHotkeys.enterProfile ?? ''),
            enterWorkMode: ''
          },
          controllerShortcut: normalizeControllerShortcut(effectivePcGamingProfileControllerShortcuts[profile.name] ?? null)
        })),
        pcGamingProfileBehaviours: effectivePcGamingProfiles.map((profile) => ({
          profileName: profile.name,
          behaviour: effectivePcGamingProfileBehaviours[profile.name] ?? savedPcGamingBehaviour
        }))
      },
      launcher: {
        type: launcherSettings.type,
        executablePath: launcherSettings.executablePath,
        arguments: launcherSettings.arguments,
        targetDisplayId: selectedConsoleDisplay?.monitorDevicePath ?? null
      },
      gameShell: {
        ...consoleBehaviour,
        completionDelaySeconds: settings?.gameShell.completionDelaySeconds ?? 5
      },
      pcGaming: {
        ...savedPcGamingBehaviour,
        protectedProcessNames: settings?.pcGaming?.protectedProcessNames ?? [],
        closeOnlyProcessNames: settings?.pcGaming?.closeOnlyProcessNames ?? [],
        completionDelaySeconds: settings?.pcGaming?.completionDelaySeconds ?? 5
      },
      workMode: {
        ...workBehaviour,
        taskbarRestoreDelaySeconds: settings?.workMode.taskbarRestoreDelaySeconds ?? 3,
        desktopIconsDelaySeconds: settings?.workMode.desktopIconsDelaySeconds ?? 15,
        completionDelaySeconds: settings?.workMode.completionDelaySeconds ?? 20
      },
      audio: {
        switchAudioInConsoleMode: settings?.audio?.switchAudioInConsoleMode ?? false,
        restoreAudioInWorkMode: settings?.audio?.restoreAudioInWorkMode ?? false,
        consoleAudioDeviceId: settings?.audio?.consoleAudioDeviceId ?? null,
        consoleAudioDeviceName: settings?.audio?.consoleAudioDeviceName ?? null
      },
      controller: {
        enabled: Object.values(effectivePcGamingProfileControllerShortcuts).some((shortcut) => (shortcut?.buttons.length ?? 0) > 0)
          || (workModeControllerShortcut?.buttons.length ?? 0) > 0
          || settings?.controller?.enabled
          || false,
        consoleMode: settings?.controller?.consoleMode ?? null,
        pcGamingMode: activeProfileName
          ? normalizeControllerShortcut(activeProfileControllerShortcut)
          : normalizeControllerShortcut(settings?.controller?.pcGamingMode ?? null),
        workMode: normalizeControllerShortcut(workModeControllerShortcut)
      },
      hotkeys: {
        consoleMode: normalizeHotkeyForDisplay(hotkeys.consoleMode),
        pcGamingMode: normalizeHotkeyForDisplay(savedHotkeys.enterProfile || hotkeys.pcGamingMode),
        workMode: normalizeHotkeyForDisplay(hotkeys.workMode)
      },
      startup: {
        startWithWindows: finishOptions.startWithWindows,
        startMinimizedToTray: finishOptions.startMinimized,
        createStartMenuShortcut: finishOptions.startMenuShortcut,
        createDesktopShortcut: finishOptions.desktopShortcut
      },
      ui: {
        closeToTray: true,
        isFirstRunCompleted
      }
    };
  }

  async function saveCurrentSettings(isFirstRunCompleted = isSetupComplete, overrides?: SettingsPayloadOverrides) {
    const savedSettings = await sendBridgeCommand<AppSettings>('saveSettings', buildSettingsPayload(isFirstRunCompleted, overrides));
    setSettings(savedSettings);
    applySettingsToState(savedSettings);
    setStatus((current) => ({
      ...current,
      selectedConsoleDisplay: savedSettings.display.consoleDisplay?.friendlyName ?? null,
      hasPcGamingLayout: Boolean(savedSettings.display.pcGamingLayout) || (savedSettings.display.pcGamingProfiles?.length ?? 0) > 0,
      activeLauncher: savedSettings.launcher.type
    }));
    return savedSettings;
  }

  async function persistAndReturnToDashboard() {
    setError(null);

    if (pcGamingProfiles.length === 0) {
      setError('Najpierw dodaj co najmniej jeden profil ekranów.');
      return;
    }

    setIsSaving(true);
    setMessage(isEditingSection ? 'Zapisywanie zmian...' : 'Zapisywanie konfiguracji...');

    try {
      const preferredProfileName = resolvePreferredProfileNameForPostSaveSelection(
        displayProfileDraftRef.current.profileName,
        selectedPcGamingBehaviourProfileName
      );
      const savedSettings = await saveCurrentSettings(true, pendingSettingsOverrideRef.current ?? undefined);
      pendingSettingsOverrideRef.current = null;

      const savedProfiles = savedSettings.display?.pcGamingProfiles ?? [];
      const matchedPreferred = preferredProfileName
        ? savedProfiles.find((profile) => profile.name.toLowerCase() === preferredProfileName.toLowerCase())?.name ?? null
        : null;
      if (matchedPreferred) {
        setSelectedPcGamingBehaviourProfileName(matchedPreferred);
      }

      setIsEditingSection(false);
      setIsCreatingProfileFlow(false);
      setIsSetupComplete(savedSettings.ui.isFirstRunCompleted);
      setMessage(isEditingSection ? 'Zmiany zapisane.' : 'Konfiguracja zapisana. GameShell jest gotowy do użycia.');
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Nie udało się zapisać konfiguracji.');
    } finally {
      setIsSaving(false);
    }
  }

  async function saveAndReturnToDashboard() {
    if (isCreatingProfileFlow || currentStep.id === 'gaming-display') {
      const committed = await commitGamingDisplayDraft(false, 'save');
      if (!committed) {
        return;
      }

      // Let React commit profile state updates before building save payload.
      await new Promise((resolve) => setTimeout(resolve, 0));
    }

    await persistAndReturnToDashboard();
  }

  function requestNavigation(action: PendingNavigationAction) {
    if (isVeryFirstConfiguration) {
      void executeNavigation(action);
      return;
    }

    if (action === 'goNext') {
      void executeNavigation(action);
      return;
    }

    if (isCreatingProfileFlow && action === 'goBack') {
      void executeNavigation(action);
      return;
    }

    if (isCreatingProfileFlow && action === 'returnToDashboard' && !hasAdvancedInCreateProfileFlow) {
      void executeNavigation(action);
      return;
    }

    if (!hasUnsavedChanges) {
      void executeNavigation(action);
      return;
    }

    setPendingNavigationAction(action);
    setShowUnsavedDialog(true);
  }

  async function handleSidebarStepSelect(stepId: StepId) {
    if (!canSelectSidebarStep) {
      return;
    }

    const targetIndex = steps.findIndex((entry) => entry.id === stepId);
    if (targetIndex < 0 || targetIndex === stepIndex) {
      return;
    }

    if (currentStep.id === 'gaming-display') {
      const committed = await commitGamingDisplayDraft(false, 'next');
      if (!committed) {
        return;
      }
    }

    setError(null);
    setStepIndex(targetIndex);
    setMessage('Edytujesz wybraną sekcję.');
  }

  async function executeNavigation(action: PendingNavigationAction) {
    switch (action) {
      case 'goBack':
        goBackInternal();
        break;
      case 'goNext':
        await goNextInternal();
        break;
      case 'returnToDashboard':
        returnToDashboardInternal();
        break;
      default:
        break;
    }
  }

  async function goNextInternal() {
    setError(null);

    if (currentStep.id === 'gaming-display') {
      await continueFromGamingDisplayStep(false);
      return;
    }

    if (isLastStep) {
      await saveAndReturnToDashboard();
      return;
    }

    const nextIndex = activeStepIndexes[resolvedFlowPosition + 1];
    if (typeof nextIndex === 'number') {
      setStepIndex(nextIndex);
      if (isCreatingProfileFlow) {
        setHasAdvancedInCreateProfileFlow(true);
      }
    }
    setMessage(isEditingSection ? 'Możesz przejrzeć kolejne sekcje albo zapisać zmiany.' : 'Gotowe.');
  }

  async function continueFromGamingDisplayStep(allowOverwrite: boolean) {
    const committed = await commitGamingDisplayDraft(allowOverwrite, 'next');
    if (!committed) {
      return;
    }

    const nextIndex = activeStepIndexes[resolvedFlowPosition + 1];
    if (typeof nextIndex === 'number') {
      setStepIndex(nextIndex);
      if (isCreatingProfileFlow) {
        setHasAdvancedInCreateProfileFlow(true);
      }
    }
    setMessage(isEditingSection ? 'Możesz przejrzeć kolejne sekcje albo zapisać zmiany.' : 'Gotowe.');
  }

  async function commitGamingDisplayDraft(allowOverwrite: boolean, overwriteAction: OverwriteProfileAction) {
    const latestDraft = displayProfileDraftRef.current;
    const profileName = latestDraft.profileName.trim();
    const identities = latestDraft.identities;
    const missingName = profileName.length === 0;
    const missingDisplays = identities.length === 0;
    setDisplayValidationState({
      showNameError: missingName,
      showSelectionError: missingDisplays
    });
    const hasDraft = profileName.length > 0 && identities.length > 0;
    const hasPartialDraft = profileName.length > 0 || identities.length > 0;

    if (hasPartialDraft && !hasDraft) {
      setError('Uzupełnij nazwę nowego profilu i wybierz co najmniej jeden ekran.');
      return false;
    }

    if (isCreatingProfileFlow && !hasDraft) {
      setError('Uzupełnij nazwę nowego profilu i wybierz co najmniej jeden ekran.');
      return false;
    }

    if (!hasDraft && pcGamingProfiles.length === 0) {
      setError('Musisz dodać pierwszy profil ekranów, aby kontynuować.');
      return false;
    }

    if (hasDraft && identities.length > MAX_PROFILE_DISPLAYS) {
      setError(PROFILE_DISPLAY_LIMIT_MESSAGE);
      return false;
    }

    if (hasDraft) {
      const existingProfile = pcGamingProfiles.find((profile) => profile.name.toLowerCase() === profileName.toLowerCase()) ?? null;
      const isEditingSelectedProfile = selectedPcGamingBehaviourProfileName.length > 0
        && selectedPcGamingBehaviourProfileName.toLowerCase() === profileName.toLowerCase();
      const isLayoutUnchanged = existingProfile
        ? areIdentitySetsEqual(identities, extractDisplayIdentities(existingProfile.layout))
        : false;

      if (existingProfile && !isEditingSelectedProfile && !isLayoutUnchanged && !allowOverwrite) {
        setPendingOverwriteProfileAction(overwriteAction);
        setShowOverwriteProfileDialog(true);
        return false;
      }

      if (!existingProfile || !isLayoutUnchanged) {
        await saveSelectedPcGamingLayout(profileName, identities);
      }
    }

    setDisplayValidationState({
      showNameError: false,
      showSelectionError: false
    });

    return true;
  }

  async function handleOverwriteProfileDialogConfirm() {
    setShowOverwriteProfileDialog(false);
    const action = pendingOverwriteProfileAction;
    setPendingOverwriteProfileAction(null);

    const committed = await commitGamingDisplayDraft(true, action ?? 'next');
    if (!committed) {
      return;
    }

    if (action === 'save') {
      // Let React commit profile state updates before building save payload.
      await new Promise((resolve) => setTimeout(resolve, 0));
      await persistAndReturnToDashboard();
      return;
    }

    if (action === 'save-then-continue') {
      try {
        setIsSaving(true);

        // Let React commit profile state updates before building save payload.
        await new Promise((resolve) => setTimeout(resolve, 0));

        await saveCurrentSettings(isSetupComplete, pendingSettingsOverrideRef.current ?? undefined);
        pendingSettingsOverrideRef.current = null;
        setHasUnsavedChanges(false);

        if (pendingNavigationAction) {
          const actionToRun = pendingNavigationAction;
          setPendingNavigationAction(null);
          await executeNavigation(actionToRun);
        }
      } catch (reason: unknown) {
        setError(reason instanceof Error ? reason.message : 'Nie udało się zapisać konfiguracji.');
      } finally {
        setIsSaving(false);
      }

      return;
    }

    const nextIndex = activeStepIndexes[resolvedFlowPosition + 1];
    if (typeof nextIndex === 'number') {
      setStepIndex(nextIndex);
    }
    setMessage(isEditingSection ? 'Możesz przejrzeć kolejne sekcje albo zapisać zmiany.' : 'Gotowe.');
  }

  function goBackInternal() {
    setError(null);
    const previousIndex = activeStepIndexes[resolvedFlowPosition - 1];
    if (typeof previousIndex === 'number') {
      setStepIndex(previousIndex);
    }
  }

  function returnToDashboardInternal() {
    setError(null);
    setIsEditingSection(false);
    setIsCreatingProfileFlow(false);
    setHasAdvancedInCreateProfileFlow(false);
    setIsSetupComplete(true);
    syncDisplayProfileDraft({ profileName: '', identities: [] });
    setShowOverwriteProfileDialog(false);
    setPendingOverwriteProfileAction(null);
    pendingSettingsOverrideRef.current = null;
    setMessage('Gotowe.');
  }

  async function handleUnsavedDialogSave() {
    if (!pendingNavigationAction) {
      setShowUnsavedDialog(false);
      return;
    }

    setShowUnsavedDialog(false);

    if (pendingNavigationAction === 'goNext' && isLastStep) {
      await saveAndReturnToDashboard();
      setPendingNavigationAction(null);
      return;
    }

    let shouldResetPendingNavigation = true;

    try {
      setIsSaving(true);

      if (currentStep.id === 'gaming-display') {
        const committed = await commitGamingDisplayDraft(false, 'save-then-continue');
        if (!committed) {
          shouldResetPendingNavigation = false;
          return;
        }

        // Let React commit profile state updates before building save payload.
        await new Promise((resolve) => setTimeout(resolve, 0));
      }

      await saveCurrentSettings(isSetupComplete, pendingSettingsOverrideRef.current ?? undefined);
      pendingSettingsOverrideRef.current = null;
      setHasUnsavedChanges(false);
      await executeNavigation(pendingNavigationAction);
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Nie udało się zapisać konfiguracji.');
    } finally {
      setIsSaving(false);
      if (shouldResetPendingNavigation) {
        setPendingNavigationAction(null);
      }
    }
  }

  async function handleUnsavedDialogDiscard() {
    const action = pendingNavigationAction;
    setShowUnsavedDialog(false);
    setPendingNavigationAction(null);

    if (settings) {
      applySettingsToState(settings);
    } else {
      setHasUnsavedChanges(false);
      pendingSettingsOverrideRef.current = null;
    }

    if (action) {
      await executeNavigation(action);
    }
  }

  async function refreshDisplays() {
    setIsRefreshingDisplays(true);
    setDisplayListError(null);
    setError(null);
    try {
      const detectedDisplays = await sendBridgeCommand<DisplayDeviceInfo[]>('getDisplays');
      setDisplays(detectedDisplays);
      setMessage(detectedDisplays.length === 0 ? 'Nie wykryto aktywnych ekranów.' : 'Lista ekranów została odświeżona.');
    } catch (reason: unknown) {
      setDisplayListError(reason instanceof Error ? reason.message : 'Nie udało się odczytać konfiguracji ekranów.');
    } finally {
      setIsRefreshingDisplays(false);
    }
  }

  async function saveSelectedPcGamingLayout(profileName: string, identities: DisplayIdentity[]) {
    const trimmedName = profileName.trim();
    if (!trimmedName || identities.length === 0) {
      setError('Uzupełnij nazwę nowego profilu i wybierz co najmniej jeden ekran.');
      return;
    }

    if (identities.length > MAX_PROFILE_DISPLAYS) {
      setError(PROFILE_DISPLAY_LIMIT_MESSAGE);
      return;
    }

    const selectedDevices = identities
      .map((identity) => displays.find((display) => sameIdentity(display.identity, identity)) ?? null)
      .filter((display): display is DisplayDeviceInfo => display !== null);

    if (selectedDevices.length !== identities.length || selectedDevices.length === 0) {
      setError('Nie wszystkie wybrane ekrany są obecnie dostępne. Odśwież listę ekranów.');
      return;
    }

    const primary = selectedDevices.find((display) => display.isPrimary) ?? selectedDevices[0];
    const originX = primary.positionX;
    const originY = primary.positionY;
    const layout: DisplayLayoutSnapshot = {
      id: `${Date.now()}`,
      capturedAt: new Date().toISOString(),
      displays: selectedDevices.map((display) => ({
        identity: display.identity,
        isActive: true,
        isPrimary: sameIdentity(primary.identity, display.identity),
        positionX: display.positionX - originX,
        positionY: display.positionY - originY,
        width: display.width,
        height: display.height,
        orientation: display.orientation,
        refreshRateNumerator: display.refreshRateNumerator,
        refreshRateDenominator: display.refreshRateDenominator,
        refreshRateHz: display.refreshRateHz,
        sourceId: display.sourceId,
        targetId: display.targetId,
        adapterLuid: display.adapterLuid,
        sourceDeviceName: display.sourceDeviceName,
        connectionTechnology: display.connectionTechnology
      }))
    };

    const existingProfile = pcGamingProfiles.find((profile) => profile.name.toLowerCase() === trimmedName.toLowerCase()) ?? null;
    const nextProfiles = existingProfile
      ? pcGamingProfiles.map((profile) => profile.name.toLowerCase() === trimmedName.toLowerCase()
        ? { ...profile, name: existingProfile.name, layout }
        : profile)
      : [...pcGamingProfiles, { name: trimmedName, layout }];
    const resolvedProfileName = existingProfile?.name ?? trimmedName;
    const nextProfileBehaviours: PcGamingProfileBehaviourMap = {
      ...pcGamingProfileBehaviours,
      [resolvedProfileName]: pcGamingProfileBehaviours[resolvedProfileName] ?? pcGamingBehaviour
    };
    const nextProfileLaunchers: PcGamingProfileLauncherMap = {
      ...pcGamingProfileLaunchers,
      [resolvedProfileName]: pcGamingProfileLaunchers[resolvedProfileName] ?? launcher
    };
    const nextProfileHotkeys: PcGamingProfileHotkeysMap = {
      ...pcGamingProfileHotkeys,
      [resolvedProfileName]: existingProfile
        ? (pcGamingProfileHotkeys[resolvedProfileName] ?? {
          enterProfile: hotkeys.pcGamingMode
        })
        : {
          enterProfile: ''
        }
    };
    const nextProfileControllerShortcuts: PcGamingProfileControllerShortcutMap = {
      ...pcGamingProfileControllerShortcuts,
      [resolvedProfileName]: existingProfile
        ? (pcGamingProfileControllerShortcuts[resolvedProfileName] ?? null)
        : null
    };

    setPcGamingProfiles(nextProfiles);
    setActivePcGamingProfileName(resolvedProfileName);
    setPcGamingLayout(layout);
    setSelectedPcGamingBehaviourProfileName(resolvedProfileName);
    setPcGamingProfileBehaviours(nextProfileBehaviours);
    setPcGamingProfileLaunchers(nextProfileLaunchers);
    setPcGamingProfileHotkeys(nextProfileHotkeys);
    setPcGamingProfileControllerShortcuts(nextProfileControllerShortcuts);
    pendingSettingsOverrideRef.current = {
      pcGamingProfiles: nextProfiles,
      activePcGamingProfileName: resolvedProfileName,
      pcGamingLayout: layout,
      pcGamingProfileBehaviours: nextProfileBehaviours,
      pcGamingProfileLaunchers: nextProfileLaunchers,
      pcGamingProfileHotkeys: nextProfileHotkeys,
      pcGamingProfileControllerShortcuts: nextProfileControllerShortcuts
    };
    markUnsavedChanges();
    setError(null);
    setMessage(
      isEditingSection
        ? `Profil ekranow przygotowany: ${resolvedProfileName}. Zapisz zmiany, aby zatwierdzić.`
        : `Profil ekranow przygotowany: ${resolvedProfileName}. Kliknij Dalej, aby kontynuować konfigurację.`
    );
  }

  async function runModeCommand(command: ModeCommand, saveBeforeRun = false, payload?: unknown) {
    const actionKey = getModeActionKey(command);
    setActiveModeAction(actionKey);
    setError(null);
    setMessage(`Przełączanie na ${formatCommandTargetMode(command)}...`);

    try {
      const shouldSaveBeforeRun = saveBeforeRun || hasUnsavedChanges;
      if (shouldSaveBeforeRun) {
        await saveCurrentSettings(isSetupComplete);
      }

      const result = await sendBridgeCommand<ModeResult>(command, payload);
      setStatus(await sendBridgeCommand<AppStatus>('getStatus'));

      if (command === 'enterGameShellWithProfile') {
        const requestedProfileName = typeof payload === 'object' && payload !== null && 'profileName' in payload
          ? String((payload as { profileName?: unknown }).profileName ?? '').trim()
          : '';

        if (requestedProfileName) {
          const matchedProfile = pcGamingProfiles.find((profile) => profile.name.toLowerCase() === requestedProfileName.toLowerCase()) ?? null;
          if (matchedProfile) {
            setActivePcGamingProfileName(matchedProfile.name);
            setSelectedPcGamingBehaviourProfileName(matchedProfile.name);
            setPcGamingLayout(matchedProfile.layout);
          }
        }
      }

      setMessage(command === 'enterWorkMode' && result.message === 'Enter Work Mode completed.'
        ? 'Przełączono do Work Mode.'
        : result.message);
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Nie udało się uruchomić trybu.');
    } finally {
      setActiveModeAction(null);
    }
  }

  async function deleteSelectedProfile() {
    const profileName = selectedPcGamingBehaviourProfileName.trim();
    if (!profileName) {
      return;
    }

    const profileKey = profileName.toLowerCase();
    const nextProfiles = pcGamingProfiles.filter((profile) => profile.name.toLowerCase() !== profileKey);
    if (nextProfiles.length === pcGamingProfiles.length) {
      return;
    }

    const nextProfileBehaviours = Object.fromEntries(
      Object.entries(pcGamingProfileBehaviours).filter(([name]) => name.toLowerCase() !== profileKey)
    ) as PcGamingProfileBehaviourMap;
    const nextProfileLaunchers = Object.fromEntries(
      Object.entries(pcGamingProfileLaunchers).filter(([name]) => name.toLowerCase() !== profileKey)
    ) as PcGamingProfileLauncherMap;
    const nextProfileHotkeys = Object.fromEntries(
      Object.entries(pcGamingProfileHotkeys).filter(([name]) => name.toLowerCase() !== profileKey)
    ) as PcGamingProfileHotkeysMap;
    const nextProfileControllerShortcuts = Object.fromEntries(
      Object.entries(pcGamingProfileControllerShortcuts).filter(([name]) => name.toLowerCase() !== profileKey)
    ) as PcGamingProfileControllerShortcutMap;

    const currentActiveKey = activePcGamingProfileName?.toLowerCase() ?? null;
    const deletedActiveProfile = currentActiveKey === profileKey;
    const nextActiveProfileName = deletedActiveProfile
      ? nextProfiles[0]?.name ?? null
      : activePcGamingProfileName && nextProfiles.some((profile) => profile.name === activePcGamingProfileName)
        ? activePcGamingProfileName
        : nextProfiles[0]?.name ?? null;
    const nextLayout = nextActiveProfileName
      ? nextProfiles.find((profile) => profile.name === nextActiveProfileName)?.layout ?? null
      : null;

    setError(null);
    setIsRemovingProfile(true);
    setMessage(`Usuwanie profilu: ${profileName}...`);

    try {
      await saveCurrentSettings(true, {
        pcGamingProfiles: nextProfiles,
        activePcGamingProfileName: nextActiveProfileName,
        pcGamingLayout: nextLayout,
        pcGamingProfileBehaviours: nextProfileBehaviours,
        pcGamingProfileLaunchers: nextProfileLaunchers,
        pcGamingProfileHotkeys: nextProfileHotkeys,
        pcGamingProfileControllerShortcuts: nextProfileControllerShortcuts
      });
      setMessage(`Profil został usunięty: ${profileName}.`);
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Nie udało się usunąć profilu.');
    } finally {
      setIsRemovingProfile(false);
    }
  }

  if (isInitialLoading) {
    return <LoadingScreen />;
  }

  if (isSetupComplete) {
    return (
      <Dashboard
        status={status}
        launcher={launcher}
        pcGamingLayout={pcGamingLayout}
        pcGamingProfiles={pcGamingProfiles}
        pcGamingProfileLaunchers={pcGamingProfileLaunchers}
        pcGamingProfileHotkeys={pcGamingProfileHotkeys}
        activeProfileName={activePcGamingProfileName}
        selectedProfileName={selectedPcGamingBehaviourProfileName}
        globalWorkModeHotkey={hotkeys.workMode}
        isModeSwitching={isModeSwitching}
        isRemovingProfile={isRemovingProfile}
        activeModeAction={activeModeAction}
        message={footerMessage}
        hasError={Boolean(error || status.lastError)}
        runModeCommand={runModeCommand}
        deleteSelectedProfile={deleteSelectedProfile}
        selectProfile={setSelectedPcGamingBehaviourProfileName}
        createProfile={() => {
          const targetIndex = steps.findIndex((step) => step.id === 'gaming-display');
          setIsEditingSection(false);
          setIsCreatingProfileFlow(true);
          setHasAdvancedInCreateProfileFlow(false);
          setIsSetupComplete(false);
          syncDisplayProfileDraft({ profileName: '', identities: [] });
          setProfileHotkeyValidationError(null);
          setProfileControllerValidationError(null);
          setHotkeys((current) => ({
            ...current,
            pcGamingMode: ''
          }));
          setShowOverwriteProfileDialog(false);
          setStepIndex(targetIndex >= 0 ? targetIndex : 0);
          setMessage('Dodaj nowy profil i przejdź przez kroki profilu.');
        }}
        editSettings={(stepId) => {
          const targetIndex = steps.findIndex((step) => step.id === stepId);
          setIsEditingSection(true);
          setIsCreatingProfileFlow(false);
          setHasAdvancedInCreateProfileFlow(false);
          setIsSetupComplete(false);
          if (stepId === 'gaming-display') {
            const selectedProfile = pcGamingProfiles.find((profile) => profile.name === selectedPcGamingBehaviourProfileName) ?? null;
            if (selectedProfile) {
              syncDisplayProfileDraft({
                profileName: selectedProfile.name,
                identities: extractDisplayIdentities(selectedProfile.layout)
              });
            }
          }
          setStepIndex(targetIndex >= 0 ? targetIndex : 0);
          setMessage('Edytujesz wybraną sekcję.');
        }}
      />
    );
  }

  return (
    <main className="app-shell">
      <section className="setup-window" aria-labelledby="app-title">
        <aside className="brand-panel" aria-label="Kroki konfiguracji GameShell">
          <div className="brand-lockup">
            <img className="brand-mark" src="/assets/GameShell.png" alt="" aria-hidden="true" />
            <div>
              <p>GameShell Setup</p>
              <small>lokalne profile gamingowe</small>
            </div>
          </div>
          <ol className="step-list">
            {activeSteps.map((step, index) => (
              <li
                className={`step-item ${index === resolvedFlowPosition ? 'active' : ''} ${index < resolvedFlowPosition ? 'done' : ''} ${canSelectSidebarStep ? 'clickable' : ''}`}
                key={step.id}
                role={canSelectSidebarStep ? 'button' : undefined}
                tabIndex={canSelectSidebarStep ? 0 : undefined}
                onClick={() => {
                  void handleSidebarStepSelect(step.id);
                }}
                onKeyDown={(event: KeyboardEvent<HTMLLIElement>) => {
                  if (!canSelectSidebarStep || (event.key !== 'Enter' && event.key !== ' ')) {
                    return;
                  }

                  event.preventDefault();
                  void handleSidebarStepSelect(step.id);
                }}
              >
                <span>{step.title}</span>
                <small>{step.subtitle}</small>
              </li>
            ))}
          </ol>
        </aside>

        <div className="setup-main">
          <header className="masthead">
            <div>
              <h1 id="app-title">{currentStep.title}</h1>
              <p className="intro">{currentStep.subtitle}</p>
            </div>
            <span className="status-pill">Kreator konfiguracji</span>
          </header>

          <section className="setup-page">
            {currentStep.id === 'welcome' ? <WelcomeStep stepNumber={stepIndex + 1} /> : null}
            {currentStep.id === 'gaming-display' ? (
              <DisplaysStep
                stepNumber={resolvedFlowPosition + 1}
                displays={displays}
                pcGamingProfiles={pcGamingProfiles}
                selectedProfileName={selectedPcGamingBehaviourProfileName}
                prefillFromSelectedProfile={!isCreatingProfileFlow}
                onDraftChange={handleDisplayDraftChange}
                validationState={displayValidationState}
                onValidationStateChange={handleDisplayValidationStateChange}
                isRefreshingDisplays={isRefreshingDisplays}
                displayListError={displayListError}
                refreshDisplays={refreshDisplays}
              />
            ) : null}
            {currentStep.id === 'launcher' ? (
              <LauncherStep
                stepNumber={resolvedFlowPosition + 1}
                pcGamingProfiles={pcGamingProfiles}
                selectedProfileName={selectedPcGamingBehaviourProfileName}
                launcher={launcher}
                setLauncher={(nextLauncher) => {
                  const profileName = selectedPcGamingBehaviourProfileName;
                  if (!profileName) {
                    setLauncher(nextLauncher);
                    markUnsavedChanges();
                    return;
                  }

                  setProfileLauncher(profileName, nextLauncher);
                }}
              />
            ) : null}
            {currentStep.id === 'console-behaviour' ? (
              <ConsoleBehaviourStep
                stepNumber={resolvedFlowPosition + 1}
                pcGamingProfiles={pcGamingProfiles}
                selectedProfileName={selectedPcGamingBehaviourProfileName}
                pcGamingBehaviour={pcGamingBehaviour}
                onChangeBehaviourFlag={updatePcGamingBehaviourFlag}
              />
            ) : null}
            {currentStep.id === 'work-behaviour' ? (
              <WorkBehaviourStep
                stepNumber={resolvedFlowPosition + 1}
                behaviour={workBehaviour}
                setBehaviour={(behaviour) => {
                  setWorkBehaviour(behaviour);
                  markUnsavedChanges();
                }}
              />
            ) : null}
            {currentStep.id === 'work-shortcuts' ? (
              <ControllerStep
                stepNumber={resolvedFlowPosition + 1}
                pcGamingProfiles={[]}
                selectedProfileName="Work Mode"
                isWorkMode
                hotkeys={hotkeys}
                hotkeyValidationError={profileHotkeyValidationError}
                controllerShortcut={workModeControllerShortcut}
                controllerValidationError={profileControllerValidationError}
                controllerStatus={controllerShortcutStatus}
                captureSuccessButtons={controllerCaptureSuccessButtons}
                controllerNotice={controllerNotice}
                onStartControllerCapture={() => { void startControllerCapture(workModeControllerCaptureId); }}
                onCancelControllerCapture={() => {
                  if (controllerCaptureCompletionTimerRef.current) {
                    clearTimeout(controllerCaptureCompletionTimerRef.current);
                    controllerCaptureCompletionTimerRef.current = null;
                  }
                  setControllerCaptureSuccessButtons(null);
                  void sendBridgeCommand('cancelControllerShortcutCapture', { reason: 'UserCancelled' })
                    .then(() => sendBridgeCommand<ControllerStatusInfo>('getControllerStatus'))
                    .then((nextStatus) => applyControllerStatus(nextStatus))
                    .catch(() => setError('Nie udało się anulować nasłuchiwania kontrolera.'));
                }}
                onRemoveControllerShortcut={() => updateWorkModeControllerShortcut([])}
                onRemoveHotkey={() => updateWorkModeHotkey('')}
                onChangeHotkey={(_, value) => updateWorkModeHotkey(value)}
              />
            ) : null}
            {currentStep.id === 'controller' ? (
              <ControllerStep
                stepNumber={resolvedFlowPosition + 1}
                pcGamingProfiles={pcGamingProfiles}
                selectedProfileName={selectedPcGamingBehaviourProfileName}
                hotkeys={hotkeys}
                hotkeyValidationError={profileHotkeyValidationError}
                controllerShortcut={pcGamingProfileControllerShortcuts[selectedPcGamingBehaviourProfileName] ?? null}
                controllerValidationError={profileControllerValidationError}
                controllerStatus={controllerShortcutStatus}
                captureSuccessButtons={controllerCaptureSuccessButtons}
                controllerNotice={controllerNotice}
                onStartControllerCapture={() => { void startControllerCapture(selectedPcGamingBehaviourProfileName); }}
                onCancelControllerCapture={() => {
                  if (controllerCaptureCompletionTimerRef.current) {
                    clearTimeout(controllerCaptureCompletionTimerRef.current);
                    controllerCaptureCompletionTimerRef.current = null;
                  }
                  setControllerCaptureSuccessButtons(null);
                  void sendBridgeCommand('cancelControllerShortcutCapture', { reason: 'UserCancelled' })
                    .then(() => sendBridgeCommand<ControllerStatusInfo>('getControllerStatus'))
                    .then((nextStatus) => applyControllerStatus(nextStatus))
                    .catch(() => {
                      setError('Nie udało się anulować nasłuchiwania kontrolera.');
                    });
                }}
                  onRemoveControllerShortcut={() => updateProfileControllerShortcut(selectedPcGamingBehaviourProfileName, [])}
                onRemoveHotkey={() => updateProfileHotkey(selectedPcGamingBehaviourProfileName, 'enterProfile', '')}
                onChangeHotkey={(key, value) => {
                  const profileName = selectedPcGamingBehaviourProfileName;
                  if (!profileName) {
                    setHotkeys((current) => ({
                      ...current,
                      pcGamingMode: key === 'enterProfile' ? value : current.pcGamingMode
                    }));
                    return;
                  }

                  if (key === 'enterProfile') {
                    updateProfileHotkey(profileName, key, value);
                  }
                }}
              />
            ) : null}
            {currentStep.id === 'app-behaviour' ? (
              <AppBehaviourStep
                stepNumber={resolvedFlowPosition + 1}
                finishOptions={finishOptions}
                setFinishOptions={(options) => {
                  setFinishOptions(options);
                  markUnsavedChanges();
                }}
              />
            ) : null}
            {currentStep.id === 'finish' ? (
              <FinishStep
                stepNumber={resolvedFlowPosition + 1}
                launcher={launcher}
                pcGamingLayout={pcGamingLayout}
                pcGamingProfileLaunchers={pcGamingProfileLaunchers}
                pcGamingProfileHotkeys={pcGamingProfileHotkeys}
                activeProfileName={activePcGamingProfileName}
              />
            ) : null}
          </section>

          <footer className="setup-footer">
            {isNonFirstRunEditingSession ? (
              <>
                <div className="footer-actions-left">
                  <button className="secondary-button" type="button" disabled={isSaving} onClick={() => requestNavigation('returnToDashboard')}>
                    Panel główny
                  </button>
                </div>
                <p className={error || status.lastError ? 'error-line' : 'operation-line'}>{footerMessage}</p>
                <div className="footer-actions-right">
                  <button className="primary-button" type="button" disabled={isSaving} onClick={saveAndReturnToDashboard}>
                    {isSaving ? 'Zapisywanie...' : 'Zapisz zmiany'}
                  </button>
                </div>
              </>
            ) : isEditingSection ? (
              <>
                <div className="footer-actions-left">
                  <button className="secondary-button" type="button" disabled={!canGoBack || isSaving} onClick={() => requestNavigation('goBack')}>
                    Wróć
                  </button>
                  <button className="secondary-button" type="button" disabled={isSaving} onClick={() => requestNavigation('returnToDashboard')}>
                    Panel główny
                  </button>
                </div>
                <p className={error || status.lastError ? 'error-line' : 'operation-line'}>{footerMessage}</p>
                <div className="footer-actions-right">
                  <button className="secondary-button" type="button" disabled={isSaving || isLastStep} onClick={() => requestNavigation('goNext')}>
                    Dalej
                  </button>
                  <button className="primary-button" type="button" disabled={isSaving} onClick={saveAndReturnToDashboard}>
                    {isSaving ? 'Zapisywanie...' : 'Zapisz zmiany'}
                  </button>
                </div>
              </>
            ) : (
              <>
                <div className="footer-actions-left">
                  <button className="secondary-button" type="button" disabled={!canGoBack} onClick={() => requestNavigation('goBack')}>
                    Wróć
                  </button>
                  {isCreatingProfileFlow ? (
                    <button className="secondary-button" type="button" disabled={isSaving} onClick={() => requestNavigation('returnToDashboard')}>
                      Panel główny
                    </button>
                  ) : null}
                </div>
                <p className={error || status.lastError ? 'error-line' : 'operation-line'}>{footerMessage}</p>
                <div className="footer-actions-right">
                  <button className="primary-button" type="button" disabled={isSaving} onClick={() => requestNavigation('goNext')}>
                    {isSaving ? 'Zapisywanie...' : isLastStep ? 'Zakończ i wróć do panelu' : 'Dalej'}
                  </button>
                </div>
              </>
            )}
          </footer>

          {showOverwriteProfileDialog ? (
            <div className="dialog-backdrop" role="presentation">
              <div className="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="overwrite-profile-dialog-title">
                <h3 id="overwrite-profile-dialog-title">Nadpisanie profilu</h3>
                <p>Profil o tej nazwie już istnieje. Czy na pewno chcesz go nadpisać?</p>
                <div className="dialog-actions">
                  <button
                    className="secondary-button"
                    type="button"
                    onClick={() => {
                      if (pendingOverwriteProfileAction === 'save-then-continue') {
                        setPendingNavigationAction(null);
                      }
                      setShowOverwriteProfileDialog(false);
                      setPendingOverwriteProfileAction(null);
                    }}
                  >
                    Anuluj
                  </button>
                  <button className="primary-button" type="button" onClick={() => void handleOverwriteProfileDialogConfirm()}>
                    Nadpisz
                  </button>
                </div>
              </div>
            </div>
          ) : null}

          {showUnsavedDialog ? (
            <div className="dialog-backdrop" role="presentation">
              <div className="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="unsaved-dialog-title">
                <h3 id="unsaved-dialog-title">Niezapisane zmiany</h3>
                <p>Masz niezapisane zmiany w profilach. Chcesz je zapisać czy odrzucić?</p>
                <div className="dialog-actions">
                  <button className="secondary-button" type="button" onClick={() => setShowUnsavedDialog(false)}>
                    Anuluj
                  </button>
                  <button className="danger-button secondary-button" type="button" onClick={() => void handleUnsavedDialogDiscard()}>
                    Odrzuć
                  </button>
                  <button className="primary-button" type="button" onClick={() => void handleUnsavedDialogSave()}>
                    Zapisz
                  </button>
                </div>
              </div>
            </div>
          ) : null}
        </div>
      </section>
    </main>
  );
}

function LoadingScreen() {
  return (
    <main className="app-shell">
      <section className="setup-window compact-window" aria-labelledby="app-title">
        <div className="setup-main">
          <header className="masthead">
            <div>
              <h1 id="app-title">GameShell</h1>
              <p className="intro">Wczytywanie konfiguracji...</p>
            </div>
            <span className="status-pill">Start</span>
          </header>
        </div>
      </section>
    </main>
  );
}

function Dashboard({
  status,
  launcher,
  pcGamingLayout,
  pcGamingProfiles,
  pcGamingProfileLaunchers,
  pcGamingProfileHotkeys,
  activeProfileName,
  selectedProfileName,
  globalWorkModeHotkey,
  isModeSwitching,
  isRemovingProfile,
  activeModeAction,
  message,
  hasError,
  runModeCommand,
  deleteSelectedProfile,
  selectProfile,
  createProfile,
  editSettings
}: {
  status: AppStatus;
  launcher: LauncherType;
  pcGamingLayout: DisplayLayoutSnapshot | null;
  pcGamingProfiles: DisplayLayoutProfile[];
  pcGamingProfileLaunchers: PcGamingProfileLauncherMap;
  pcGamingProfileHotkeys: PcGamingProfileHotkeysMap;
  activeProfileName: string | null;
  selectedProfileName: string;
  globalWorkModeHotkey: string;
  isModeSwitching: boolean;
  isRemovingProfile: boolean;
  activeModeAction: string | null;
  message: string;
  hasError: boolean;
  runModeCommand: (command: ModeCommand, saveBeforeRun?: boolean, payload?: unknown) => Promise<void>;
  deleteSelectedProfile: () => Promise<void>;
  selectProfile: (profileName: string) => void;
  createProfile: () => void;
  editSettings: (stepId: StepId) => void;
}) {
  const [showDeleteProfileDialog, setShowDeleteProfileDialog] = useState(false);
  const isWorkModeActive = status.currentMode === 'Work';
  const isPcGamingActive = status.currentMode === 'PcGaming';
  const isConsoleModeActive = status.currentMode === 'Console';
  const activeGamingProfileLayout = activeProfileName
    ? pcGamingProfiles.find((profile) => profile.name === activeProfileName)?.layout ?? null
    : null;
  const resolvedGamingLayout = activeGamingProfileLayout ?? pcGamingLayout;
  const visibleLayoutSummary = isWorkModeActive
    ? 'Układ roboczy'
    : isPcGamingActive
      ? resolvedGamingLayout
        ? summarizeLayout(resolvedGamingLayout)
        : 'Brak zapisanego profilu'
      : isConsoleModeActive
        ? status.selectedConsoleDisplay ?? 'Brak wybranego ekranu Console Mode'
        : pcGamingLayout
          ? summarizeLayout(pcGamingLayout)
          : 'Brak zapisanego profilu';
  const shouldShowLauncherFact = !isWorkModeActive;
  const activeProfileLauncher = activeProfileName ? pcGamingProfileLaunchers[activeProfileName] : null;
  const visibleLauncher = activeProfileLauncher ?? launcher;
  const selectedProfile = pcGamingProfiles.find((profile) => profile.name === selectedProfileName) ?? null;
  const canRunSelectedProfile = Boolean(selectedProfileName) && !isModeSwitching && !isRemovingProfile;
  const canDeleteSelectedProfile = Boolean(selectedProfileName) && !isModeSwitching && !isRemovingProfile;
  const profileCountLabel = pcGamingProfiles.length === 1
    ? 'profil'
    : pcGamingProfiles.length >= 2 && pcGamingProfiles.length <= 4
      ? 'profile'
      : 'profili';

  return (
    <main className="app-shell">
      <section className="setup-window" aria-labelledby="app-title">
        <aside className="brand-panel dashboard-sidebar" aria-label="GameShell">
          <header className="brand-lockup sidebar-brand">
            <img className="brand-mark" src="/assets/GameShell.png" alt="" aria-hidden="true" />
            <div>
              <p>GameShell</p>
              <small>lokalne profile gamingowe</small>
            </div>
          </header>

          <section className="sidebar-state-block" aria-label="Stan GameShell">
            <div className="sidebar-section-label">Stan</div>
            {isWorkModeActive ? (
              <div className="sidebar-state-content">
                <div className="sidebar-state-line">
                  <strong>Work Mode</strong>
                  <span className="work-return-status">AKTYWNY</span>
                </div>
                <p>Układ roboczy</p>
                <small>{globalWorkModeHotkey || 'Hotkey: brak'}</small>
              </div>
            ) : (
              <div className="sidebar-state-content">
                <div className="sidebar-state-line">
                  <strong>{formatCurrentMode(status)}</strong>
                  {isPcGamingActive ? <span className="work-return-status">AKTYWNY</span> : null}
                </div>
                {isPcGamingActive ? <p>Profil: {activeProfileName || 'Nie wybrano'}</p> : null}
                <p className="sidebar-state-detail">{visibleLayoutSummary}</p>
                {shouldShowLauncherFact ? <small>{visibleLauncher}</small> : null}
                {!isConsoleModeActive && !isPcGamingActive ? <small>{globalWorkModeHotkey || 'Hotkey: brak'}</small> : null}
                {!isWorkModeActive ? (
                  <button
                    className="sidebar-state-action"
                    type="button"
                    disabled={isModeSwitching}
                    onClick={() => runModeCommand('enterWorkMode')}
                  >
                    Wróć do Work Mode
                  </button>
                ) : null}
              </div>
            )}
          </section>

          <section className="sidebar-global-settings" aria-label="Globalne ustawienia">
            <div className="sidebar-section-label">Globalne</div>
            <div className="sidebar-global-links">
              <button type="button" onClick={() => editSettings('work-behaviour')}>
                <span>Zachowanie Work Mode</span>
              </button>
              <button type="button" onClick={() => editSettings('work-shortcuts')}>
                <span>Skróty Work Mode</span>
              </button>
              <button type="button" onClick={() => editSettings('app-behaviour')}>
                <span>Uruchamianie aplikacji</span>
              </button>
            </div>
          </section>
        </aside>

        <div className="setup-main">
          <header className="masthead">
            <div>
              <h1 id="app-title">GameShell</h1>
              <p className="intro">Wybierz profil gamingowy albo dostosuj konfigurację GameShell.</p>
            </div>
          </header>

          <section className="setup-page">
            <div className="dashboard-layout">
              <section className="dashboard-band dashboard-mode-band" aria-labelledby="dashboard-modes-title">
                <section className="quick-switch-panel profile-module" aria-label="Zapisane profile ekranów">
                  <div className="profile-module-header">
                    <div className="profile-module-title-wrap">
                      <h2 id="dashboard-modes-title" className="profile-module-title">Zapisane profile ekranów</h2>
                    </div>
                    <div className="profile-module-header-actions">
                      <small className="profile-module-count">
                        {pcGamingProfiles.length} {profileCountLabel}
                      </small>
                      <button
                        className="secondary-button compact add-profile-head-button"
                        type="button"
                        onClick={createProfile}
                        disabled={isRemovingProfile}
                      >
                        + Dodaj profil
                      </button>
                    </div>
                  </div>

                  <div className="profile-list-shell">
                    <div className="profile-quick-list" role="listbox" aria-label="Lista profili PC Gaming">
                      {pcGamingProfiles.map((profile) => {
                        const profileLauncher = pcGamingProfileLaunchers[profile.name] ?? launcher;
                        const isProfileConfiguredAsActive = activeProfileName === profile.name;
                        const isProfileSelectedForEditing = selectedProfileName === profile.name;
                        const isPcGamingNow = status.currentMode === 'PcGaming';
                        const stateLabel = isPcGamingNow && isProfileConfiguredAsActive
                          ? 'AKTYWNY'
                          : isProfileSelectedForEditing
                            ? 'ZAZNACZONY'
                            : 'GOTOWY';

                        return (
                          <button
                            className={`profile-quick-row ${isProfileSelectedForEditing ? 'selected' : ''} ${isPcGamingNow && isProfileConfiguredAsActive ? 'active' : ''}`}
                            type="button"
                            key={profile.name}
                            disabled={isModeSwitching || isRemovingProfile}
                            onClick={() => selectProfile(profile.name)}
                            role="option"
                            aria-selected={isProfileSelectedForEditing}
                          >
                            <div className="profile-quick-main">
                              <strong>{profile.name}</strong>
                              <small>{summarizeLayout(profile.layout)} · {profileLauncher}</small>
                            </div>
                            <div className="profile-quick-meta">
                              <span className="profile-quick-state">{stateLabel}</span>
                            </div>
                          </button>
                        );
                      })}
                    </div>
                  </div>

                  <div className="profile-actions profile-module-footer">
                    <button
                      className="danger-button secondary-button compact delete-action"
                      type="button"
                      disabled={!canDeleteSelectedProfile}
                      onClick={() => setShowDeleteProfileDialog(true)}
                    >
                      Usuń profil
                    </button>
                    <button
                      className="primary-button launch-profile-button launch-action"
                      type="button"
                      disabled={!canRunSelectedProfile}
                      onClick={() => runModeCommand('enterGameShellWithProfile', false, { profileName: selectedProfileName })}
                    >
                      <span>Uruchom profil{selectedProfile ? `: ${selectedProfile.name}` : ''}</span>
                    </button>
                  </div>
                </section>
                {pcGamingProfiles.length === 0 ? <p className="hint-line">Brak zapisanych profili. Dodaj profil w sekcji Ekrany.</p> : null}
              </section>

              <section className="dashboard-band dashboard-config-section" aria-label="Konfiguracja profilu">
                <div className="dashboard-shortcut-groups" aria-label="Edycja konfiguracji">
                  <section className="dashboard-shortcut-group" aria-labelledby="profile-settings-title">
                    <div className="profile-settings-heading">
                      <h3 id="profile-settings-title">KONFIGURACJA PROFILU</h3>
                      <span className="profile-context">{selectedProfileName || 'PC'}</span>
                    </div>
                    <div className="section-shortcuts">
                      <button type="button" onClick={() => editSettings('gaming-display')}>
                        <span>Ekrany</span>
                        <small>Tworzenie i wybór układów ekranów</small>
                      </button>
                      <button type="button" onClick={() => editSettings('launcher')}>
                        <span>Launcher profilu</span>
                        <small>Konfiguracja launchera dla każdego profilu</small>
                      </button>
                      <button type="button" onClick={() => editSettings('console-behaviour')}>
                        <span>Zachowanie profilu</span>
                        <small>Ikony, taskbar, okna i kursor per profil</small>
                      </button>
                      <button type="button" onClick={() => editSettings('controller')}>
                        <span>Skróty profilu</span>
                        <small>Hotkey wejścia dla wybranego profilu</small>
                      </button>
                    </div>
                  </section>
                </div>
              </section>
            </div>
          </section>

          <footer className="setup-footer">
            <p className={hasError ? 'error-line' : 'operation-line'}>{message}</p>
          </footer>

          {showDeleteProfileDialog ? (
            <div className="dialog-backdrop" role="presentation">
              <div className="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="delete-profile-dialog-title">
                <h3 id="delete-profile-dialog-title">Usuwanie profilu</h3>
                <p>
                  Czy na pewno chcesz usunąć profil
                  {selectedProfile ? ` „${selectedProfile.name}”` : ''}?
                </p>
                <div className="dialog-actions">
                  <button
                    className="secondary-button"
                    type="button"
                    disabled={isRemovingProfile}
                    onClick={() => setShowDeleteProfileDialog(false)}
                  >
                    Anuluj
                  </button>
                  <button
                    className="danger-button secondary-button"
                    type="button"
                    disabled={isRemovingProfile}
                    onClick={() => {
                      void (async () => {
                        await deleteSelectedProfile();
                        setShowDeleteProfileDialog(false);
                      })();
                    }}
                  >
                    {isRemovingProfile ? 'Usuwanie...' : 'Usuń profil'}
                  </button>
                </div>
              </div>
            </div>
          ) : null}
        </div>
      </section>
    </main>
  );
}

function WelcomeStep({ stepNumber }: { stepNumber: number }) {
  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Skonfiguruj GameShell na tym komputerze</h2>
      <p>
        Ten kreator pomoże Ci ustawić profile ekranów, launchery i zachowanie trybów.
        Po zakończeniu szybko przełączysz komputer między graniem a pracą.
      </p>
    </div>
  );
}

function DisplaysStep({
  stepNumber,
  displays,
  pcGamingProfiles,
  selectedProfileName,
  prefillFromSelectedProfile,
  onDraftChange,
  validationState,
  onValidationStateChange,
  isRefreshingDisplays,
  displayListError,
  refreshDisplays
}: {
  stepNumber: number;
  displays: DisplayDeviceInfo[];
  pcGamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  prefillFromSelectedProfile: boolean;
  onDraftChange: (profileName: string, identities: DisplayIdentity[]) => void;
  validationState: DisplayValidationState;
  onValidationStateChange: (state: DisplayValidationState) => void;
  isRefreshingDisplays: boolean;
  displayListError: string | null;
  refreshDisplays: () => Promise<void>;
}) {
  const [newProfileName, setNewProfileName] = useState('');
  const [selectedDisplays, setSelectedDisplays] = useState<DisplayIdentity[]>([]);
  const [displayLimitMessage, setDisplayLimitMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!prefillFromSelectedProfile || !selectedProfileName) {
      return;
    }

    const selectedProfile = pcGamingProfiles.find((profile) => profile.name === selectedProfileName) ?? null;
    if (!selectedProfile) {
      return;
    }

    const profileIdentities = extractDisplayIdentities(selectedProfile.layout);
    setNewProfileName(selectedProfile.name);
    setSelectedDisplays(profileIdentities);
    setDisplayLimitMessage(null);
    onValidationStateChange({
      showNameError: false,
      showSelectionError: false
    });
    onDraftChange(selectedProfile.name, profileIdentities);
  }, [onValidationStateChange, onDraftChange, pcGamingProfiles, prefillFromSelectedProfile, selectedProfileName]);

  const canEditSelection = Boolean(newProfileName.trim())
    && selectedDisplays.length > 0;

  function toggleSelectedDisplay(identity: DisplayIdentity) {
    setSelectedDisplays((current) => {
      const alreadySelected = current.some((selected) => sameIdentity(selected, identity));

      if (!alreadySelected && current.length >= MAX_PROFILE_DISPLAYS) {
        setDisplayLimitMessage(PROFILE_DISPLAY_LIMIT_MESSAGE);
        return current;
      }

      const next = alreadySelected
        ? current.filter((selected) => !sameIdentity(selected, identity))
        : [...current, identity];

      if (next.length <= MAX_PROFILE_DISPLAYS) {
        setDisplayLimitMessage(null);
      }

      if (next.length > 0 && validationState.showSelectionError) {
        onValidationStateChange({
          ...validationState,
          showSelectionError: false
        });
      }

      onDraftChange(newProfileName, next);
      return next;
    });
  }

  return (
    <div className="wizard-copy displays-step">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Ekrany</h2>
      <section className="settings-section">
        <p>Wybierz ekrany należące do profilu.</p>
        <DisplayDiagnosticsPanel
          displays={displays}
          selectedDisplays={selectedDisplays}
          onToggleDisplay={toggleSelectedDisplay}
          isRefreshing={isRefreshingDisplays}
          error={displayListError}
          refreshDisplays={refreshDisplays}
          profileName={newProfileName}
          setProfileName={(nextName) => {
            setNewProfileName(nextName);

            if (nextName.trim().length > 0 && validationState.showNameError) {
              onValidationStateChange({
                ...validationState,
                showNameError: false
              });
            }

            onDraftChange(nextName, selectedDisplays);
          }}
          validationState={validationState}
          onValidationStateChange={onValidationStateChange}
          canEditSelection={canEditSelection}
          displayLimitMessage={displayLimitMessage}
        />
      </section>
    </div>
  );
}

function DisplayDiagnosticsPanel({
  displays,
  selectedDisplays,
  onToggleDisplay,
  isRefreshing,
  error,
  refreshDisplays,
  profileName,
  setProfileName,
  validationState,
  onValidationStateChange,
  canEditSelection,
  displayLimitMessage
}: {
  displays: DisplayDeviceInfo[];
  selectedDisplays: DisplayIdentity[];
  onToggleDisplay: (identity: DisplayIdentity) => void;
  isRefreshing: boolean;
  error: string | null;
  refreshDisplays: () => Promise<void>;
  profileName: string;
  setProfileName: (value: string) => void;
  validationState: DisplayValidationState;
  onValidationStateChange: (state: DisplayValidationState) => void;
  canEditSelection: boolean;
  displayLimitMessage: string | null;
}) {
  const [limitFeedbackKey, setLimitFeedbackKey] = useState<string | null>(null);
  const showNameError = validationState.showNameError && profileName.trim().length === 0;
  const showSelectionError = validationState.showSelectionError && selectedDisplays.length === 0;

  function triggerLimitFeedback(display: DisplayDeviceInfo) {
    const feedbackKey = display.id || `${display.adapterLuid}:${display.targetId}`;
    setLimitFeedbackKey(feedbackKey);
    window.setTimeout(() => {
      setLimitFeedbackKey((current) => (current === feedbackKey ? null : current));
    }, 320);
  }

  function handleDisplayToggle(display: DisplayDeviceInfo) {
    const isSelected = selectedDisplays.some((selectedDisplay) => sameIdentity(selectedDisplay, display.identity));
    const wouldExceedLimit = !isSelected && selectedDisplays.length >= MAX_PROFILE_DISPLAYS;

    if (wouldExceedLimit) {
      triggerLimitFeedback(display);
    }

    onToggleDisplay(display.identity);
  }

  return (
    <section className="display-diagnostics display-module" aria-label="Dostępne ekrany">
      <div className="display-diagnostics-head display-module-header">
        <div>
          <p>Lista ekranów wykrytych przez Windows, także tych chwilowo wyłączonych.</p>
        </div>
        <div className="display-diagnostics-actions">
          <button className="secondary-button compact" type="button" disabled={isRefreshing} onClick={refreshDisplays}>
            {isRefreshing ? 'Odświeżanie...' : 'Odśwież listę ekranów'}
          </button>
        </div>
      </div>
      <div className="display-module-body">
        <div className="profile-name-section">
          <div className="profile-name-header">
            <label className="profile-name-label" htmlFor="profile-name-input">Nazwa profilu</label>
            <span className="selection-count" aria-live="polite">
              Wybrano <strong>{selectedDisplays.length}/{MAX_PROFILE_DISPLAYS}</strong>
            </span>
          </div>
          <input
            id="profile-name-input"
            className="text-input profile-name-input"
            type="text"
            value={profileName}
            onChange={(event) => setProfileName(event.target.value)}
            onBlur={() => {
              const shouldShowNameError = profileName.trim().length === 0;
              onValidationStateChange({
                ...validationState,
                showNameError: shouldShowNameError
              });
            }}
            aria-invalid={showNameError}
            aria-describedby={showNameError ? 'profile-name-validation-error' : undefined}
            placeholder="np. Salon TV + biurko"
          />
        </div>
        {showNameError ? (
          <div id="profile-name-validation-error" className="field-validation-error" role="alert">
            Podaj nazwę profilu.
          </div>
        ) : null}
        {displayLimitMessage ? (
          <div className="inline-notice inline-notice-warning" role="alert" aria-live="assertive">
            <span className="inline-notice-label">LIMIT</span>
            <div className="inline-notice-copy">
              <strong>{displayLimitMessage}</strong>
              <span>Obsługa większej liczby ekranów zostanie dodana później.</span>
            </div>
          </div>
        ) : null}
        {showSelectionError ? (
          <div className="selection-validation-error" role="alert">
            Wybierz co najmniej jeden ekran.
          </div>
        ) : null}
        {error ? <p className="error-line display-error">{error}</p> : null}
        {!error && displays.length === 0 ? <p className="hint-line">Nie wykryto ekranów.</p> : null}
        {displays.length > 0 ? (
          <div className="display-grid">
            {displays.map((display) => {
              const isSelected = selectedDisplays.some((selectedDisplay) => sameIdentity(selectedDisplay, display.identity));
              const feedbackKey = display.id || `${display.adapterLuid}:${display.targetId}`;
              const displayClasses = [
                'display-card',
                display.isEnabled ? 'is-available' : 'is-inactive',
                isSelected ? 'is-selected' : '',
                limitFeedbackKey === feedbackKey ? 'limit-hit' : ''
              ].filter(Boolean).join(' ');

              return (
                <article
                  className={displayClasses}
                  key={feedbackKey}
                  role="button"
                  tabIndex={0}
                  aria-pressed={isSelected}
                  aria-label={`Ekran ${display.name || 'Nieznany ekran'}`}
                  onClick={() => handleDisplayToggle(display)}
                  onKeyDown={(event) => {
                    if (event.key !== 'Enter' && event.key !== ' ') {
                      return;
                    }

                    event.preventDefault();
                    handleDisplayToggle(display);
                  }}
                >
                  <div className="display-card-title-line">
                    <strong>{display.name || 'Nieznany ekran'}</strong>
                    <div className="display-card-badges">
                      {display.isEnabled && display.isPrimary ? <span className="display-state-badge">GŁÓWNY</span> : null}
                      {!display.isEnabled ? <span className="display-state-badge inactive">NIEAKTYWNY</span> : null}
                    </div>
                  </div>
                  <span className="display-tech-line">
                    {formatDisplayResolution(display)} · {formatDisplayRefresh(display)}
                  </span>
                  {display.connectionTechnology ? <small>{display.connectionTechnology}</small> : null}
                  <small>Pozycja: {display.positionX}, {display.positionY}</small>
                </article>
              );
            })}
          </div>
        ) : null}
      </div>
    </section>
  );
}

function LauncherStep({
  stepNumber,
  pcGamingProfiles,
  selectedProfileName,
  launcher,
  setLauncher
}: {
  stepNumber: number;
  pcGamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  launcher: LauncherType;
  setLauncher: (launcher: LauncherType) => void;
}) {
  const hasProfiles = pcGamingProfiles.length > 0;
  const selectedProfileLabel = selectedProfileName || 'Brak zaznaczonego profilu';

  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Launcher profilu</h2>
      <p>Każdy profil ekranów może używać innego launchera. Zmiana dotyczy aktualnie zaznaczonego profilu.</p>
      <div className="field-grid">
        <div className="profile-target-line" aria-live="polite">
          <span>Edytowany profil</span>
          <strong>{selectedProfileLabel}</strong>
        </div>
      </div>
      <div className="launcher-grid" aria-label="Launcher" aria-disabled={!hasProfiles}>
        {launcherOptions.map((item) => (
          <button
            className={`launcher-card ${launcher === item.value ? 'selected' : ''}`}
            key={item.value}
            type="button"
            onClick={() => setLauncher(item.value)}
            disabled={!hasProfiles}
          >
            <span className="launcher-badge">{item.badge}</span>
            <strong>{item.title}</strong>
            <small>{item.description}</small>
          </button>
        ))}
      </div>
    </div>
  );
}

function ConsoleBehaviourStep({
  stepNumber,
  pcGamingProfiles,
  selectedProfileName,
  pcGamingBehaviour,
  onChangeBehaviourFlag
}: {
  stepNumber: number;
  pcGamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  pcGamingBehaviour: PcGamingBehaviourState;
  onChangeBehaviourFlag: (key: PcGamingBehaviourKey, value: boolean) => void;
}) {
  const hasProfiles = pcGamingProfiles.length > 0;
  const selectedProfileLabel = selectedProfileName || 'Brak zaznaczonego profilu';

  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Zachowanie Gaming Mode</h2>
      <section className="settings-section gaming-behaviour-section">
        <h3>Gaming Mode</h3>
        <p>Te opcje określają, co ma się stać po uruchomieniu wybranego profilu.</p>
        <div className="field-grid">
          <div className="profile-target-line" aria-live="polite">
            <span>Edytowany profil</span>
            <strong>{selectedProfileLabel}</strong>
          </div>
        </div>
        <div className="settings-group">
          {pcGamingBehaviourOptions.map((option) => (
            <SettingRow
              key={option.key}
              title={option.label}
              checked={pcGamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
              disabled={!hasProfiles}
            />
          ))}
        </div>
      </section>
    </div>
  );
}

function WorkBehaviourStep({
  stepNumber,
  behaviour,
  setBehaviour
}: {
  stepNumber: number;
  behaviour: typeof defaultWorkBehaviour;
  setBehaviour: (behaviour: typeof defaultWorkBehaviour) => void;
}) {
  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Zachowanie Work Mode</h2>
      <p>Work Mode przywraca układ roboczy oraz standardowy wygląd pulpitu.</p>
      <div className="settings-group">
        <SettingRow title="Zamknij launcher" checked={behaviour.closeLauncher} onChange={(value) => setBehaviour({ ...behaviour, closeLauncher: value })} />
        <SettingRow title="Pokaż paski zadań" checked={behaviour.showTaskbars} onChange={(value) => setBehaviour({ ...behaviour, showTaskbars: value })} />
        <SettingRow title="Pokaż ikony pulpitu" checked={behaviour.showDesktopIcons} onChange={(value) => setBehaviour({ ...behaviour, showDesktopIcons: value })} />
        <SettingRow title="Nie minimalizuj istniejących okien" checked={behaviour.doNotMinimizeWindows} onChange={(value) => setBehaviour({ ...behaviour, doNotMinimizeWindows: value })} />
      </div>
    </div>
  );
}

function ControllerStep({
  stepNumber,
  pcGamingProfiles,
  selectedProfileName,
  isWorkMode = false,
  hotkeys,
  hotkeyValidationError,
  controllerShortcut,
  controllerValidationError,
  controllerStatus,
  captureSuccessButtons,
  controllerNotice,
  onStartControllerCapture,
  onCancelControllerCapture,
  onRemoveControllerShortcut,
  onRemoveHotkey,
  onChangeHotkey
}: {
  stepNumber: number;
  pcGamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  isWorkMode?: boolean;
  hotkeys: HotkeySettingsState;
  hotkeyValidationError: string | null;
  controllerShortcut: ControllerShortcutSettings | null;
  controllerValidationError: string | null;
  controllerStatus: ControllerStatusInfo;
  captureSuccessButtons: string[] | null;
  controllerNotice: 'missing' | 'disconnected' | null;
  onStartControllerCapture: () => void;
  onCancelControllerCapture: () => void;
  onRemoveControllerShortcut: () => void;
  onRemoveHotkey: () => void;
  onChangeHotkey: (key: 'enterProfile' | 'workMode', value: string) => void;
}) {
  const hasProfiles = pcGamingProfiles.length > 0;
  const selectedProfileLabel = selectedProfileName || 'Brak zaznaczonego profilu';
  const selectedControllerButtons = controllerShortcut?.buttons ?? [];
  const hasControllerShortcut = selectedControllerButtons.length > 0;
  const isCapturePhase = controllerStatus.capturePhase === 'WaitingForRelease'
    || controllerStatus.capturePhase === 'WaitingForFirstPress'
    || controllerStatus.capturePhase === 'Recording'
    || controllerStatus.capturePhase === 'WaitingForCombinationRelease';
  const canCancelCapture = controllerStatus.capturePhase === 'WaitingForRelease'
    || controllerStatus.capturePhase === 'WaitingForFirstPress';
  const isCaptureSuccess = controllerStatus.capturePhase === 'Completed' || captureSuccessButtons !== null;
  const displayControllerButtons = controllerStatus.capturedButtons.length > 0
    ? controllerStatus.capturedButtons
    : selectedControllerButtons;
  const controllerFamily = detectControllerFamily(controllerStatus.deviceName);
  const controllerDeviceLabel = controllerStatus.isConnected
    ? controllerFamily === ControllerFamily.PlayStation
      ? 'PLAYSTATION POŁĄCZONY'
      : controllerFamily === ControllerFamily.Xbox
        ? 'XBOX POŁĄCZONY'
        : controllerFamily === ControllerFamily.Nintendo
          ? 'NINTENDO POŁĄCZONY'
          : 'KONTROLER POŁĄCZONY'
    : 'BRAK KONTROLERA';
  const controllerDeviceDescription = controllerStatus.deviceName || 'Brak wykrytego kontrolera';
  return (
    <div className="wizard-copy profile-shortcuts-content">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>{isWorkMode ? 'Skrót Work Mode' : 'Skróty profilu'}</h2>
      <div className="profile-shortcuts-intro">
        <p>{isWorkMode ? 'Przypisz globalną kombinację przycisków kontrolera do przejścia w Work Mode.' : `Przypisz skrót klawiaturowy lub kombinację przycisków kontrolera do profilu ${selectedProfileLabel}.`}</p>
        <span className="profile-shortcuts-profile-label">{isWorkMode ? 'CEL: WORK MODE' : `PROFIL: ${selectedProfileLabel.toUpperCase()}`}</span>
      </div>

      <section className="shortcut-module keyboard-shortcut-module">
        <header className="shortcut-module-header">
          <div>
            <h3>Skrót klawiaturowy</h3>
            <p>{isWorkMode ? 'Przechodzi w Work Mode z dowolnego miejsca w Windows.' : 'Uruchamia profil z dowolnego miejsca w Windows.'}</p>
          </div>
        </header>
        <div className="shortcut-module-body keyboard-shortcut-body">
          <label className="shortcut-control-label">
            <span>HOTKEY</span>
            <HotkeyField
              value={isWorkMode ? hotkeys.workMode : hotkeys.pcGamingMode}
              onChange={(value) => onChangeHotkey(isWorkMode ? 'workMode' : 'enterProfile', value)}
              placeholder={isWorkMode ? 'np. Ctrl+Alt+W' : 'np. Ctrl+Alt+P'}
              disabled={!isWorkMode && !hasProfiles}
              error={hotkeyValidationError}
            />
            {(isWorkMode ? hotkeys.workMode : hotkeys.pcGamingMode) ? (
              <button className="danger-button secondary-button" type="button" onClick={onRemoveHotkey}>
                Usuń
              </button>
            ) : null}
          </label>
          <span className={`shortcut-saved-status ${(isWorkMode ? hotkeys.workMode : hotkeys.pcGamingMode) ? 'is-saved' : ''}`}>
            {(isWorkMode ? hotkeys.workMode : hotkeys.pcGamingMode) ? 'ZAPISANO' : 'BRAK SKRÓTU'}
          </span>
        </div>
      </section>

      <section className={`shortcut-module controller-shortcut-module ${isCapturePhase || isCaptureSuccess ? 'is-capturing' : ''}`}>
        <header className="shortcut-module-header controller-module-header">
          <div>
            <h3>Skrót kontrolera</h3>
            <p>Przytrzymaj zapisaną kombinację, aby {isWorkMode ? 'przejść w Work Mode' : 'uruchomić ten profil'}.</p>
          </div>
          <span className={`controller-status-pill ${controllerStatus.isConnected ? 'is-connected' : 'is-disconnected'}`} title={controllerDeviceDescription}>
            {controllerDeviceLabel}
          </span>
        </header>
        <div className="shortcut-module-body controller-shortcut-body">
          {controllerNotice ? (
            <div className="inline-notice controller-device-notice" role="alert" aria-live="assertive">
              <span className="inline-notice-label">{controllerNotice === 'missing' ? 'URZĄDZENIE' : 'ROZŁĄCZONO'}</span>
              <div className="inline-notice-copy">
                <strong>
                  {controllerNotice === 'missing'
                    ? 'Nie wykryto kontrolera.'
                    : 'Kontroler został rozłączony.'}
                </strong>
                <span>
                  {controllerNotice === 'missing'
                    ? 'Podłącz kontroler przez USB lub Bluetooth i spróbuj ponownie.'
                    : 'Podłącz go ponownie, aby kontynuować.'}
                </span>
                {controllerNotice === 'missing' ? <span>GameShell wykryje go automatycznie.</span> : null}
              </div>
            </div>
          ) : null}
          {isCapturePhase || isCaptureSuccess ? (
            <div className="controller-capture-panel" aria-live="polite">
              {isCaptureSuccess ? (
                <>
                  <span className="capture-mode-label capture-success-label">ZAPISANO</span>
                  <ControllerKeycaps buttons={captureSuccessButtons ?? controllerStatus.completedCapture?.buttons ?? []} family={controllerFamily} />
                </>
              ) : null}
              {controllerStatus.capturePhase === 'WaitingForRelease' ? (
                <>
                  <span className="capture-mode-label">NASŁUCHIWANIE</span>
                  <p>Puść wszystkie przyciski kontrolera.</p>
                  <small>Nasłuchiwanie rozpocznie się automatycznie.</small>
                </>
              ) : null}
              {controllerStatus.capturePhase === 'WaitingForFirstPress' ? (
                <>
                  <span className="capture-mode-label">NASŁUCHIWANIE</span>
                  <p className="capture-primary-message">Teraz naciśnij i przytrzymaj wybraną kombinację.</p>
                  <small>Możesz naciskać kolejne przyciski z niewielkim odstępem.</small>
                </>
              ) : null}
              {controllerStatus.capturePhase === 'Recording' ? (
                <>
                  <span className="capture-mode-label">NASŁUCHIWANIE</span>
                  <p>Wykryto kombinację</p>
                  <ControllerKeycaps buttons={displayControllerButtons} family={controllerFamily} />
                  <small>Przytrzymaj jeszcze przez chwilę.</small>
                </>
              ) : null}
              {controllerStatus.capturePhase === 'WaitingForCombinationRelease' ? (
                <>
                  <p>Puść kombinację, aby ją zapisać.</p>
                  <ControllerKeycaps buttons={displayControllerButtons} family={controllerFamily} />
                </>
              ) : null}
              {canCancelCapture ? (
                <button className="secondary-button controller-capture-cancel" type="button" onClick={onCancelControllerCapture}>
                  Anuluj
                </button>
              ) : null}
            </div>
          ) : (
            <>
              {controllerStatus.capturePhase === 'Completed' && displayControllerButtons.length > 0 ? (
                <div className="controller-capture-completed" aria-live="polite">
                  <span className="capture-step-label">KOMBINACJA ZAPISANA</span>
                  <ControllerKeycaps buttons={displayControllerButtons} family={controllerFamily} />
                </div>
              ) : hasControllerShortcut ? (
                <div className="controller-assigned-state">
                  <span className="capture-step-label">PRZYPISANA KOMBINACJA</span>
                  <ControllerKeycaps buttons={selectedControllerButtons} family={controllerFamily} />
                  <p>Przytrzymaj przez {((controllerShortcut?.holdDurationMs ?? 700) / 1000).toFixed(1)} s, aby {isWorkMode ? 'przejść w Work Mode' : 'uruchomić profil'}.</p>
                </div>
              ) : (
                <div className="controller-empty-state">
                  <span className="capture-step-label">BRAK PRZYPISANEJ KOMBINACJI</span>
                  <p>Przypisz przyciski, których przytrzymanie {isWorkMode ? 'przełączy w Work Mode' : 'uruchomi profil'}.</p>
                </div>
              )}
              <div className="controller-shortcut-actions">
                <button
                  className="primary-button controller-shortcut-listen-button"
                  type="button"
                  disabled={!isWorkMode && !hasProfiles}
                  onClick={onStartControllerCapture}
                >
                  {hasControllerShortcut ? 'Zmień kombinację' : 'Przypisz kombinację'}
                </button>
                {hasControllerShortcut ? (
                  <button className="danger-button secondary-button controller-shortcut-remove" type="button" onClick={onRemoveControllerShortcut}>
                    Usuń
                  </button>
                ) : null}
              </div>
            </>
          )}
          {controllerValidationError ? <div className="field-validation-error" role="alert">{controllerValidationError}</div> : null}
          <details className="controller-diagnostics">
            <summary>Szczegóły diagnostyczne</summary>
            <dl>
              <div><dt>Kontrolery</dt><dd>{controllerStatus.controllerCount}</dd></div>
              <div><dt>Urządzenie</dt><dd>{controllerDeviceDescription}</dd></div>
            </dl>
          </details>
        </div>
      </section>
    </div>
  );
}

function ControllerKeycaps({ buttons, family }: { buttons: string[]; family: ControllerFamily }) {
  if (buttons.length === 0) {
    return null;
  }

  return (
    <div className="controller-keycaps" aria-label={`Kombinacja: ${formatControllerShortcutButtons(buttons, family)}`}>
      {buttons.map((button) => <span className="controller-keycap" key={button}>{getControllerButtonDisplayName(button, family)}</span>)}
    </div>
  );
}

function AppBehaviourStep({
  stepNumber,
  finishOptions,
  setFinishOptions
}: {
  stepNumber: number;
  finishOptions: { startWithWindows: boolean; startMinimized: boolean; startMenuShortcut: boolean; desktopShortcut: boolean };
  setFinishOptions: (options: { startWithWindows: boolean; startMinimized: boolean; startMenuShortcut: boolean; desktopShortcut: boolean }) => void;
}) {
  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Zachowanie aplikacji</h2>
      <p>Te ustawienia dotyczą całej aplikacji i obowiązują dla wszystkich profili.</p>
      <div className="settings-group">
        <SettingRow
          title="Startuj z Windows"
          checked={finishOptions.startWithWindows}
          onChange={(value) => setFinishOptions({
            ...finishOptions,
            startWithWindows: value,
            startMinimized: value ? finishOptions.startMinimized : false
          })}
        />
        <SettingRow
          title="Startuj zminimalizowany do tray"
          checked={finishOptions.startMinimized}
          disabled={!finishOptions.startWithWindows}
          onChange={(value) => setFinishOptions({ ...finishOptions, startMinimized: value })}
        />
        <SettingRow
          title="Utwórz skrót w menu Start"
          checked={finishOptions.startMenuShortcut}
          onChange={(value) => setFinishOptions({ ...finishOptions, startMenuShortcut: value })}
        />
        <SettingRow
          title="Utwórz skrót na pulpicie"
          checked={finishOptions.desktopShortcut}
          onChange={(value) => setFinishOptions({ ...finishOptions, desktopShortcut: value })}
        />
      </div>
    </div>
  );
}

function FinishStep({
  stepNumber,
  launcher,
  pcGamingLayout,
  pcGamingProfileLaunchers,
  pcGamingProfileHotkeys,
  activeProfileName
}: {
  stepNumber: number;
  launcher: LauncherType;
  pcGamingLayout: DisplayLayoutSnapshot | null;
  pcGamingProfileLaunchers: PcGamingProfileLauncherMap;
  pcGamingProfileHotkeys: PcGamingProfileHotkeysMap;
  activeProfileName: string | null;
}) {
  const activeLauncher = activeProfileName ? pcGamingProfileLaunchers[activeProfileName] ?? launcher : launcher;
  const activeHotkeys = activeProfileName ? pcGamingProfileHotkeys[activeProfileName] ?? defaultProfileHotkeys : defaultProfileHotkeys;

  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Podsumowanie konfiguracji</h2>
      <dl className="summary-list">
        <div>
          <dt>Profil Gaming Mode</dt>
          <dd>{activeProfileName || 'Nie wybrano'}</dd>
        </div>
        <div>
          <dt>Uklad ekranow</dt>
          <dd>{pcGamingLayout ? summarizeLayout(pcGamingLayout) : 'Brak aktywnego profilu'}</dd>
        </div>
        <div>
          <dt>Launcher</dt>
          <dd>{activeLauncher}</dd>
        </div>
        <div>
          <dt>Hotkey profilu</dt>
          <dd>{activeHotkeys.enterProfile || 'Brak przypisanego hotkeya'}</dd>
        </div>
      </dl>
    </div>
  );
}

function GameShellCheckbox({
  checked,
  disabled,
  onChange
}: {
  checked: boolean;
  disabled?: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <>
      <input
        className="setting-checkbox-native"
        type="checkbox"
        checked={checked}
        disabled={disabled}
        onChange={(event) => onChange(event.target.checked)}
        onKeyDown={(event) => {
          if (event.key !== 'Enter') {
            return;
          }

          event.preventDefault();
          onChange(!checked);
        }}
      />
      <span className="setting-checkbox-visual" aria-hidden="true" />
    </>
  );
}

function SettingRow({
  title,
  description,
  checked,
  onChange,
  disabled = false
}: {
  title: string;
  description?: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
}) {
  return (
    <label className={`setting-row ${disabled ? 'is-disabled' : ''}`}>
      <GameShellCheckbox checked={checked} disabled={disabled} onChange={onChange} />
      <span className="setting-row-content">
        <span className="setting-row-title">{title}</span>
        {description ? <span className="setting-row-description">{description}</span> : null}
      </span>
    </label>
  );
}

function HotkeyField({
  value,
  onChange,
  placeholder,
  disabled = false,
  error = null
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  disabled?: boolean;
  error?: string | null;
}) {
  const hintId = useId();
  const errorId = useId();
  const [isListening, setIsListening] = useState(false);
  const hasValue = value.trim().length > 0;

  useEffect(() => {
    return () => {
      void sendBridgeCommand('setHotkeyCaptureActive', { active: false }).catch(() => {
        // Ignore bridge teardown errors.
      });
    };
  }, []);

  const helperText = isListening
    ? 'Naciśnij kombinację klawiszy…'
    : hasValue
      ? 'Zapisany skrót klawiaturowy.'
      : 'Kliknij i naciśnij skrót';

  return (
    <div className={`hotkey-field ${isListening ? 'is-listening' : ''} ${disabled ? 'is-disabled' : ''}`}>
      <div className="hotkey-field-input-wrap">
        <input
          className="hotkey-input"
          value={value}
          readOnly
          disabled={disabled}
          placeholder={placeholder}
          aria-invalid={error ? 'true' : 'false'}
          aria-describedby={error ? `${hintId} ${errorId}` : hintId}
          onMouseDown={() => {
            void sendBridgeCommand('setHotkeyCaptureActive', { active: true }).catch(() => {
              // Bridge may be unavailable (e.g. preview mode); ignore.
            });
          }}
          onFocus={(event) => {
            void sendBridgeCommand('setHotkeyCaptureActive', { active: true }).catch(() => {
              // Bridge may be unavailable (e.g. preview mode); ignore and keep local capture behavior.
            });
            setIsListening(true);
            event.currentTarget.select();
          }}
          onBlur={() => {
            void sendBridgeCommand('setHotkeyCaptureActive', { active: false }).catch(() => {
              // Ignore bridge sync errors.
            });
            setIsListening(false);
          }}
          onKeyDown={(event) => {
            if (disabled) {
              return;
            }

            event.preventDefault();
            event.stopPropagation();
            if (isClearHotkeyEvent(event)) {
              onChange('');
              return;
            }

            const hotkey = formatHotkey(event);
            if (hotkey) {
              onChange(hotkey);
            }
          }}
          onPaste={(event) => event.preventDefault()}
          onDrop={(event) => event.preventDefault()}
        />
        {isListening ? <span className="hotkey-listening-badge">NASŁUCHIWANIE</span> : null}
      </div>
      <p id={hintId} className="hotkey-field-hint">{helperText}</p>
      {error ? (
        <div id={errorId} className="field-validation-error" role="alert">
          {error}
        </div>
      ) : null}
    </div>
  );
}

function isClearHotkeyEvent(event: KeyboardEvent<HTMLInputElement>) {
  return !event.ctrlKey && !event.altKey && !event.shiftKey && !event.metaKey && ['Backspace', 'Delete', 'Escape'].includes(event.key);
}

function formatHotkey(event: KeyboardEvent<HTMLInputElement>) {
  const key = normalizeHotkeyKey(event.key, event.code);
  if (!key) {
    return null;
  }

  const modifiers = [
    event.ctrlKey ? 'Ctrl' : null,
    event.altKey ? 'Alt' : null,
    event.shiftKey ? 'Shift' : null,
    event.metaKey ? 'Win' : null
  ].filter(Boolean);

  return modifiers.length === 0 ? null : [...modifiers, key].join('+');
}

function normalizeHotkeyKey(key: string, code: string) {
  if (['Control', 'Shift', 'Alt', 'Meta', 'OS'].includes(key)) {
    return null;
  }

  // Use physical key code for letters/digits so AltGr/locale variants (e.g. Ę) map to base keys.
  if (/^Key[A-Z]$/.test(code)) {
    return code.slice(3);
  }

  if (/^Digit[0-9]$/.test(code)) {
    return code.slice(5);
  }

  if (key.length === 1) {
    return removeDiacritics(key).toUpperCase();
  }

  const aliases: Record<string, string> = {
    ' ': 'Space',
    ArrowLeft: 'Left',
    ArrowRight: 'Right',
    ArrowUp: 'Up',
    ArrowDown: 'Down',
    Backspace: 'Back',
    Enter: 'Return',
    Esc: 'Escape'
  };

  return aliases[key] ?? key;
}

function removeDiacritics(value: string) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '');
}

function getLauncherSettings(launcher: LauncherType) {
  switch (launcher) {
    case 'Steam Big Picture':
      return { type: 'Steam', executablePath: 'steam://open/bigpicture', arguments: '' };
    case 'Xbox App':
      return { type: 'XboxApp', executablePath: '', arguments: '' };
    case 'Playnite Fullscreen':
    default:
      return {
        type: 'Playnite',
        executablePath: '%LOCALAPPDATA%\\Playnite\\Playnite.FullscreenApp.exe',
        arguments: '--startfullscreen --hidesplashscreen'
      };
  }
}

function toLauncherType(type: string): LauncherType {
  switch (type) {
    case 'Steam':
      return 'Steam Big Picture';
    case 'XboxApp':
      return 'Xbox App';
    case 'Playnite':
    default:
      return 'Playnite Fullscreen';
  }
}

function getModeActionKey(command: ModeCommand) {
  return `${command}:`;
}

function getModeCardClass(command: ModeCommand, status: AppStatus, isUnconfigured = false, isSelected = false) {
  const classes = ['mode-card'];
  if (isCurrentMode(command, status)) {
    classes.push('active');
  } else if (isUnconfigured) {
    classes.push('unconfigured');
  }

  if (isSelected) {
    classes.push('selected');
  }

  return classes.join(' ');
}

function getModeCardStateLabel(command: ModeCommand, status: AppStatus, isUnconfigured: boolean) {
  if (isCurrentMode(command, status)) {
    return 'AKTYWNY';
  }

  return isUnconfigured ? 'NIESKONFIGUROWANY' : 'GOTOWY';
}

function getModeCardHint(command: ModeCommand, status: AppStatus) {
  return isCurrentMode(command, status) ? 'Jesteś tutaj' : `Przełącz na ${formatCommandTargetMode(command)}`;
}

function isCurrentMode(command: ModeCommand, status: AppStatus) {
  return (command === 'enterGameShell' && status.currentMode === 'Console')
    || (command === 'enterGameShellWithProfile' && status.currentMode === 'PcGaming')
    || (command === 'enterWorkMode' && status.currentMode === 'Work');
}

function formatCurrentMode(status: AppStatus) {
  switch (status.currentMode) {
    case 'Console':
    case 'PcGaming':
      return 'Gaming Mode';
    case 'Work':
      return 'Work Mode';
    default:
      return formatModeState(status.state);
  }
}

function formatModeState(state: AppStatus['state']) {
  switch (state) {
    case 'WorkMode':
      return 'Work Mode';
    case 'GameShell':
      return 'tryb gamingowy';
    case 'SwitchingToConsole':
      return 'przełączanie do gamingowego';
    case 'SwitchingToWork':
      return 'przełączanie do Work Mode';
    case 'Error':
      return 'wymaga uwagi';
    case 'Unknown':
    default:
      return 'nieustalony';
  }
}

function formatCommandTargetMode(command: ModeCommand) {
  switch (command) {
    case 'enterGameShell':
    case 'enterGameShellWithProfile':
      return 'Gaming Mode';
    case 'enterWorkMode':
      return 'Work Mode';
  }
}

function toPcGamingBehaviourState(behaviour: Partial<PcGamingBehaviourState> | AppSettings['pcGaming'] | null | undefined): PcGamingBehaviourState {
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

function resolvePcGamingBehaviourProfileName(activeName: string | null, profiles: DisplayLayoutProfile[]) {
  return resolveActivePcGamingProfile(activeName, profiles)?.name ?? null;
}

function buildProfileBehaviourMap(
  profiles: DisplayLayoutProfile[],
  configurations: NonNullable<AppSettings['display']['pcGamingProfileConfigurations']>,
  savedBehaviours: NonNullable<AppSettings['display']['pcGamingProfileBehaviours']>,
  fallbackBehaviour: PcGamingBehaviourState
): PcGamingProfileBehaviourMap {
  const map: PcGamingProfileBehaviourMap = {};

  profiles.forEach((profile) => {
    const configured = configurations.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    const saved = savedBehaviours.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    map[profile.name] = toPcGamingBehaviourState(configured?.behaviour ?? saved?.behaviour ?? fallbackBehaviour);
  });

  return map;
}

function buildProfileLauncherMap(
  profiles: DisplayLayoutProfile[],
  configurations: NonNullable<AppSettings['display']['pcGamingProfileConfigurations']>,
  fallbackLauncher: LauncherType
): PcGamingProfileLauncherMap {
  const map: PcGamingProfileLauncherMap = {};

  profiles.forEach((profile) => {
    const configured = configurations.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    map[profile.name] = toLauncherType(configured?.launcher?.type ?? fallbackLauncher);
  });

  return map;
}

function buildProfileHotkeysMap(
  profiles: DisplayLayoutProfile[],
  configurations: NonNullable<AppSettings['display']['pcGamingProfileConfigurations']>,
  fallbackHotkeys: ProfileHotkeysState
): PcGamingProfileHotkeysMap {
  const map: PcGamingProfileHotkeysMap = {};

  profiles.forEach((profile) => {
    const configured = configurations.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    map[profile.name] = {
      enterProfile: normalizeHotkeyForDisplay(configured?.hotkeys?.enterProfile ?? fallbackHotkeys.enterProfile)
    };
  });

  return map;
}

function buildProfileControllerShortcutMap(
  profiles: DisplayLayoutProfile[],
  configurations: NonNullable<AppSettings['display']['pcGamingProfileConfigurations']>,
  fallbackShortcut: ControllerShortcutSettings | null
): PcGamingProfileControllerShortcutMap {
  const map: PcGamingProfileControllerShortcutMap = {};

  profiles.forEach((profile) => {
    const configured = configurations.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    map[profile.name] = normalizeControllerShortcut(configured ? configured.controllerShortcut : fallbackShortcut);
  });

  return map;
}

function normalizeHotkeyValue(value: string) {
  return removeDiacritics(value.trim()).toUpperCase();
}

function isSameHotkey(left: string, right: string) {
  const normalizedLeft = normalizeHotkeyValue(left);
  return normalizedLeft.length > 0 && normalizedLeft === normalizeHotkeyValue(right);
}

function normalizeHotkeyForDisplay(value: string) {
  if (!value.trim()) {
    return '';
  }

  const parts = value
    .split('+')
    .map((part) => part.trim())
    .filter(Boolean);

  if (parts.length === 0) {
    return '';
  }

  const keyToken = removeDiacritics(parts[parts.length - 1]).toUpperCase();
  const lowerModifiers = new Set(parts.slice(0, -1).map((part) => removeDiacritics(part).toLowerCase()));
  const normalizedModifiers = [
    lowerModifiers.has('ctrl') || lowerModifiers.has('control') ? 'Ctrl' : null,
    lowerModifiers.has('alt') ? 'Alt' : null,
    lowerModifiers.has('shift') ? 'Shift' : null,
    lowerModifiers.has('win') || lowerModifiers.has('windows') || lowerModifiers.has('meta') ? 'Win' : null
  ].filter((modifier): modifier is string => Boolean(modifier));

  if (normalizedModifiers.length === 0 || !keyToken) {
    return '';
  }

  return [...normalizedModifiers, keyToken].join('+');
}

function isDuplicateProfileEnterHotkey(
  profileName: string,
  candidateHotkey: string,
  profileHotkeys: PcGamingProfileHotkeysMap
) {
  const normalizedCandidate = normalizeHotkeyValue(candidateHotkey);
  if (!normalizedCandidate) {
    return false;
  }

  return Object.entries(profileHotkeys).some(([name, hotkeys]) => {
    if (name.toLowerCase() === profileName.toLowerCase()) {
      return false;
    }

    return normalizeHotkeyValue(hotkeys.enterProfile) === normalizedCandidate;
  });
}

function isDuplicateWorkModeHotkey(candidateHotkey: string, profileHotkeys: PcGamingProfileHotkeysMap) {
  const normalizedCandidate = normalizeHotkeyValue(candidateHotkey);
  return normalizedCandidate.length > 0 && Object.values(profileHotkeys).some((hotkeys) => (
    normalizeHotkeyValue(hotkeys.enterProfile) === normalizedCandidate
  ));
}

function normalizeControllerButtons(buttons: string[] | null | undefined) {
  return [...new Set(
    (Array.isArray(buttons) ? buttons : [])
      .filter((button): button is string => typeof button === 'string')
      .map((button) => button.trim())
      .filter(Boolean)
  )];
}

function normalizeControllerStatus(status: ControllerStatusInfo): ControllerStatusInfo {
  return {
    ...status,
    capturedButtons: normalizeControllerButtons(status.capturedButtons),
    lastReadButtons: normalizeControllerButtons(status.lastReadButtons),
    completedCapture: status.completedCapture
      ? {
        ...status.completedCapture,
        buttons: normalizeControllerButtons(status.completedCapture.buttons)
      }
      : null
  };
}

function isControllerCapturePhase(phase: ControllerStatusInfo['capturePhase']) {
  return phase === 'WaitingForRelease'
    || phase === 'WaitingForFirstPress'
    || phase === 'Recording'
    || phase === 'WaitingForCombinationRelease';
}

function normalizeControllerShortcut(shortcut: ControllerShortcutSettings | null | undefined): ControllerShortcutSettings | null {
  if (!shortcut) {
    return null;
  }

  const buttons = normalizeControllerButtons(shortcut.buttons ?? []);
  if (buttons.length === 0) {
    return null;
  }

  return {
    buttons,
    holdDurationMs: shortcut.holdDurationMs || defaultControllerShortcutHoldDurationMs
  };
}

function isDuplicateProfileControllerShortcut(
  profileName: string,
  candidateButtons: string[],
  profileControllerShortcuts: PcGamingProfileControllerShortcutMap
) {
  if (candidateButtons.length === 0) {
    return false;
  }

  const normalizedCandidate = getControllerShortcutKey(candidateButtons);

  return Object.entries(profileControllerShortcuts).some(([name, shortcut]) => {
    if (name.toLowerCase() === profileName.toLowerCase()) {
      return false;
    }

    return getControllerShortcutKey(shortcut?.buttons) === normalizedCandidate;
  });
}

function isDuplicateWorkModeControllerShortcut(
  candidateButtons: string[],
  profileControllerShortcuts: PcGamingProfileControllerShortcutMap
) {
  const normalizedCandidate = getControllerShortcutKey(candidateButtons);
  return normalizedCandidate.length > 0 && Object.values(profileControllerShortcuts).some((shortcut) => (
    getControllerShortcutKey(shortcut?.buttons) === normalizedCandidate
  ));
}

function isSameControllerShortcut(left: string[] | undefined, right: string[] | undefined) {
  const leftKey = getControllerShortcutKey(left);
  return leftKey.length > 0 && leftKey === getControllerShortcutKey(right);
}

function getControllerShortcutKey(buttons: string[] | undefined) {
  return normalizeControllerButtons(buttons).sort((left, right) => left.localeCompare(right)).join('+').toUpperCase();
}

function formatControllerShortcutButtons(buttons: string[], family: ControllerFamily) {
  return mapControllerButtons(normalizeControllerButtons(buttons), family)
    .join(' + ');
}

function resolveActivePcGamingProfile(activeName: string | null, profiles: DisplayLayoutProfile[]) {
  if (profiles.length === 0) {
    return null;
  }

  if (activeName) {
    const matched = profiles.find((profile) => profile.name.toLowerCase() === activeName.toLowerCase());
    if (matched) {
      return matched;
    }
  }

  return profiles[0];
}

function extractDisplayIdentities(layout: DisplayLayoutSnapshot) {
  return layout.displays
    .filter((display) => display.isActive)
    .map((display) => display.identity);
}

function sameIdentity(left: DisplayIdentity, right: DisplayIdentity) {
  const leftPath = left.monitorDevicePath?.trim();
  const rightPath = right.monitorDevicePath?.trim();
  if (leftPath && rightPath) {
    return leftPath.toLowerCase() === rightPath.toLowerCase();
  }

  if (left.adapterLuid !== 0
    && right.adapterLuid !== 0
    && left.targetId !== 0
    && right.targetId !== 0) {
    return left.adapterLuid === right.adapterLuid
      && left.targetId === right.targetId;
  }

  if (left.edidManufacturerId > 0
    && right.edidManufacturerId > 0
    && left.edidProductCodeId > 0
    && right.edidProductCodeId > 0
    && left.connectorInstance > 0
    && right.connectorInstance > 0) {
    return left.edidManufacturerId === right.edidManufacturerId
      && left.edidProductCodeId === right.edidProductCodeId
      && left.connectorInstance === right.connectorInstance;
  }

  return false;
}

function areIdentitySetsEqual(left: DisplayIdentity[], right: DisplayIdentity[]) {
  if (left.length !== right.length) {
    return false;
  }

  return left.every((identity) => right.some((candidate) => sameIdentity(identity, candidate)));
}

function resolvePreferredProfileNameForPostSaveSelection(draftProfileName: string, selectedProfileName: string) {
  const trimmedDraft = draftProfileName.trim();
  if (trimmedDraft.length > 0) {
    return trimmedDraft;
  }

  const trimmedSelected = selectedProfileName.trim();
  return trimmedSelected.length > 0 ? trimmedSelected : '';
}

function formatDisplayResolution(display: DisplayDeviceInfo) {
  return display.width > 0 && display.height > 0 ? `${display.width} × ${display.height}` : 'Rozdzielczość nieznana';
}

function formatDisplayRefresh(display: DisplayDeviceInfo) {
  if (!display.refreshRateHz || display.refreshRateHz <= 0) {
    return 'Hz nieznane';
  }

  return `${formatNumber(display.refreshRateHz)} Hz`;
}

function summarizeLayout(layout: DisplayLayoutSnapshot) {
  const active = layout.displays.filter((display) => display.isActive);
  if (active.length === 0) {
    return 'brak aktywnych ekranów';
  }

  return active.map((display) => `${display.identity.friendlyName || 'Ekran'} ${display.width} × ${display.height}`).join(' / ');
}

function formatNumber(value: number) {
  return Number.isInteger(value) ? value.toFixed(0) : value.toFixed(2).replace(/\.?0+$/, '');
}
