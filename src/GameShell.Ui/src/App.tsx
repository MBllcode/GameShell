import { useCallback, useEffect, useRef, useState } from 'react';
import { useProfileEditingFeature } from './features/profiles/hooks/useProfileEditingFeature';
import { useDisplayProfileWorkflow } from './features/profiles/hooks/useDisplayProfileWorkflow';
import { useDashboardFeature } from './features/dashboard/hooks/useDashboardFeature';
import { useSettingsIntegrationFeature } from './features/settings/hooks/useSettingsIntegrationFeature';
import { useSetupWorkflowState } from './features/setup/hooks/useSetupWorkflowState';
import { setI18nLanguage } from './i18n/i18n';
import { useSetupNavigationFeature } from './features/setup/hooks/useSetupNavigationFeature';
import { useSetupPageActions } from './features/setup/hooks/useSetupPageActions';
import { useSetupFeature } from './features/setup/hooks/useSetupFeature';
import { useDisplayConfigurationState } from './features/profiles/hooks/useDisplayConfigurationState';
import { steps } from './features/setup/setupFlow';
import { useSettingsFormState } from './features/settings/hooks/useSettingsFormState';
import { useAppRuntimeState } from './features/app/hooks/useAppRuntimeState';
import { useSetupFlowController } from './features/setup/hooks/useSetupFlowController';
import { AppView } from './AppView';
import { resolvePreferredProfileNameForPostSaveSelection } from './features/profiles/utils';
import { normalizeGamingProfileName } from './features/profiles/utils';
import { areTopologiesEqual, getProfileTopology, normalizeCanvasTopology, topologyFromLegacy, topologyToLegacy } from './utils/displays';
import { createSettingsComparisonKey } from './features/settings/settingsComparison';
import { resolveWorkingProfileName } from './features/setup/setupRouting';

const FOOTNOTE_ERROR_DURATION_MS = 6000;

