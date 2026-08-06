import { useEffect, useState, type KeyboardEvent } from 'react';
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
  | 'test'
  | 'finish';

type LauncherType = 'Playnite Fullscreen' | 'Steam Big Picture' | 'Xbox App';
type ModeCommand = 'enterGameShell' | 'enterGameShellWithProfile' | 'enterWorkMode';
type HotkeySettingsState = {
  consoleMode: string;
  pcGamingMode: string;
  workMode: string;
};

type SetupStep = {
  id: StepId;
  title: string;
  subtitle: string;
};

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
  { id: 'gaming-display', title: 'Ekrany', subtitle: 'Profile ekranow i Gaming Mode' },
  { id: 'launcher', title: 'Launcher', subtitle: 'Co uruchamiać' },
  { id: 'console-behaviour', title: 'Zachowanie Gaming Mode', subtitle: 'Pulpit i okna' },
  { id: 'work-behaviour', title: 'Zachowanie Work Mode', subtitle: 'Powrót pulpitu' },
  { id: 'controller', title: 'Skróty klawiatury i kontrolera', subtitle: 'Hotkeye trybów' },
  { id: 'test', title: 'Test', subtitle: 'Sprawdzenie przełączania' },
  { id: 'finish', title: 'Zakończenie', subtitle: 'Gotowe do zapisu' }
];

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

