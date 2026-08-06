import { useCallback, useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { sendBridgeCommand, subscribeBridgeEvent } from './services/webViewBridge';
import type {
  AppSettings,
  AppStatus,
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

type OverwriteProfileAction = 'next' | 'save' | 'save-then-continue';

type SettingsPayloadOverrides = {
  pcGamingProfiles?: DisplayLayoutProfile[];
  activePcGamingProfileName?: string | null;
  pcGamingLayout?: DisplayLayoutSnapshot | null;
  pcGamingProfileBehaviours?: PcGamingProfileBehaviourMap;
  pcGamingProfileLaunchers?: PcGamingProfileLauncherMap;
  pcGamingProfileHotkeys?: PcGamingProfileHotkeysMap;
};

const MAX_PROFILE_DISPLAYS = 2;
const PROFILE_DISPLAY_LIMIT_MESSAGE = 'Obsługa profili z więcej niż 2 monitorami będzie dostępna wkrótce. Na razie wybierz maksymalnie 2 ekrany.';

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
  { id: 'gaming-display', title: 'Ekrany', subtitle: 'Tworzenie profili ekranow' },
  { id: 'launcher', title: 'Launcher profilu', subtitle: 'Launcher per profil' },
  { id: 'console-behaviour', title: 'Zachowanie profilu', subtitle: 'Pulpit i okna per profil' },
  { id: 'controller', title: 'Skróty profilu', subtitle: 'Hotkeye per profil' },
  { id: 'work-behaviour', title: 'Zachowanie Work Mode', subtitle: 'Powrót pulpitu' },
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
    description: 'Najlepszy wybór dla biblioteki wielu launcherów i obsługi pada na TV.',
    badge: 'Polecany'
  },
  {
    value: 'Steam Big Picture',
    title: 'Steam Big Picture',
    description: 'Bezpośrednie wejście w tryb kanapowy Steam.',
    badge: 'Steam'
  },
  {
    value: 'Xbox App',
    title: 'Xbox App',
    description: 'Szybki start aplikacji Xbox i biblioteki Game Pass.',
    badge: 'Xbox'
  }
];

const defaultProfileHotkeys: ProfileHotkeysState = {
  enterProfile: ''
};

