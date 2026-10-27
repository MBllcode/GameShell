import { useCallback, useEffect, useRef, useState } from 'react';
import { useProfileEditingFeature } from '../features/profiles/hooks/useProfileEditingFeature';
import { useDisplayProfileWorkflow } from '../features/profiles/hooks/useDisplayProfileWorkflow';
import { useDashboardFeature } from '../features/dashboard/hooks/useDashboardFeature';
import { useSettingsIntegrationFeature } from '../features/settings/hooks/useSettingsIntegrationFeature';
import { useSetupWorkflowState } from '../features/setup/hooks/useSetupWorkflowState';
import { setI18nLanguage } from '../i18n/i18n';
import { useSetupNavigationFeature } from '../features/setup/hooks/useSetupNavigationFeature';
import { useSetupPageActions } from '../features/setup/hooks/useSetupPageActions';
import { useSetupFeature } from '../features/setup/hooks/useSetupFeature';
import { useDisplayConfigurationState } from '../features/profiles/hooks/useDisplayConfigurationState';
import { steps } from '../features/setup/setupFlow';
import { defaultGamingBehaviour, useSettingsFormState } from '../features/settings/hooks/useSettingsFormState';
import { useAppRuntimeState } from './hooks/useAppRuntimeState';
import { useSetupFlowController } from '../features/setup/hooks/useSetupFlowController';
import { AppView } from './AppView';
import { resolvePreferredProfileNameForPostSaveSelection } from '../features/profiles/utils';
import { normalizeGamingProfileName } from '../features/profiles/utils';
import { areTopologiesEqual, getProfileTopology, normalizeCanvasTopology, topologyFromLegacy, topologyToLegacy } from '../features/displays/displayTopology';
import { createSettingsComparisonKey } from '../features/settings/settingsComparison';
import { resolveWorkingProfileName } from '../features/setup/setupRouting';
import { formatTransientOperation } from './status';
import { useTranslation } from 'react-i18next';

const FOOTNOTE_ERROR_DURATION_MS = 6000;