export function App() {
  const runtimeState = useAppRuntimeState();
  const { status, setStatus, settings, setSettings, isRemovingProfile, setIsRemovingProfile, activeModeAction, setActiveModeAction } = runtimeState;
  const setupState = useSetupWorkflowState();
  const { stepIndex, setStepIndex, message, setMessage, setTransientMessage, error, setError, isSaving, setIsSaving, isInitialLoading, setIsInitialLoading, setupMode, setSetupMode, isHydrated, setIsHydrated, bootstrapRoute, setBootstrapRoute, editingProfileId, setEditingProfileId, isSetupComplete, setIsSetupComplete, hasAdvancedInCreateProfileFlow, setHasAdvancedInCreateProfileFlow, hasUnsavedChanges, setHasUnsavedChanges, pendingNavigationAction, setPendingNavigationAction, showUnsavedDialog, setShowUnsavedDialog, showOverwriteProfileDialog, setShowOverwriteProfileDialog, pendingOverwriteProfileAction, setPendingOverwriteProfileAction, displayProfileDraft, displayValidationState, setDisplayValidationState, displayProfileDraftRef, pendingSettingsOverrideRef, syncDisplayProfileDraft, markUnsavedChanges, handleDisplayDraftChange, handleDisplayValidationStateChange } = setupState;
  const isVeryFirstConfiguration = setupMode === 'FirstRun';
  const isEditingSection = setupMode === 'EditProfile' || setupMode === 'EditGlobal';
  const isCreatingProfileFlow = setupMode === 'CreateProfile';
  const [selectedGamingBehaviourProfileName, setSelectedGamingBehaviourProfileName] = useState('');
  const persistedSettingsBaselineRef = useRef<string | null>(null);
  const workingProfileName = resolveWorkingProfileName(setupMode, displayProfileDraft.profileName, selectedGamingBehaviourProfileName);
  const displayState = useDisplayConfigurationState({ selectedProfileName: selectedGamingBehaviourProfileName });
  const { displays, setDisplays, isRefreshingDisplays, setIsRefreshingDisplays, displayListError, setDisplayListError, selectedConsoleDisplay, setSelectedConsoleDisplay, selectedConsoleDisplays, setSelectedConsoleDisplays, gamingLayout, setGamingLayout, workModeLayout, setWorkModeLayout, gamingProfiles, setGamingProfiles, activeGamingProfileName, setActiveGamingProfileName, gamingProfileBehaviours, setGamingProfileBehaviours, gamingProfileLaunchers, setGamingProfileLaunchers, gamingProfileHotkeys, setGamingProfileHotkeys, gamingProfileControllerShortcuts, setGamingProfileControllerShortcuts, workModeControllerShortcut, setWorkModeControllerShortcut, profileHotkeyValidationError, setProfileHotkeyValidationError, profileControllerValidationError, setProfileControllerValidationError } = displayState;
  const formState = useSettingsFormState();
  const { hotkeys, setHotkeys, launcher, setLauncher, language, setLanguage, consoleBehaviour, setConsoleBehaviour, gamingBehaviour, setGamingBehaviour, workBehaviour, setWorkBehaviour, finishOptions, setFinishOptions } = formState;

  useEffect(() => {
    setI18nLanguage(language);
  }, [language]);

  const profileFeature = useProfileEditingFeature({
    workingProfileName,
    gamingBehaviour,
    profileBehaviours: gamingProfileBehaviours,
    profileLaunchers: gamingProfileLaunchers,
    profileHotkeys: gamingProfileHotkeys,
    profileControllerShortcuts: gamingProfileControllerShortcuts,
    workModeControllerShortcut,
    workModeHotkey: hotkeys.workMode,
    setGamingBehaviour,
    setProfileBehaviours: setGamingProfileBehaviours,
    setLauncher,
    setProfileLaunchers: setGamingProfileLaunchers,
    setProfileHotkeys: setGamingProfileHotkeys,
    setProfileControllerShortcuts: setGamingProfileControllerShortcuts,
    setWorkModeControllerShortcut,
    setHotkeys,
    setProfileHotkeyValidationError,
    setProfileControllerValidationError,
    setError,
    markUnsavedChanges
  });
  const { mutations: profileMutations, controllerCapture } = profileFeature;

  const setupPageActions = useSetupPageActions({
    workingProfileName,
    setLauncher,
    setWorkBehaviour,
    setFinishOptions,
    markUnsavedChanges,
    setProfileLauncher: profileMutations.setProfileLauncher,
    updateProfileHotkey: profileMutations.updateProfileHotkey,
    updateProfileControllerShortcut: profileMutations.updateProfileControllerShortcut,
    updateWorkModeHotkey: profileMutations.updateWorkModeHotkey,
    updateWorkModeControllerShortcut: profileMutations.updateWorkModeControllerShortcut,
    startControllerCapture: controllerCapture.startCapture,
    cancelControllerCapture: controllerCapture.cancelCapture
  });

  const displayWorkflow = useDisplayProfileWorkflow({
    displayProfileDraftRef,
    displays,
    profiles: gamingProfiles,
    selectedProfileName: workingProfileName,
    isCreatingProfileFlow,
    isFirstRun: isVeryFirstConfiguration,
    isEditingSection,
    gamingBehaviour,
    launcher,
    hotkeys,
    profileBehaviours: gamingProfileBehaviours,
    profileLaunchers: gamingProfileLaunchers,
    profileHotkeys: gamingProfileHotkeys,
    profileControllerShortcuts: gamingProfileControllerShortcuts,
    setDisplays,
    setIsRefreshingDisplays,
    setDisplayListError,
    setDisplayValidationState,
    setProfiles: setGamingProfiles,
    setActiveProfileName: setActiveGamingProfileName,
    setLayout: setGamingLayout,
    setSelectedProfileName: setSelectedGamingBehaviourProfileName,
    setProfileBehaviours: setGamingProfileBehaviours,
    setProfileLaunchers: setGamingProfileLaunchers,
    setProfileHotkeys: setGamingProfileHotkeys,
    setProfileControllerShortcuts: setGamingProfileControllerShortcuts,
    pendingSettingsOverrideRef,
    setPendingOverwriteProfileAction,
    setShowOverwriteProfileDialog,
    markUnsavedChanges,
    setError,
    setMessage
  });

  const flow = useSetupFlowController({
    stepIndex,
    setupMode,
    activeGamingProfileName,
    selectedProfileName: workingProfileName,
    profiles: gamingProfiles,
    profileBehaviours: gamingProfileBehaviours,
    profileLaunchers: gamingProfileLaunchers,
    profileHotkeys: gamingProfileHotkeys,
    gamingBehaviour,
    launcher,
    setSelectedConsoleDisplays,
    setSelectedConsoleDisplay,
    setSelectedProfileName: setSelectedGamingBehaviourProfileName,
    setGamingBehaviour,
    setLauncher,
    setHotkeys,
    displays,
    selectedConsoleDisplays,
    selectedConsoleDisplay,
    controllerCapture
  });
  const { activeSteps, activeStepIndexes, currentStep, resolvedFlowPosition, isNonFirstRunEditingSession, isLastStep, canGoBack } = flow;
  const isModeSwitching = activeModeAction !== null;

  useEffect(() => {
    if (!error && !status.lastError) return;

    const timer = window.setTimeout(() => {
      setError(null);
      setStatus((current) => current.lastError === status.lastError && status.lastError !== null
        ? { ...current, lastError: null }
        : current);
    }, FOOTNOTE_ERROR_DURATION_MS);

    return () => window.clearTimeout(timer);
  }, [error, setError, setStatus, status.lastError]);

  const footerMessage = error || status.lastError || message;
  const canSelectSidebarStep = !isVeryFirstConfiguration && !isSaving;
  const handleWorkModeDraftChange = useCallback((topology: Parameters<typeof topologyToLegacy>[0]) => {
    setWorkModeLayout(topologyToLegacy(topology, displays));
  }, [displays, setWorkModeLayout]);

  const settingsIntegration = useSettingsIntegrationFeature({
    settings,
    selectedConsoleDisplay,
    selectedConsoleDisplays,
    workModeLayout,
    gamingLayout,
    gamingProfiles,
    activeGamingProfileName,
    gamingProfileBehaviours,
    gamingProfileLaunchers,
    gamingProfileHotkeys,
    gamingProfileControllerShortcuts,
    workModeControllerShortcut,
    gamingBehaviour,
    launcher,
    hotkeys,
    language,
    consoleBehaviour,
    workBehaviour,
    finishOptions,
    isSetupComplete,
    bootstrapRoute,
    setEditingProfileId,
    setStepIndex,
    gamingProfilesCount: gamingProfiles.length,
    preferredProfileName: () => resolvePreferredProfileNameForPostSaveSelection(displayProfileDraftRef.current.profileName, selectedGamingBehaviourProfileName),
    pendingSettingsOverrideRef,
    setSelectedConsoleDisplays,
    setWorkModeLayout,
    setSelectedConsoleDisplay,
    setProfiles: setGamingProfiles,
    setActiveProfileName: setActiveGamingProfileName,
    setProfileBehaviours: setGamingProfileBehaviours,
    setProfileLaunchers: setGamingProfileLaunchers,
    setProfileHotkeys: setGamingProfileHotkeys,
    setProfileControllerShortcuts: setGamingProfileControllerShortcuts,
    setWorkModeControllerShortcut,
    setSelectedProfileName: setSelectedGamingBehaviourProfileName,
    setLayout: setGamingLayout,
    setHotkeys,
    setLauncher,
    setLanguage,
    setConsoleBehaviour,
    setGamingBehaviour,
    setWorkBehaviour,
    setFinishOptions,
    setupMode,
    setIsSetupComplete,
    hasUnsavedChanges,
    setHasUnsavedChanges,
    setSettings,
    setStatus,
    setSetupMode,
    setIsHydrated,
    setBootstrapRoute,
    setIsSaving,
    setError,
    setMessage,
    applyControllerStatus: controllerCapture.applyStatus,
    setDisplays,
    setDisplayListError,
    setInitialLoading: setIsInitialLoading
  });
  const { applySettingsToState, buildSettingsPayload, saveCurrentSettings, persistAndReturnToDashboard } = settingsIntegration;

  useEffect(() => {
    if (!isHydrated || !settings) return;

    const draftHasContent = Boolean(displayProfileDraft.profileName.trim()) || displayProfileDraft.topology.displays.length > 0;
    let hasDisplayDraftChanges = false;
    if (draftHasContent && setupMode === 'CreateProfile') {
      hasDisplayDraftChanges = true;
    } else if (draftHasContent && setupMode === 'EditProfile') {
      const editedProfileName = editingProfileId || selectedGamingBehaviourProfileName;
      const editedProfile = gamingProfiles.find((profile) => normalizeGamingProfileName(profile.name) === normalizeGamingProfileName(editedProfileName));
      hasDisplayDraftChanges = !editedProfile
        || normalizeGamingProfileName(displayProfileDraft.profileName) !== normalizeGamingProfileName(editedProfile.name)
        || !areTopologiesEqual(displayProfileDraft.topology, getProfileTopology(editedProfile));
    }

    const savedWorkModeTopology = settings.workMode.defaultDisplayLayout
      ? normalizeCanvasTopology(topologyFromLegacy(settings.workMode.defaultDisplayLayout))
      : { displays: [] };
    const draftWorkModeTopology = workModeLayout
      ? normalizeCanvasTopology(topologyFromLegacy(workModeLayout))
      : { displays: [] };
    const hasWorkModeLayoutChanges = !areTopologiesEqual(draftWorkModeTopology, savedWorkModeTopology);

    const candidate = buildSettingsPayload(settings.ui?.isFirstRunCompleted ?? false, pendingSettingsOverrideRef.current ?? undefined);
    const candidateForPersistedComparison = {
      ...candidate,
      workMode: {
        ...candidate.workMode,
        defaultDisplayLayout: settings.workMode.defaultDisplayLayout ?? null
      }
    };
    const candidateKey = createSettingsComparisonKey(candidateForPersistedComparison);

    // The bridge payload can contain legacy/default fields that are normalized by
    // buildSettingsPayload. Establish the baseline from that canonical payload
    // after hydration instead of comparing two differently-shaped objects.
    if (setupMode === 'None') {
      persistedSettingsBaselineRef.current = candidateKey;
      if (hasUnsavedChanges) setHasUnsavedChanges(false);
      return;
    }

    if (persistedSettingsBaselineRef.current === null) {
      persistedSettingsBaselineRef.current = candidateKey;
      return;
    }

    const hasPersistedStateChanges = persistedSettingsBaselineRef.current !== candidateKey;
    const nextDirtyState = hasDisplayDraftChanges || hasWorkModeLayoutChanges || hasPersistedStateChanges;
    if (hasUnsavedChanges !== nextDirtyState) setHasUnsavedChanges(nextDirtyState);
  }, [
    buildSettingsPayload,
    displayProfileDraft,
    editingProfileId,
    gamingProfiles,
    hasUnsavedChanges,
    isHydrated,
    selectedGamingBehaviourProfileName,
    setHasUnsavedChanges,
    settings,
    setupMode,
    workModeLayout
  ]);

  const dashboardPage = useDashboardFeature({
    status,
    launcher,
    gamingLayout,
    gamingProfiles,
    gamingProfileLaunchers,
    gamingProfileHotkeys,
    activeGamingProfileName,
    selectedProfileName: selectedGamingBehaviourProfileName,
    globalWorkModeHotkey: hotkeys.workMode,
    isModeSwitching,
    isRemovingProfile,
    activeModeAction,
    message: footerMessage,
    hasError: Boolean(error || status.lastError),
    hasUnsavedChanges,
    setHasUnsavedChanges,
    isSetupComplete,
    profileBehaviours: gamingProfileBehaviours,
    profileLaunchers: gamingProfileLaunchers,
    profileHotkeys: gamingProfileHotkeys,
    profileControllerShortcuts: gamingProfileControllerShortcuts,
    saveCurrentSettings,
    setStatus,
    setActiveModeAction,
    setIsRemovingProfile,
    setActiveProfileName: setActiveGamingProfileName,
    setSelectedProfileName: setSelectedGamingBehaviourProfileName,
    setLayout: setGamingLayout,
    setError,
    setMessage,
    setTransientMessage,
    syncDisplayProfileDraft,
    setStepIndex,
    setSetupMode,
    setHasAdvancedInCreateProfileFlow,
    setIsSetupComplete,
    setProfileHotkeyValidationError,
    setProfileControllerValidationError,
    setHotkeys,
    setGamingBehaviour,
    setShowOverwriteProfileDialog,
    setBootstrapRoute,
    setEditingProfileId
  });

  const setupNavigationFeature = useSetupNavigationFeature({
    setupMode,
    hasAdvancedInCreateProfileFlow,
    hasUnsavedChanges,
    hasShortcutValidationError: Boolean(profileHotkeyValidationError || profileControllerValidationError),
    isLastStep,
    canSelectSidebarStep,
    currentStepId: currentStep.id,
    stepIndex,
    activeStepIndexes,
    resolvedFlowPosition,
    isSetupComplete,
    pendingNavigationAction,
    pendingOverwriteProfileAction,
    settings,
    pendingSettingsOverrideRef,
    saveCurrentSettings,
    persistAndReturnToDashboard,
    commitGamingDisplayDraft: displayWorkflow.commitGamingDisplayDraft,
    resolveStepIndex: (stepId) => steps.findIndex((step) => step.id === stepId),
    applySettingsToState,
    resetDisplayProfileDraft: () => syncDisplayProfileDraft({ profileName: '', topology: { displays: [] } }),
    setPendingNavigationAction,
    setPendingOverwriteProfileAction,
    setShowOverwriteProfileDialog,
    setShowUnsavedDialog,
    setError,
    setMessage,
    setStepIndex,
    setHasAdvancedInCreateProfileFlow,
    setSetupMode,
    setBootstrapRoute,
    setEditingProfileId,
    setIsSetupComplete,
    setIsSaving,
    setHasUnsavedChanges
  });
  const { saveAndReturnToDashboard, navigation } = setupNavigationFeature;

  const setupPage = useSetupFeature({
    setupState,
    displayState,
    formState,
    profileFeature,
    setupPageActions,
    displayWorkflow,
    navigation,
    saveAndReturnToDashboard,
    flow,
    footerMessage,
    status,
    selectedProfileName: workingProfileName,
    workModeLayout,
    workModeSavedLayout: settings?.workMode?.defaultDisplayLayout ?? null,
    onWorkModeDraftChange: handleWorkModeDraftChange
  });

  return <AppView
    isInitialLoading={isInitialLoading}
    isHydrated={isHydrated}
    bootstrapRoute={bootstrapRoute}
    dashboard={dashboardPage}
    setup={setupPage}
  />;
}