export function App() {
  const [status, setStatus] = useState<AppStatus>(initialStatus);
  const [settings, setSettings] = useState<AppSettings | null>(null);
  const [stepIndex, setStepIndex] = useState(0);
  const [message, setMessage] = useState('Gotowe.');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [activeModeAction, setActiveModeAction] = useState<string | null>(null);
  const [isInitialLoading, setIsInitialLoading] = useState(true);
  const [isVeryFirstConfiguration, setIsVeryFirstConfiguration] = useState(true);
  const [isSetupComplete, setIsSetupComplete] = useState(false);
  const [isEditingSection, setIsEditingSection] = useState(false);
  const [isCreatingProfileFlow, setIsCreatingProfileFlow] = useState(false);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const [pendingNavigationAction, setPendingNavigationAction] = useState<PendingNavigationAction | null>(null);
  const [showUnsavedDialog, setShowUnsavedDialog] = useState(false);
  const [showOverwriteProfileDialog, setShowOverwriteProfileDialog] = useState(false);
  const [pendingOverwriteProfileAction, setPendingOverwriteProfileAction] = useState<OverwriteProfileAction | null>(null);
  const [displayProfileDraft, setDisplayProfileDraft] = useState<DisplayProfileDraft>({ profileName: '', identities: [] });
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
      })
    ])
      .then(([nextStatus, loadedSettings, detectedDisplays]) => {
        if (!isMounted) {
          return;
        }

        setStatus(nextStatus);
        setSettings(loadedSettings);
        setDisplays(detectedDisplays);
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
            pcGamingMode: nextHotkeys.enterProfile
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
        pcGamingMode: nextHotkeys.enterProfile
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
      enterProfile: loadedSettings.hotkeys?.pcGamingMode ?? ''
    });
    const selectedProfileName = activeProfile?.name ?? loadedProfiles[0]?.name ?? '';

    setSelectedConsoleDisplays(consoleDisplays);
    setSelectedConsoleDisplay(consoleDisplays[0] ?? null);
    setPcGamingProfiles(loadedProfiles);
    setActivePcGamingProfileName(activeProfile?.name ?? null);
    setPcGamingProfileBehaviours(nextProfileBehaviours);
    setPcGamingProfileLaunchers(nextProfileLaunchers);
    setPcGamingProfileHotkeys(nextProfileHotkeys);
    setSelectedPcGamingBehaviourProfileName(selectedProfileName);
    setPcGamingLayout(activeProfile?.layout ?? loadedSettings.display?.pcGamingLayout ?? null);
    setHotkeys({
      consoleMode: loadedSettings.hotkeys?.consoleMode || '',
      pcGamingMode: loadedSettings.hotkeys?.pcGamingMode || '',
      workMode: loadedSettings.hotkeys?.workMode || ''
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
    if (key === 'enterProfile' && isDuplicateProfileEnterHotkey(profileName, value, pcGamingProfileHotkeys)) {
      setError('Ten hotkey wejścia jest już przypisany do innego profilu. Wybierz inną kombinację.');
      return;
    }

    setError(null);
    setPcGamingProfileHotkeys((current) => ({
      ...current,
      [profileName]: {
        ...(current[profileName] ?? defaultProfileHotkeys),
        [key]: value
      }
    }));
    markUnsavedChanges();

    if (selectedPcGamingBehaviourProfileName === profileName) {
      setHotkeys((current) => ({
        ...current,
        pcGamingMode: key === 'enterProfile' ? value : current.pcGamingMode
      }));
    }
  }

  function buildSettingsPayload(
    isFirstRunCompleted = isSetupComplete,
    overrides?: SettingsPayloadOverrides
  ): AppSettings {
    const effectivePcGamingProfiles = overrides?.pcGamingProfiles ?? pcGamingProfiles;
    const effectiveActivePcGamingProfileName = overrides?.activePcGamingProfileName ?? activePcGamingProfileName;
    const effectivePcGamingLayout = overrides?.pcGamingLayout ?? pcGamingLayout;
    const effectivePcGamingProfileBehaviours = overrides?.pcGamingProfileBehaviours ?? pcGamingProfileBehaviours;
    const effectivePcGamingProfileLaunchers = overrides?.pcGamingProfileLaunchers ?? pcGamingProfileLaunchers;
    const effectivePcGamingProfileHotkeys = overrides?.pcGamingProfileHotkeys ?? pcGamingProfileHotkeys;

    const activeProfileName = resolvePcGamingBehaviourProfileName(effectiveActivePcGamingProfileName, effectivePcGamingProfiles);
    const activeProfileBehaviour = activeProfileName ? effectivePcGamingProfileBehaviours[activeProfileName] : null;
    const activeProfileLauncher = activeProfileName ? effectivePcGamingProfileLaunchers[activeProfileName] : null;
    const activeProfileHotkeys = activeProfileName ? effectivePcGamingProfileHotkeys[activeProfileName] : null;
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
            enterProfile: (effectivePcGamingProfileHotkeys[profile.name]?.enterProfile ?? savedHotkeys.enterProfile ?? '').trim(),
            enterWorkMode: ''
          }
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
      controller: settings?.controller ?? { enabled: false, consoleMode: null, pcGamingMode: null, workMode: null },
      hotkeys: {
        consoleMode: hotkeys.consoleMode.trim(),
        pcGamingMode: (savedHotkeys.enterProfile || hotkeys.pcGamingMode).trim(),
        workMode: hotkeys.workMode.trim()
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

    if (!hasUnsavedChanges) {
      void executeNavigation(action);
      return;
    }

    setPendingNavigationAction(action);
    setShowUnsavedDialog(true);
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
    }
    setMessage(isEditingSection ? 'Możesz przejrzeć kolejne sekcje albo zapisać zmiany.' : 'Gotowe.');
  }

  async function commitGamingDisplayDraft(allowOverwrite: boolean, overwriteAction: OverwriteProfileAction) {
    const latestDraft = displayProfileDraftRef.current;
    const profileName = latestDraft.profileName.trim();
    const identities = latestDraft.identities;
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
      [resolvedProfileName]: pcGamingProfileHotkeys[resolvedProfileName] ?? {
        enterProfile: hotkeys.pcGamingMode
      }
    };

    setPcGamingProfiles(nextProfiles);
    setActivePcGamingProfileName(resolvedProfileName);
    setPcGamingLayout(layout);
    setSelectedPcGamingBehaviourProfileName(resolvedProfileName);
    setPcGamingProfileBehaviours(nextProfileBehaviours);
    setPcGamingProfileLaunchers(nextProfileLaunchers);
    setPcGamingProfileHotkeys(nextProfileHotkeys);
    pendingSettingsOverrideRef.current = {
      pcGamingProfiles: nextProfiles,
      activePcGamingProfileName: resolvedProfileName,
      pcGamingLayout: layout,
      pcGamingProfileBehaviours: nextProfileBehaviours,
      pcGamingProfileLaunchers: nextProfileLaunchers,
      pcGamingProfileHotkeys: nextProfileHotkeys
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
      setMessage(result.message);
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Nie udało się uruchomić trybu.');
    } finally {
      setActiveModeAction(null);
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
        activeModeAction={activeModeAction}
        message={footerMessage}
        hasError={Boolean(error || status.lastError)}
        runModeCommand={runModeCommand}
        selectProfile={setSelectedPcGamingBehaviourProfileName}
        createProfile={() => {
          const targetIndex = steps.findIndex((step) => step.id === 'gaming-display');
          setIsEditingSection(false);
          setIsCreatingProfileFlow(true);
          setIsSetupComplete(false);
          syncDisplayProfileDraft({ profileName: '', identities: [] });
          setShowOverwriteProfileDialog(false);
          setStepIndex(targetIndex >= 0 ? targetIndex : 0);
          setMessage('Dodaj nowy profil i przejdź przez kroki profilu.');
        }}
        editSettings={(stepId) => {
          const targetIndex = steps.findIndex((step) => step.id === stepId);
          setIsEditingSection(true);
          setIsCreatingProfileFlow(false);
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
              <small>lokalny kreator Windows</small>
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
                  if (!canSelectSidebarStep) {
                    return;
                  }

                  const targetIndex = steps.findIndex((entry) => entry.id === step.id);
                  if (targetIndex >= 0) {
                    setError(null);
                    setStepIndex(targetIndex);
                    setMessage('Edytujesz wybraną sekcję.');
                  }
                }}
                onKeyDown={(event: KeyboardEvent<HTMLLIElement>) => {
                  if (!canSelectSidebarStep || (event.key !== 'Enter' && event.key !== ' ')) {
                    return;
                  }

                  event.preventDefault();
                  const targetIndex = steps.findIndex((entry) => entry.id === step.id);
                  if (targetIndex >= 0) {
                    setError(null);
                    setStepIndex(targetIndex);
                    setMessage('Edytujesz wybraną sekcję.');
                  }
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
                globalWorkModeHotkey={hotkeys.workMode}
                onChangeGlobalWorkModeHotkey={(value) => {
                  setHotkeys((current) => ({
                    ...current,
                    workMode: value
                  }));
                  markUnsavedChanges();
                }}
                behaviour={workBehaviour}
                setBehaviour={(behaviour) => {
                  setWorkBehaviour(behaviour);
                  markUnsavedChanges();
                }}
              />
            ) : null}
            {currentStep.id === 'controller' ? (
              <ControllerStep
                stepNumber={resolvedFlowPosition + 1}
                pcGamingProfiles={pcGamingProfiles}
                selectedProfileName={selectedPcGamingBehaviourProfileName}
                hotkeys={hotkeys}
                onChangeHotkey={(key, value) => {
                  const profileName = selectedPcGamingBehaviourProfileName;
                  if (!profileName) {
                    setHotkeys((current) => ({
                      ...current,
                      pcGamingMode: key === 'enterProfile' ? value : current.pcGamingMode
                    }));
                    return;
                  }

                  updateProfileHotkey(profileName, key, value);
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
  activeModeAction,
  message,
  hasError,
  runModeCommand,
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
  activeModeAction: string | null;
  message: string;
  hasError: boolean;
  runModeCommand: (command: ModeCommand, saveBeforeRun?: boolean, payload?: unknown) => Promise<void>;
  selectProfile: (profileName: string) => void;
  createProfile: () => void;
  editSettings: (stepId: StepId) => void;
}) {
  const activeProfileLauncher = activeProfileName ? pcGamingProfileLaunchers[activeProfileName] : null;
  const visibleLauncher = activeProfileLauncher ?? launcher;
  const selectedProfile = pcGamingProfiles.find((profile) => profile.name === selectedProfileName) ?? null;
  const canRunSelectedProfile = Boolean(selectedProfileName) && !isModeSwitching;

  return (
    <main className="app-shell">
      <section className="setup-window" aria-labelledby="app-title">
        <aside className="brand-panel" aria-label="GameShell">
          <div className="brand-lockup">
            <img className="brand-mark" src="/assets/GameShell.png" alt="" aria-hidden="true" />
            <div>
              <p>GameShell</p>
              <small>tryby lokalne Windows</small>
            </div>
          </div>
          <section className="mode-side" aria-label="Setup">
            <div className="mode-side-head">
              <span>Obecny tryb</span>
              <strong>{formatCurrentMode(status)}</strong>
            </div>
            <dl className="mode-side-facts">
              <div>
                <dt>Profil Gaming Mode</dt>
                <dd>{activeProfileName || 'Nie wybrano'}</dd>
              </div>
              <div>
                <dt>Uklad ekranow</dt>
                <dd>{pcGamingLayout ? summarizeLayout(pcGamingLayout) : 'Brak zapisanego profilu'}</dd>
              </div>
              <div>
                <dt>Launcher</dt>
                <dd>{visibleLauncher}</dd>
              </div>
            </dl>
          </section>
        </aside>

        <div className="setup-main">
          <header className="masthead">
            <div>
              <h1 id="app-title">GameShell</h1>
              <p className="intro">Wybierz następny tryb albo popraw konkretną część konfiguracji.</p>
            </div>
          </header>

          <section className="setup-page">
            <div className="dashboard-layout">
              <section className="dashboard-band dashboard-mode-band" aria-labelledby="dashboard-modes-title">
                <div className="dashboard-section-head">
                  <div>
                    <p className="eyebrow">Panel główny</p>
                    <h2 id="dashboard-modes-title">Szybkie przełączenie</h2>
                  </div>
                  <span className="current-mode-chip">{formatCurrentMode(status)}</span>
                </div>
                <div className="mode-card-grid">
                  <button className={getModeCardClass('enterWorkMode', status)} type="button" disabled={isModeSwitching} onClick={() => runModeCommand('enterWorkMode')}>
                    <span className="mode-card-kicker">Strategiczny powrót</span>
                    <span className="mode-card-hotkey">{globalWorkModeHotkey ? `Hotkey: ${globalWorkModeHotkey}` : 'Hotkey: brak'}</span>
                    <strong>Work Mode</strong>
                    <small>Stały punkt bezpieczeństwa: szybki powrót do układu roboczego i pulpitu.</small>
                    <span className="mode-card-state">{getModeCardStateLabel('enterWorkMode', status, false)}</span>
                    <em className="mode-card-action">{activeModeAction === getModeActionKey('enterWorkMode') ? 'Przełączanie...' : getModeCardHint('enterWorkMode', status)}</em>
                  </button>
                  {pcGamingProfiles.map((profile) => {
                    const profileLauncher = pcGamingProfileLaunchers[profile.name] ?? launcher;
                    const profileHotkeys = pcGamingProfileHotkeys[profile.name] ?? defaultProfileHotkeys;
                    const profileActionKey = getModeActionKey('enterGameShellWithProfile');
                    const isProfileConfiguredAsActive = activeProfileName === profile.name;
                    const isProfileSelectedForEditing = selectedProfileName === profile.name;
                    const isPcGamingNow = status.currentMode === 'PcGaming';
                    const stateLabel = isPcGamingNow && isProfileConfiguredAsActive ? 'AKTYWNY' : 'GOTOWY';

                    return (
                      <button
                        className={getModeCardClass('enterGameShellWithProfile', status, false, isProfileSelectedForEditing)}
                        type="button"
                        key={profile.name}
                        disabled={isModeSwitching}
                        onClick={() => selectProfile(profile.name)}
                      >
                        <span className="mode-card-kicker">Profil ekranow</span>
                        <strong>{profile.name}</strong>
                        <small>{summarizeLayout(profile.layout)} · {profileLauncher}</small>
                        <span className="mode-card-state">{isProfileSelectedForEditing ? 'ZAZNACZONY' : stateLabel}</span>
                        <em className="mode-card-action">
                          {activeModeAction === profileActionKey
                            ? 'Przełączanie...'
                            : isProfileSelectedForEditing
                              ? 'Profil do edycji'
                              : profileHotkeys.enterProfile
                                ? `Hotkey: ${profileHotkeys.enterProfile}`
                                : 'Kliknij, aby zaznaczyć'}
                        </em>
                      </button>
                    );
                  })}
                </div>
                {pcGamingProfiles.length === 0 ? <p className="hint-line">Brak zapisanych profili. Dodaj profil w sekcji Ekrany.</p> : null}
                <div className="dashboard-inline-actions">
                  <button
                    className="primary-button compact"
                    type="button"
                    disabled={!canRunSelectedProfile}
                    onClick={() => runModeCommand('enterGameShellWithProfile', false, { profileName: selectedProfileName })}
                  >
                    Uruchom profil{selectedProfile ? `: ${selectedProfile.name}` : ''}
                  </button>
                  <button className="secondary-button compact" type="button" onClick={createProfile}>
                    Dodaj profil
                  </button>
                </div>
              </section>

              <section className="dashboard-band" aria-labelledby="dashboard-config-title">
                <div className="dashboard-section-head compact">
                  <div>
                    <p className="eyebrow">Konfiguracja</p>
                    <h2 id="dashboard-config-title">Dostosuj ustawienia</h2>
                  </div>
                </div>
                <div className="dashboard-shortcut-groups" aria-label="Edycja konfiguracji">
                  <section className="dashboard-shortcut-group" aria-labelledby="profile-settings-title">
                    <h3 id="profile-settings-title">Ustawienia profilu</h3>
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

                  <section className="dashboard-shortcut-group" aria-labelledby="global-settings-title">
                    <h3 id="global-settings-title">Ustawienia globalne</h3>
                    <div className="section-shortcuts section-shortcuts-global">
                      <button type="button" onClick={() => editSettings('work-behaviour')}>
                        <span>Zachowanie Work Mode</span>
                        <small>Globalne przywracanie pulpitu po graniu</small>
                      </button>
                      <button type="button" onClick={() => editSettings('app-behaviour')}>
                        <span>Uruchamianie aplikacji</span>
                        <small>Autostart, tray i skróty systemowe</small>
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
        GameShell przełącza układ ekranów natywnie przez Windows, uruchamia tryb gamingowy i pozwala wrócić
        do układu pracy.
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
    setDisplayLimitMessage(profileIdentities.length > MAX_PROFILE_DISPLAYS ? PROFILE_DISPLAY_LIMIT_MESSAGE : null);
    onDraftChange(selectedProfile.name, profileIdentities);
  }, [pcGamingProfiles, prefillFromSelectedProfile, selectedProfileName]);

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

      onDraftChange(newProfileName, next);
      return next;
    });
  }

  return (
    <div className="wizard-copy displays-step">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Ekrany</h2>
      <section className="settings-section">
        <h3>Wykryte ekrany</h3>
        <p>Kliknij ekrany, aby zbudować lub edytować aktualnie zaznaczony profil. Zaznaczone karty są zielone, a niezaznaczone pozostają tylko podglądem stanu podłączenia.</p>
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
            onDraftChange(nextName, selectedDisplays);
          }}
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
  canEditSelection: boolean;
  displayLimitMessage: string | null;
}) {
  const selectionMessage = displayLimitMessage ?? (!canEditSelection ? 'Uzupełnij nazwę profilu i wybierz co najmniej jeden ekran.' : null);

  return (
    <section className="display-diagnostics" aria-labelledby="display-diagnostics-title">
      <div className="display-diagnostics-head">
        <div>
          <h3 id="display-diagnostics-title">Wykryte ekrany</h3>
          <p>Monitory widziane przez Windows, także wyłączone w ustawieniach ekranów.</p>
        </div>
        <div className="display-diagnostics-actions">
          <button className="secondary-button compact" type="button" disabled={isRefreshing} onClick={refreshDisplays}>
            {isRefreshing ? 'Odświeżanie...' : 'Odśwież listę ekranów'}
          </button>
        </div>
      </div>
      <label className="profile-input-group">
        Nazwa profilu
        <input
          className="text-input"
          type="text"
          value={profileName}
          onChange={(event) => setProfileName(event.target.value)}
          placeholder="np. TV + monitor biurkowy"
        />
      </label>
      {selectionMessage ? (
        <p className={`${displayLimitMessage ? 'error-line display-error' : 'hint-line'} display-selection-message`}>
          {selectionMessage}
        </p>
      ) : null}
      {error ? <p className="error-line display-error">{error}</p> : null}
      {!error && displays.length === 0 ? <p className="hint-line">Nie wykryto ekranów.</p> : null}
      {displays.length > 0 ? (
        <div className="display-list">
          {displays.map((display) => {
            const isSelected = selectedDisplays.some((selectedDisplay) => sameIdentity(selectedDisplay, display.identity));
            const displayState = isSelected ? 'selected' : display.isEnabled ? 'available' : 'inactive';
            return (
              <article className={`display-item ${displayState}`} key={display.id || `${display.adapterLuid}:${display.targetId}`} onClick={() => onToggleDisplay(display.identity)}>
                <div className="display-item-main">
                  <strong>{display.name || 'Nieznany ekran'}</strong>
                  <span>
                    {formatDisplayResolution(display)} · {formatDisplayRefresh(display)}
                    {display.isEnabled && display.isPrimary ? ' · Główny' : ''}
                    {!display.isEnabled ? ' · Nieaktywny' : ''}
                  </span>
                  <small>Pozycja: {display.positionX}, {display.positionY}</small>
                  {display.connectionTechnology ? <small>Połączenie: {display.connectionTechnology}</small> : null}
                </div>
              </article>
            );
          })}
        </div>
      ) : null}
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
      <p>Każdy profil ekranów może mieć inny launcher. Edytujesz aktualnie zaznaczony profil z panelu głównego.</p>
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
        <p>Checkboxy dotyczą aktualnie zaznaczonego profilu z panelu głównego.</p>
        <div className="field-grid">
          <div className="profile-target-line" aria-live="polite">
            <span>Edytowany profil</span>
            <strong>{selectedProfileLabel}</strong>
          </div>
        </div>
        <div className="check-list">
          {pcGamingBehaviourOptions.map((option) => (
            <CheckRow
              key={option.key}
              label={option.label}
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
  globalWorkModeHotkey,
  onChangeGlobalWorkModeHotkey,
  behaviour,
  setBehaviour
}: {
  stepNumber: number;
  globalWorkModeHotkey: string;
  onChangeGlobalWorkModeHotkey: (value: string) => void;
  behaviour: typeof defaultWorkBehaviour;
  setBehaviour: (behaviour: typeof defaultWorkBehaviour) => void;
}) {
  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Zachowanie Work Mode</h2>
      <p>Work Mode przywraca poprzedni układ ekranów oraz normalny pulpit.</p>
      <div className="check-list">
        <CheckRow label="Zamknij launcher" checked={behaviour.closeLauncher} onChange={(value) => setBehaviour({ ...behaviour, closeLauncher: value })} />
        <CheckRow label="Pokaż paski zadań" checked={behaviour.showTaskbars} onChange={(value) => setBehaviour({ ...behaviour, showTaskbars: value })} />
        <CheckRow label="Pokaż ikony pulpitu" checked={behaviour.showDesktopIcons} onChange={(value) => setBehaviour({ ...behaviour, showDesktopIcons: value })} />
        <CheckRow label="Nie minimalizuj istniejących okien" checked={behaviour.doNotMinimizeWindows} onChange={(value) => setBehaviour({ ...behaviour, doNotMinimizeWindows: value })} />
      </div>
      <div className="field-grid">
        <label>
          Globalny hotkey przejścia do Work Mode
          <HotkeyInput value={globalWorkModeHotkey} onChange={onChangeGlobalWorkModeHotkey} placeholder="np. Ctrl+Alt+W" />
        </label>
      </div>
    </div>
  );
}

function ControllerStep({
  stepNumber,
  pcGamingProfiles,
  selectedProfileName,
  hotkeys,
  onChangeHotkey
}: {
  stepNumber: number;
  pcGamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  hotkeys: HotkeySettingsState;
  onChangeHotkey: (key: 'enterProfile', value: string) => void;
}) {
  const hasProfiles = pcGamingProfiles.length > 0;
  const selectedProfileLabel = selectedProfileName || 'Brak zaznaczonego profilu';

  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Skróty profilu</h2>
      <section className="settings-section">
        <h3>Skróty klawiaturowe</h3>
        <p>Każdy profil może mieć własny hotkey wejścia. Edytujesz profil wybrany na panelu głównym.</p>
        <div className="field-grid">
          <div className="profile-target-line" aria-live="polite">
            <span>Edytowany profil</span>
            <strong>{selectedProfileLabel}</strong>
          </div>
        </div>
        <div className="field-grid">
          <label>
            Hotkey uruchomienia profilu
            <HotkeyInput value={hotkeys.pcGamingMode} onChange={(value) => onChangeHotkey('enterProfile', value)} placeholder="np. Ctrl+Alt+P" disabled={!hasProfiles} />
          </label>
        </div>
      </section>
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
      <p>Te ustawienia są globalne i dotyczą całej aplikacji, nie pojedynczego profilu.</p>
      <div className="check-list">
        <CheckRow
          label="Startuj z Windows"
          checked={finishOptions.startWithWindows}
          onChange={(value) => setFinishOptions({
            ...finishOptions,
            startWithWindows: value,
            startMinimized: value ? finishOptions.startMinimized : false
          })}
        />
        <CheckRow
          label="Startuj zminimalizowany do tray"
          checked={finishOptions.startMinimized}
          disabled={!finishOptions.startWithWindows}
          onChange={(value) => setFinishOptions({ ...finishOptions, startMinimized: value })}
        />
        <CheckRow
          label="Utwórz skrót w menu Start"
          checked={finishOptions.startMenuShortcut}
          onChange={(value) => setFinishOptions({ ...finishOptions, startMenuShortcut: value })}
        />
        <CheckRow
          label="Utwórz skrót na pulpicie"
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
      <h2>Sprawdź podsumowanie</h2>
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

function CheckRow({
  label,
  checked,
  onChange,
  disabled = false
}: {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
}) {
  return (
    <label className="check-row">
      <input type="checkbox" checked={checked} disabled={disabled} onChange={(event) => onChange(event.target.checked)} />
      <span>{label}</span>
    </label>
  );
}

function HotkeyInput({
  value,
  onChange,
  placeholder,
  disabled = false
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  disabled?: boolean;
}) {
  return (
    <input
      className="hotkey-input"
      value={value}
      readOnly
      disabled={disabled}
      placeholder={placeholder}
      onFocus={(event) => event.currentTarget.select()}
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
  );
}

function isClearHotkeyEvent(event: KeyboardEvent<HTMLInputElement>) {
  return !event.ctrlKey && !event.altKey && !event.shiftKey && !event.metaKey && ['Backspace', 'Delete', 'Escape'].includes(event.key);
}

function formatHotkey(event: KeyboardEvent<HTMLInputElement>) {
  const key = normalizeHotkeyKey(event.key);
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

function normalizeHotkeyKey(key: string) {
  if (['Control', 'Shift', 'Alt', 'Meta', 'OS'].includes(key)) {
    return null;
  }

  if (key.length === 1) {
    return key.toUpperCase();
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
      enterProfile: configured?.hotkeys?.enterProfile ?? fallbackHotkeys.enterProfile
    };
  });

  return map;
}

function normalizeHotkeyValue(value: string) {
  return value.trim().toUpperCase();
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