export function App() {
  const { t } = useTranslation();
  const runtimeState = useAppRuntimeState();
  const { status, setStatus, settings, isRemovingProfile, setIsRemovingProfile } = runtimeState;
  const setupState = useSetupWorkflowState();
  const {
    stepIndex, setStepIndex, message, setMessage,
    setTransientMessage, error, setError, isSaving,
    setIsSaving, isInitialLoading, setupMode, setSetupMode,
    isHydrated, bootstrapRoute, setBootstrapRoute, editingProfileId,
    setEditingProfileId, isSetupComplete, setIsSetupComplete, hasAdvancedInCreateProfileFlow,
    setHasAdvancedInCreateProfileFlow, hasUnsavedChanges, setHasUnsavedChanges, pendingNavigationAction,
    setPendingNavigationAction, setShowUnsavedDialog, setShowOverwriteProfileDialog, pendingOverwriteProfileAction,
    setPendingOverwriteProfileAction, displayProfileDraft, setDisplayValidationState, displayProfileDraftRef,
    pendingSettingsOverrideRef, syncDisplayProfileDraft, markUnsavedChanges
  } = setupState;
  const isVeryFirstConfiguration = setupMode === 'FirstRun';
  const isEditingSection = setupMode === 'EditProfile' || setupMode === 'EditGlobal';
  const isCreatingProfileFlow = setupMode === 'CreateProfile';
  const [selectedGamingBehaviourProfileName, setSelectedGamingBehaviourProfileName] = useState('');
  const persistedSettingsBaselineRef = useRef<string | null>(null);
  const workingProfileName = resolveWorkingProfileName(setupMode, displayProfileDraft.profileName, selectedGamingBehaviourProfileName);
  const displayState = useDisplayConfigurationState({ selectedProfileName: selectedGamingBehaviourProfileName });
  const {
    displays, setDisplays, setIsRefreshingDisplays, setDisplayListError,
    selectedConsoleDisplay, setSelectedConsoleDisplay, selectedConsoleDisplays, setSelectedConsoleDisplays,
    gamingLayout, setGamingLayout, workModeLayout, setWorkModeLayout,
    gamingProfiles, setGamingProfiles, activeGamingProfileName, setActiveGamingProfileName,
    gamingProfileBehaviours, gamingProfileLaunchers, gamingProfileHotkeys, gamingProfileControllerShortcuts,
    workModeControllerShortcut, setWorkModeControllerShortcut, profileHotkeyValidationError, setProfileHotkeyValidationError,
    profileControllerValidationError, setProfileControllerValidationError
  } = displayState;
  const formState = useSettingsFormState();
  const { hotkeys, setHotkeys, launcher, setLauncher, language, gamingBehaviour, setGamingBehaviour, setWorkBehaviour, setFinishOptions } = formState;

  useEffect(() => {
    setI18nLanguage(language);
  }, [language]);

  const profileFeature = useProfileEditingFeature({
    setProfiles: setGamingProfiles,
    workingProfileName,
    gamingBehaviour,
    profileBehaviours: gamingProfileBehaviours,
    profileHotkeys: gamingProfileHotkeys,
    profileControllerShortcuts: gamingProfileControllerShortcuts,
    workModeControllerShortcut,
    workModeHotkey: hotkeys.workMode,
    setGamingBehaviour,
    setLauncher,
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
    selectedProfileName: editingProfileId ?? workingProfileName,
    isCreatingProfileFlow,
    isFirstRun: isVeryFirstConfiguration,
    isEditingSection,
    gamingBehaviour,
    launcher,
    setDisplays,
    setIsRefreshingDisplays,
    setDisplayListError,
    setDisplayValidationState,
    setProfiles: setGamingProfiles,
    setActiveProfileName: setActiveGamingProfileName,
    setLayout: setGamingLayout,
    setSelectedProfileName: setSelectedGamingBehaviourProfileName,
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
  const { activeStepIndexes, currentStep, resolvedFlowPosition, isLastStep } = flow;
  const isModeSwitching = status.isBusy || Boolean(status.transientOperation?.isActive);

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

  // Completion notices expire only when the dashboard is idle. New work cancels the timer.
  useEffect(() => {
    if (!message || bootstrapRoute !== 'Dashboard' || setupMode !== 'None'
      || isSaving || isRemovingProfile || isModeSwitching || error || status.lastError) return;
    const timer = window.setTimeout(() => setMessage(''), 2500);
    return () => window.clearTimeout(timer);
  }, [message, bootstrapRoute, setupMode, isSaving, isRemovingProfile, isModeSwitching, error, status.lastError, setMessage]);

  const transientMessage = formatTransientOperation(status.transientOperation, t);
  const footerMessage = transientMessage || error || status.lastError || message;
  const canSelectSidebarStep = !isVeryFirstConfiguration && !isSaving;
  const handleWorkModeDraftChange = useCallback((topology: Parameters<typeof topologyToLegacy>[0]) => {
    setWorkModeLayout(topologyToLegacy(topology, displays));
  }, [displays, setWorkModeLayout]);

  const settingsIntegration = useSettingsIntegrationFeature({
    form: formState, display: displayState, setup: setupState, runtime: runtimeState,
    preferredProfileName: () => resolvePreferredProfileNameForPostSaveSelection(displayProfileDraftRef.current.profileName, selectedGamingBehaviourProfileName),
    setSelectedProfileName: setSelectedGamingBehaviourProfileName,
    applyControllerStatus: controllerCapture.applyStatus
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
    activeGamingProfileName,
    selectedProfileName: selectedGamingBehaviourProfileName,
    globalWorkModeHotkey: hotkeys.workMode,
    isModeSwitching,
    isRemovingProfile,
    message: footerMessage,
    hasError: Boolean(error || status.lastError),
    hasUnsavedChanges,
    setHasUnsavedChanges,
    isSetupComplete,
    saveCurrentSettings,
    setStatus,
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
    setLauncher,
    setShowOverwriteProfileDialog,
    setBootstrapRoute,
    setEditingProfileId
  });

  const setupNavigationFeature = useSetupNavigationFeature({
    hasDefaultLayout: Boolean(workModeLayout?.displays.some((display) => display.isActive)),
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
    hasFirstRunProfile: gamingProfiles.length > 0,
    resetFirstRunProfile: () => {
      displayWorkflow.discardFirstRunProfile();
      setLauncher('None');
      setGamingBehaviour({ ...defaultGamingBehaviour });
      setHotkeys(current => ({ ...current, gamingMode: '' }));
      setProfileHotkeyValidationError(null);
      setProfileControllerValidationError(null);
      setFinishOptions(current => ({ ...current, startupProfileId: null,
        startupBehavior: current.startupBehavior === 'AlwaysProfile' ? 'AlwaysWorkMode' : current.startupBehavior }));
    },
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