export function App() {
  const [status, setStatus] = useState<AppStatus>(initialStatus);
  const [settings, setSettings] = useState<AppSettings | null>(null);
  const [stepIndex, setStepIndex] = useState(0);
  const [message, setMessage] = useState('Gotowe.');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [activeModeAction, setActiveModeAction] = useState<string | null>(null);
  const [isInitialLoading, setIsInitialLoading] = useState(true);
  const [isSetupComplete, setIsSetupComplete] = useState(false);
  const [isEditingSection, setIsEditingSection] = useState(false);
  const [displays, setDisplays] = useState<DisplayDeviceInfo[]>([]);
  const [isRefreshingDisplays, setIsRefreshingDisplays] = useState(false);
  const [displayListError, setDisplayListError] = useState<string | null>(null);
  const [selectedConsoleDisplay, setSelectedConsoleDisplay] = useState<DisplayIdentity | null>(null);
  const [selectedConsoleDisplays, setSelectedConsoleDisplays] = useState<DisplayIdentity[]>([]);
  const [pcGamingLayout, setPcGamingLayout] = useState<DisplayLayoutSnapshot | null>(null);
  const [pcGamingProfiles, setPcGamingProfiles] = useState<DisplayLayoutProfile[]>([]);
  const [activePcGamingProfileName, setActivePcGamingProfileName] = useState<string | null>(null);
  const [pcGamingProfileBehaviours, setPcGamingProfileBehaviours] = useState<PcGamingProfileBehaviourMap>({});
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

  const currentStep = steps[Math.min(stepIndex, steps.length - 1)];
  const isLastStep = stepIndex === steps.length - 1;
  const canGoBack = stepIndex > 0;
  const isModeSwitching = activeModeAction !== null;
  const footerMessage = error || status.lastError || message;

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
      }

      return;
    }

    setPcGamingBehaviour(pcGamingProfileBehaviours[selectedPcGamingBehaviourProfileName] ?? pcGamingBehaviour);
  }, [activePcGamingProfileName, pcGamingProfiles, pcGamingProfileBehaviours, selectedPcGamingBehaviourProfileName]);

  function applySettingsToState(loadedSettings: AppSettings) {
    const consoleDisplays = loadedSettings.display?.consoleDisplays?.length
      ? loadedSettings.display.consoleDisplays
      : loadedSettings.display?.consoleDisplay ? [loadedSettings.display.consoleDisplay] : [];
    const loadedProfiles = loadedSettings.display?.pcGamingProfiles ?? [];
    const activeProfile = resolveActivePcGamingProfile(loadedSettings.display?.activePcGamingProfileName ?? null, loadedProfiles);
    const loadedProfileBehaviours = loadedSettings.display?.pcGamingProfileBehaviours ?? [];
    const defaultBehaviour = toPcGamingBehaviourState(loadedSettings.pcGaming);
    const nextProfileBehaviours = buildProfileBehaviourMap(loadedProfiles, loadedProfileBehaviours, defaultBehaviour);
    const selectedProfileName = activeProfile?.name ?? loadedProfiles[0]?.name ?? '';

    setSelectedConsoleDisplays(consoleDisplays);
    setSelectedConsoleDisplay(consoleDisplays[0] ?? null);
    setPcGamingProfiles(loadedProfiles);
    setActivePcGamingProfileName(activeProfile?.name ?? null);
    setPcGamingProfileBehaviours(nextProfileBehaviours);
    setSelectedPcGamingBehaviourProfileName(selectedProfileName);
    setPcGamingLayout(activeProfile?.layout ?? loadedSettings.display?.pcGamingLayout ?? null);
    setHotkeys({
      consoleMode: loadedSettings.hotkeys?.consoleMode || '',
      pcGamingMode: loadedSettings.hotkeys?.pcGamingMode || '',
      workMode: loadedSettings.hotkeys?.workMode || ''
    });
    setLauncher(toLauncherType(loadedSettings.launcher?.type || 'Playnite'));
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
    setIsSetupComplete(loadedSettings.ui?.isFirstRunCompleted ?? false);
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

  function buildSettingsPayload(isFirstRunCompleted = isSetupComplete): AppSettings {
    const launcherSettings = getLauncherSettings(launcher);
    const activeProfileName = resolvePcGamingBehaviourProfileName(activePcGamingProfileName, pcGamingProfiles);
    const activeProfileBehaviour = activeProfileName ? pcGamingProfileBehaviours[activeProfileName] : null;
    const savedPcGamingBehaviour = activeProfileBehaviour ?? pcGamingBehaviour;

    return {
      display: {
        consoleDisplay: selectedConsoleDisplay,
        consoleDisplays: selectedConsoleDisplays,
        pcGamingLayout,
        pcGamingProfiles,
        activePcGamingProfileName,
        pcGamingProfileBehaviours: pcGamingProfiles.map((profile) => ({
          profileName: profile.name,
          behaviour: pcGamingProfileBehaviours[profile.name] ?? savedPcGamingBehaviour
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
        pcGamingMode: hotkeys.pcGamingMode.trim(),
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

  async function saveCurrentSettings(isFirstRunCompleted = isSetupComplete) {
    const savedSettings = await sendBridgeCommand<AppSettings>('saveSettings', buildSettingsPayload(isFirstRunCompleted));
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

  async function saveAndReturnToDashboard() {
    setError(null);
    setIsSaving(true);
    setMessage(isEditingSection ? 'Zapisywanie zmian...' : 'Zapisywanie konfiguracji...');

    try {
      const savedSettings = await saveCurrentSettings(true);
      setIsEditingSection(false);
      setIsSetupComplete(savedSettings.ui.isFirstRunCompleted);
      setMessage(isEditingSection ? 'Zmiany zapisane.' : 'Konfiguracja zapisana. GameShell jest gotowy do użycia.');
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Nie udało się zapisać konfiguracji.');
    } finally {
      setIsSaving(false);
    }
  }

  async function goNext() {
    setError(null);
    if (isLastStep) {
      await saveAndReturnToDashboard();
      return;
    }

    setStepIndex((current) => Math.min(current + 1, steps.length - 1));
    setMessage(isEditingSection ? 'Możesz przejrzeć kolejne sekcje albo zapisać zmiany.' : 'Gotowe.');
  }

  function goBack() {
    setError(null);
    setStepIndex((current) => Math.max(0, current - 1));
  }

  function returnToDashboard() {
    setError(null);
    setIsEditingSection(false);
    setIsSetupComplete(true);
    setMessage('Gotowe.');
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
    setError(null);
    try {
      const savedSettings = await sendBridgeCommand<AppSettings>('savePcGamingDisplays', { identities, profileName });
      setSettings(savedSettings);
      applySettingsToState(savedSettings);
      setMessage(`Zapisano profil ekranow: ${profileName}.`);
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Nie udało się zapisać profilu ekranow.');
    }
  }

  async function setActivePcGamingProfile(profileName: string) {
    setError(null);
    try {
      const savedSettings = await sendBridgeCommand<AppSettings>('setActivePcGamingProfile', { profileName });
      setSettings(savedSettings);
      applySettingsToState(savedSettings);
      setMessage(`Aktywny profil Gaming Mode: ${profileName}.`);
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Nie udało się przełączyć profilu Gaming Mode.');
    }
  }

  async function clearPcGamingLayout(profileName?: string) {
    setError(null);
    try {
      const payload = profileName ? { profileName } : undefined;
      const savedSettings = await sendBridgeCommand<AppSettings>('clearPcGamingLayout', payload);
      setSettings(savedSettings);
      applySettingsToState(savedSettings);
      setMessage(profileName ? `Usunięto profil ekranow: ${profileName}.` : 'Usunięto uklad ekranow.');
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Nie udało się usunąć ukladu ekranow.');
    }
  }

  async function runModeCommand(command: ModeCommand, saveBeforeRun = false, payload?: unknown) {
    const actionKey = getModeActionKey(command);
    setActiveModeAction(actionKey);
    setError(null);
    setMessage(`Przełączanie na ${formatCommandTargetMode(command)}...`);

    try {
      if (saveBeforeRun) {
        await saveCurrentSettings(false);
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
        activeProfileName={activePcGamingProfileName}
        isModeSwitching={isModeSwitching}
        activeModeAction={activeModeAction}
        message={footerMessage}
        hasError={Boolean(error || status.lastError)}
        runModeCommand={runModeCommand}
        editSettings={(stepId) => {
          const targetIndex = steps.findIndex((step) => step.id === stepId);
          setIsEditingSection(true);
          setIsSetupComplete(false);
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
            {steps.map((step, index) => (
              <li className={`step-item ${index === stepIndex ? 'active' : ''} ${index < stepIndex ? 'done' : ''}`} key={step.id}>
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
                stepNumber={stepIndex + 1}
                displays={displays}
                pcGamingLayout={pcGamingLayout}
                pcGamingProfiles={pcGamingProfiles}
                activePcGamingProfileName={activePcGamingProfileName}
                isRefreshingDisplays={isRefreshingDisplays}
                displayListError={displayListError}
                refreshDisplays={refreshDisplays}
                saveSelectedPcGamingLayout={saveSelectedPcGamingLayout}
                setActivePcGamingProfile={setActivePcGamingProfile}
                clearPcGamingLayout={clearPcGamingLayout}
              />
            ) : null}
            {currentStep.id === 'launcher' ? <LauncherStep stepNumber={stepIndex + 1} launcher={launcher} setLauncher={setLauncher} /> : null}
            {currentStep.id === 'console-behaviour' ? (
              <ConsoleBehaviourStep
                stepNumber={stepIndex + 1}
                pcGamingProfiles={pcGamingProfiles}
                selectedProfileName={selectedPcGamingBehaviourProfileName}
                setSelectedProfileName={setSelectedPcGamingBehaviourProfileName}
                pcGamingBehaviour={pcGamingBehaviour}
                onChangeBehaviourFlag={updatePcGamingBehaviourFlag}
              />
            ) : null}
            {currentStep.id === 'work-behaviour' ? (
              <WorkBehaviourStep stepNumber={stepIndex + 1} behaviour={workBehaviour} setBehaviour={setWorkBehaviour} />
            ) : null}
            {currentStep.id === 'controller' ? <ControllerStep stepNumber={stepIndex + 1} hotkeys={hotkeys} setHotkeys={setHotkeys} /> : null}
            {currentStep.id === 'test' ? (
              <TestStep
                stepNumber={stepIndex + 1}
                hotkeys={hotkeys}
                hasPcGamingLayout={Boolean(pcGamingLayout)}
                isModeSwitching={isModeSwitching}
                activeModeAction={activeModeAction}
                runModeCommand={runModeCommand}
              />
            ) : null}
            {currentStep.id === 'finish' ? (
              <FinishStep
                stepNumber={stepIndex + 1}
                launcher={launcher}
                pcGamingLayout={pcGamingLayout}
                activeProfileName={activePcGamingProfileName}
                finishOptions={finishOptions}
                setFinishOptions={setFinishOptions}
              />
            ) : null}
          </section>

          <footer className="setup-footer">
            {isEditingSection ? (
              <>
                <div className="footer-actions-left">
                  <button className="secondary-button" type="button" disabled={!canGoBack || isSaving} onClick={goBack}>
                    Wróć
                  </button>
                  <button className="secondary-button" type="button" disabled={isSaving} onClick={returnToDashboard}>
                    Panel główny
                  </button>
                </div>
                <p className={error || status.lastError ? 'error-line' : 'operation-line'}>{footerMessage}</p>
                <div className="footer-actions-right">
                  <button className="secondary-button" type="button" disabled={isSaving || isLastStep} onClick={goNext}>
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
                  <button className="secondary-button" type="button" disabled={!canGoBack} onClick={goBack}>
                    Wróć
                  </button>
                </div>
                <p className={error || status.lastError ? 'error-line' : 'operation-line'}>{footerMessage}</p>
                <div className="footer-actions-right">
                  <button className="primary-button" type="button" disabled={isSaving} onClick={goNext}>
                    {isSaving ? 'Zapisywanie...' : isLastStep ? 'Zakończ' : 'Dalej'}
                  </button>
                </div>
              </>
            )}
          </footer>
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
  activeProfileName,
  isModeSwitching,
  activeModeAction,
  message,
  hasError,
  runModeCommand,
  editSettings
}: {
  status: AppStatus;
  launcher: LauncherType;
  pcGamingLayout: DisplayLayoutSnapshot | null;
  pcGamingProfiles: DisplayLayoutProfile[];
  activeProfileName: string | null;
  isModeSwitching: boolean;
  activeModeAction: string | null;
  message: string;
  hasError: boolean;
  runModeCommand: (command: ModeCommand, saveBeforeRun?: boolean, payload?: unknown) => Promise<void>;
  editSettings: (stepId: StepId) => void;
}) {
  const [selectedProfileName, setSelectedProfileName] = useState('');

  useEffect(() => {
    if (activeProfileName && pcGamingProfiles.some((profile) => profile.name === activeProfileName)) {
      setSelectedProfileName(activeProfileName);
      return;
    }

    setSelectedProfileName(pcGamingProfiles[0]?.name ?? '');
  }, [activeProfileName, pcGamingProfiles]);

  const selectedProfile = pcGamingProfiles.find((profile) => profile.name === selectedProfileName) ?? null;

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
                <dd>{launcher}</dd>
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
                  <button className={getModeCardClass('enterGameShellWithProfile', status, !selectedProfile)} type="button" disabled={isModeSwitching || !selectedProfile} onClick={() => runModeCommand('enterGameShellWithProfile', false, { profileName: selectedProfileName })}>
                    <span className="mode-card-kicker">Profil ekranow</span>
                    <strong>Gaming Mode</strong>
                    <small>{selectedProfile ? `${selectedProfile.name} · ${summarizeLayout(selectedProfile.layout)}` : 'Wybierz profil z listy.'}</small>
                    <span className="mode-card-state">{getModeCardStateLabel('enterGameShellWithProfile', status, !selectedProfile)}</span>
                    <em className="mode-card-action">{activeModeAction === getModeActionKey('enterGameShellWithProfile') ? 'Przełączanie...' : selectedProfile ? getModeCardHint('enterGameShellWithProfile', status) : 'Wybierz profil poniżej'}</em>
                  </button>
                  <button className={getModeCardClass('enterWorkMode', status)} type="button" disabled={isModeSwitching} onClick={() => runModeCommand('enterWorkMode')}>
                    <span className="mode-card-kicker">Powrót</span>
                    <strong>Work Mode</strong>
                    <small>Przywraca układ sprzed sesji gamingowej oraz pulpit.</small>
                    <span className="mode-card-state">{getModeCardStateLabel('enterWorkMode', status, false)}</span>
                    <em className="mode-card-action">{activeModeAction === getModeActionKey('enterWorkMode') ? 'Przełączanie...' : getModeCardHint('enterWorkMode', status)}</em>
                  </button>
                </div>
                <div className="field-grid">
                  <label>
                    Profil Gaming Mode
                    <select className="hotkey-input profile-select" value={selectedProfileName} onChange={(event) => setSelectedProfileName(event.target.value)}>
                      <option value="">Wybierz profil...</option>
                      {pcGamingProfiles.map((profile) => (
                        <option key={profile.name} value={profile.name}>{profile.name}</option>
                      ))}
                    </select>
                  </label>
                </div>
              </section>

              <section className="dashboard-band" aria-labelledby="dashboard-config-title">
                <div className="dashboard-section-head compact">
                  <div>
                    <p className="eyebrow">Konfiguracja</p>
                    <h2 id="dashboard-config-title">Dostosuj ustawienia</h2>
                  </div>
                </div>
                <div className="section-shortcuts" aria-label="Edycja konfiguracji">
                  <button type="button" onClick={() => editSettings('gaming-display')}>
                    <span>Ekrany</span>
                    <small>Profile ekranow i aktywny uklad do grania</small>
                  </button>
                  <button type="button" onClick={() => editSettings('launcher')}>
                    <span>Launcher gier</span>
                    <small>{launcher}</small>
                  </button>
                  <button type="button" onClick={() => editSettings('console-behaviour')}>
                    <span>Zachowanie trybów gamingowych</span>
                    <small>Ikony, taskbar, okna i kursor</small>
                  </button>
                  <button type="button" onClick={() => editSettings('work-behaviour')}>
                    <span>Zachowanie Work Mode</span>
                    <small>Przywracanie pulpitu po graniu</small>
                  </button>
                  <button type="button" onClick={() => editSettings('controller')}>
                    <span>Skróty klawiatury i kontrolera</span>
                    <small>Hotkeye i przyszłe mapowanie pada</small>
                  </button>
                  <button type="button" onClick={() => editSettings('finish')}>
                    <span>Uruchamianie aplikacji</span>
                    <small>Autostart, tray i skróty systemowe</small>
                  </button>
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
  pcGamingLayout,
  pcGamingProfiles,
  activePcGamingProfileName,
  isRefreshingDisplays,
  displayListError,
  refreshDisplays,
  saveSelectedPcGamingLayout,
  setActivePcGamingProfile,
  clearPcGamingLayout
}: {
  stepNumber: number;
  displays: DisplayDeviceInfo[];
  pcGamingLayout: DisplayLayoutSnapshot | null;
  pcGamingProfiles: DisplayLayoutProfile[];
  activePcGamingProfileName: string | null;
  isRefreshingDisplays: boolean;
  displayListError: string | null;
  refreshDisplays: () => Promise<void>;
  saveSelectedPcGamingLayout: (profileName: string, identities: DisplayIdentity[]) => Promise<void>;
  setActivePcGamingProfile: (profileName: string) => Promise<void>;
  clearPcGamingLayout: (profileName?: string) => Promise<void>;
}) {
  const [newProfileName, setNewProfileName] = useState('');
  const [selectedDisplays, setSelectedDisplays] = useState<DisplayIdentity[]>([]);

  const maxProfileCount = calculateProfileCombinationLimit(displays.length);
  const canCreateMoreProfiles = maxProfileCount > 0 && pcGamingProfiles.length < maxProfileCount;
  const normalizedProfileName = newProfileName.trim().toLowerCase();
  const profileNameExists = pcGamingProfiles.some((profile) => profile.name.toLowerCase() === normalizedProfileName);
  const profileCombinationExists = hasSameDisplayCombination(selectedDisplays, pcGamingProfiles);
  const canSaveProfileName = Boolean(newProfileName.trim())
    && selectedDisplays.length > 0
    && canCreateMoreProfiles
    && !profileNameExists
    && !profileCombinationExists;

  useEffect(() => {
    const activeProfile = resolveActivePcGamingProfile(activePcGamingProfileName, pcGamingProfiles);
    setSelectedDisplays(activeProfile ? extractDisplayIdentities(activeProfile.layout) : []);
  }, [activePcGamingProfileName, pcGamingProfiles]);

  function toggleSelectedDisplay(identity: DisplayIdentity) {
    setSelectedDisplays((current) => current.some((selected) => sameIdentity(selected, identity))
      ? current.filter((selected) => !sameIdentity(selected, identity))
      : [...current, identity]);
  }

  function getProfileNameOrNull() {
    const trimmed = newProfileName.trim();
    return trimmed.length > 0 ? trimmed : null;
  }

  async function handleSaveProfile() {
    const profileName = getProfileNameOrNull();
    if (!profileName) {
      return;
    }

    await saveSelectedPcGamingLayout(profileName, selectedDisplays);
    setNewProfileName('');
  }

  return (
    <div className="wizard-copy displays-step">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Ekrany</h2>
      <section className="settings-section">
        <h3>Wykryte ekrany</h3>
        <p>Kliknij ekrany, aby zbudować profil. Zaznaczone karty są zielone, a niezaznaczone pozostają tylko podglądem stanu podłączenia.</p>
        <DisplayDiagnosticsPanel
          displays={displays}
          selectedDisplays={selectedDisplays}
          onToggleDisplay={toggleSelectedDisplay}
          isRefreshing={isRefreshingDisplays}
          error={displayListError}
          refreshDisplays={refreshDisplays}
          profileName={newProfileName}
          setProfileName={setNewProfileName}
          saveProfile={handleSaveProfile}
          canSaveProfile={canSaveProfileName}
        />
      </section>

      <section className="settings-section">
        <h3>Zapisane profile</h3>
        <p className="hint-line">{pcGamingLayout ? `${activePcGamingProfileName || 'Aktywny profil'} · ${summarizeLayout(pcGamingLayout)}` : 'Brak aktywnego profilu Gaming Mode.'}</p>
        <p className="hint-line">Maksymalnie: {maxProfileCount} profili.</p>
        {profileNameExists ? <p className="error-line">Profil o tej nazwie już istnieje.</p> : null}
        {profileCombinationExists ? <p className="error-line">Profil z taką samą kombinacją ekranów już istnieje.</p> : null}

        {pcGamingProfiles.length > 0 ? (
          <div className="profile-list" role="list" aria-label="Profile ekranow">
            {pcGamingProfiles.map((profile) => {
              const isActive = activePcGamingProfileName === profile.name;
              return (
                <article className={`profile-item ${isActive ? 'active' : ''}`} key={profile.name} role="listitem" onClick={() => void setActivePcGamingProfile(profile.name)}>
                  <div className="profile-item-main">
                    <strong>{profile.name}</strong>
                    <small>{summarizeLayout(profile.layout)}</small>
                  </div>
                  <div className="profile-item-actions">
                    <span className="profile-item-state">{isActive ? 'Aktywny profil' : 'Kliknij, aby ustawic'}</span>
                    <button
                      className="secondary-button compact"
                      type="button"
                      onClick={(event) => {
                        event.stopPropagation();
                        void clearPcGamingLayout(profile.name);
                      }}
                    >
                      Usuń
                    </button>
                  </div>
                </article>
              );
            })}
          </div>
        ) : null}
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
  saveProfile,
  canSaveProfile
}: {
  displays: DisplayDeviceInfo[];
  selectedDisplays: DisplayIdentity[];
  onToggleDisplay: (identity: DisplayIdentity) => void;
  isRefreshing: boolean;
  error: string | null;
  refreshDisplays: () => Promise<void>;
  profileName: string;
  setProfileName: (value: string) => void;
  saveProfile: () => Promise<void>;
  canSaveProfile: boolean;
}) {
  return (
    <section className="display-diagnostics" aria-labelledby="display-diagnostics-title">
      <div className="display-diagnostics-head">
        <div>
          <h3 id="display-diagnostics-title">Wykryte ekrany</h3>
          <p>Monitory widziane przez Windows, także wyłączone w ustawieniach ekranów.</p>
        </div>
        <div className="display-diagnostics-actions">
          <button className="primary-button compact" type="button" disabled={!canSaveProfile} onClick={() => void saveProfile()}>
            Zapisz profil ekranow
          </button>
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
                <details onClick={(event) => event.stopPropagation()}>
                  <summary>Identyfikator</summary>
                  <code>{shortDisplayIdentifier(display)}</code>
                </details>
              </article>
            );
          })}
        </div>
      ) : null}
    </section>
  );
}

function LauncherStep({ stepNumber, launcher, setLauncher }: { stepNumber: number; launcher: LauncherType; setLauncher: (launcher: LauncherType) => void }) {
  const launchers: Array<{ value: LauncherType; title: string; description: string; badge: string }> = [
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
  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Wybierz launcher</h2>
      <p>Launcher zostanie uruchomiony po wejściu w Gaming Mode, jeśli w zachowaniu trybu włączysz jego start.</p>
      <div className="launcher-grid" aria-label="Launcher">
        {launchers.map((item) => (
          <button className={`launcher-card ${launcher === item.value ? 'selected' : ''}`} key={item.value} type="button" onClick={() => setLauncher(item.value)}>
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
  setSelectedProfileName,
  pcGamingBehaviour,
  onChangeBehaviourFlag
}: {
  stepNumber: number;
  pcGamingProfiles: DisplayLayoutProfile[];
  selectedProfileName: string;
  setSelectedProfileName: (profileName: string) => void;
  pcGamingBehaviour: PcGamingBehaviourState;
  onChangeBehaviourFlag: (key: PcGamingBehaviourKey, value: boolean) => void;
}) {
  const hasProfiles = pcGamingProfiles.length > 0;

  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Zachowanie Gaming Mode</h2>
      <section className="settings-section gaming-behaviour-section">
        <h3>Gaming Mode</h3>
        <p>Wybierz profil, a następnie ustaw checkboxy dla tego konkretnego profilu.</p>
        <div className="field-grid">
          <label>
            Profil Gaming Mode
            <select
              className="hotkey-input profile-select"
              value={selectedProfileName}
              onChange={(event) => setSelectedProfileName(event.target.value)}
              disabled={!hasProfiles}
            >
              {!hasProfiles ? <option value="">Brak zapisanych profili</option> : null}
              {pcGamingProfiles.map((profile) => (
                <option key={profile.name} value={profile.name}>{profile.name}</option>
              ))}
            </select>
          </label>
        </div>
        <div className="check-list">
          {pcGamingBehaviourOptions.map((option) => (
            <CheckRow
              key={option.key}
              label={option.label}
              checked={pcGamingBehaviour[option.key]}
              onChange={(value) => onChangeBehaviourFlag(option.key, value)}
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
      <p>Work Mode przywraca poprzedni układ ekranów oraz normalny pulpit.</p>
      <div className="check-list">
        <CheckRow label="Zamknij launcher" checked={behaviour.closeLauncher} onChange={(value) => setBehaviour({ ...behaviour, closeLauncher: value })} />
        <CheckRow label="Pokaż paski zadań" checked={behaviour.showTaskbars} onChange={(value) => setBehaviour({ ...behaviour, showTaskbars: value })} />
        <CheckRow label="Pokaż ikony pulpitu" checked={behaviour.showDesktopIcons} onChange={(value) => setBehaviour({ ...behaviour, showDesktopIcons: value })} />
        <CheckRow label="Nie minimalizuj istniejących okien" checked={behaviour.doNotMinimizeWindows} onChange={(value) => setBehaviour({ ...behaviour, doNotMinimizeWindows: value })} />
      </div>
    </div>
  );
}

function ControllerStep({ stepNumber, hotkeys, setHotkeys }: { stepNumber: number; hotkeys: HotkeySettingsState; setHotkeys: (hotkeys: HotkeySettingsState) => void }) {
  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2> Skróty klawiatury i kontrolera</h2>
      <section className="settings-section">
        <h3>Skróty klawiaturowe</h3>
        <p>Globalne skróty klawiaturowe uruchamiają pełne workflow GameShell.</p>
        <div className="field-grid">
          <label>
            Hotkey Gaming Mode
            <HotkeyInput value={hotkeys.pcGamingMode} onChange={(value) => setHotkeys({ ...hotkeys, pcGamingMode: value })} placeholder="np. Ctrl+Alt+P" />
          </label>
          <label>
            Hotkey Work Mode
            <HotkeyInput value={hotkeys.workMode} onChange={(value) => setHotkeys({ ...hotkeys, workMode: value })} placeholder="np. Ctrl+Alt+W" />
          </label>
        </div>
      </section>
    </div>
  );
}

function TestStep({
  stepNumber,
  hotkeys,
  hasPcGamingLayout,
  isModeSwitching,
  activeModeAction,
  runModeCommand
}: {
  stepNumber: number;
  hotkeys: HotkeySettingsState;
  hasPcGamingLayout: boolean;
  isModeSwitching: boolean;
  activeModeAction: string | null;
  runModeCommand: (command: ModeCommand, saveBeforeRun?: boolean) => Promise<void>;
}) {
  return (
    <div className="wizard-copy">
      <p className="eyebrow">Krok {stepNumber}</p>
      <h2>Test pełnych trybów</h2>
      <section className="test-group full-mode-test-group">
        <h3>Pełne workflow GameShell</h3>
        <p>Uruchamia układ ekranów oraz wszystkie skonfigurowane akcje: ikony pulpitu, taskbar, launcher i obsługę okien.</p>
        <div className="mode-test-grid">
          <div className="mode-test-option">
            <button className="primary-button mode-action" type="button" disabled={isModeSwitching || !hasPcGamingLayout} onClick={() => runModeCommand('enterGameShellWithProfile', true)}>
              {activeModeAction === getModeActionKey('enterGameShellWithProfile') ? 'Przełączanie...' : 'Przetestuj Gaming Mode'}
            </button>
            <span className="mode-test-hotkey">{hotkeys.pcGamingMode || 'Brak przypisanego hotkeya'}</span>
            <p>Uruchamia aktywny zapisany profil ekranow oraz skonfigurowane zachowanie gamingowe.</p>
          </div>
          <div className="mode-test-option">
            <button className="primary-button mode-action" type="button" disabled={isModeSwitching} onClick={() => runModeCommand('enterWorkMode', true)}>
              {activeModeAction === getModeActionKey('enterWorkMode') ? 'Przełączanie...' : 'Przetestuj Work Mode'}
            </button>
            <span className="mode-test-hotkey">{hotkeys.workMode || 'Brak przypisanego hotkeya'}</span>
            <p>Przywraca poprzedni układ ekranów i pulpit do pracy.</p>
          </div>
        </div>
      </section>
    </div>
  );
}

function FinishStep({
  stepNumber,
  launcher,
  pcGamingLayout,
  activeProfileName,
  finishOptions,
  setFinishOptions
}: {
  stepNumber: number;
  launcher: LauncherType;
  pcGamingLayout: DisplayLayoutSnapshot | null;
  activeProfileName: string | null;
  finishOptions: { startWithWindows: boolean; startMinimized: boolean; startMenuShortcut: boolean; desktopShortcut: boolean };
  setFinishOptions: (options: { startWithWindows: boolean; startMinimized: boolean; startMenuShortcut: boolean; desktopShortcut: boolean }) => void;
}) {
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
          <dd>{launcher}</dd>
        </div>
      </dl>
      <div className="check-list">
        <CheckRow label="Startuj z Windows" checked={finishOptions.startWithWindows} onChange={(value) => setFinishOptions({ ...finishOptions, startWithWindows: value })} />
        <CheckRow label="Startuj zminimalizowany do tray" checked={finishOptions.startMinimized} onChange={(value) => setFinishOptions({ ...finishOptions, startMinimized: value })} />
        <CheckRow label="Utwórz skrót w menu Start" checked={finishOptions.startMenuShortcut} onChange={(value) => setFinishOptions({ ...finishOptions, startMenuShortcut: value })} />
        <CheckRow label="Utwórz skrót na pulpicie" checked={finishOptions.desktopShortcut} onChange={(value) => setFinishOptions({ ...finishOptions, desktopShortcut: value })} />
      </div>
    </div>
  );
}

function CheckRow({ label, checked, onChange }: { label: string; checked: boolean; onChange: (checked: boolean) => void }) {
  return (
    <label className="check-row">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      <span>{label}</span>
    </label>
  );
}

function HotkeyInput({ value, onChange, placeholder }: { value: string; onChange: (value: string) => void; placeholder: string }) {
  return (
    <input
      className="hotkey-input"
      value={value}
      readOnly
      placeholder={placeholder}
      onFocus={(event) => event.currentTarget.select()}
      onKeyDown={(event) => {
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

function getModeCardClass(command: ModeCommand, status: AppStatus, isUnconfigured = false) {
  const classes = ['mode-card'];
  if (isCurrentMode(command, status)) {
    classes.push('active');
  } else if (isUnconfigured) {
    classes.push('unconfigured');
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
  savedBehaviours: NonNullable<AppSettings['display']['pcGamingProfileBehaviours']>,
  fallbackBehaviour: PcGamingBehaviourState
): PcGamingProfileBehaviourMap {
  const map: PcGamingProfileBehaviourMap = {};

  profiles.forEach((profile) => {
    const saved = savedBehaviours.find((entry) => entry.profileName.toLowerCase() === profile.name.toLowerCase());
    map[profile.name] = toPcGamingBehaviourState(saved?.behaviour ?? fallbackBehaviour);
  });

  return map;
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

function calculateProfileCombinationLimit(displayCount: number) {
  if (displayCount <= 0) {
    return 0;
  }

  if (displayCount >= 31) {
    return Number.MAX_SAFE_INTEGER;
  }

  return (2 ** displayCount) - 1;
}

function hasSameDisplayCombination(identities: DisplayIdentity[], profiles: DisplayLayoutProfile[]) {
  if (identities.length === 0) {
    return false;
  }

  return profiles.some((profile) => {
    const profileIdentities = extractDisplayIdentities(profile.layout);
    if (profileIdentities.length !== identities.length) {
      return false;
    }

    return identities.every((identity) => profileIdentities.some((profileIdentity) => sameIdentity(identity, profileIdentity)));
  });
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

function shortDisplayIdentifier(display: DisplayDeviceInfo) {
  const path = display.identity.monitorDevicePath || display.id || 'brak ścieżki monitora';
  return [
    path,
    `GDI: ${display.sourceDeviceName || 'brak'}`,
    `Adapter: ${display.adapterLuid}`,
    `Source: ${display.sourceId}`,
    `Target: ${display.targetId}`,
    `EDID: ${display.identity.edidManufacturerId || 'brak'} / ${display.identity.edidProductCodeId || 'brak'}`,
    `Connector: ${display.identity.connectorInstance || 'brak'}`
  ].join('\n');
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
