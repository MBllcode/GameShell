import { useState } from 'react';
import { useProfileEditingFeature } from './features/profiles/hooks/useProfileEditingFeature';
import { useDisplayProfileWorkflow } from './features/profiles/hooks/useDisplayProfileWorkflow';
import { useDashboardFeature } from './features/dashboard/hooks/useDashboardFeature';
import { useSettingsIntegrationFeature } from './features/settings/hooks/useSettingsIntegrationFeature';
import { useSetupWorkflowState } from './features/setup/hooks/useSetupWorkflowState';
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

export function App() {
  const runtimeState = useAppRuntimeState();
  const { status, setStatus, settings, setSettings, isRemovingProfile, setIsRemovingProfile, activeModeAction, setActiveModeAction } = runtimeState;
  const setupState = useSetupWorkflowState();
  const { stepIndex, setStepIndex, message, setMessage, error, setError, isSaving, setIsSaving, isInitialLoading, setIsInitialLoading, isVeryFirstConfiguration, setIsVeryFirstConfiguration, isSetupComplete, setIsSetupComplete, isEditingSection, setIsEditingSection, isCreatingProfileFlow, setIsCreatingProfileFlow, hasAdvancedInCreateProfileFlow, setHasAdvancedInCreateProfileFlow, hasUnsavedChanges, setHasUnsavedChanges, pendingNavigationAction, setPendingNavigationAction, showUnsavedDialog, setShowUnsavedDialog, showOverwriteProfileDialog, setShowOverwriteProfileDialog, pendingOverwriteProfileAction, setPendingOverwriteProfileAction, displayProfileDraft, displayValidationState, setDisplayValidationState, displayProfileDraftRef, pendingSettingsOverrideRef, syncDisplayProfileDraft, markUnsavedChanges, handleDisplayDraftChange, handleDisplayValidationStateChange } = setupState;
  const [selectedPcGamingBehaviourProfileName, setSelectedPcGamingBehaviourProfileName] = useState('');
  const displayState = useDisplayConfigurationState({ selectedProfileName: selectedPcGamingBehaviourProfileName });
  const { displays, setDisplays, isRefreshingDisplays, setIsRefreshingDisplays, displayListError, setDisplayListError, selectedConsoleDisplay, setSelectedConsoleDisplay, selectedConsoleDisplays, setSelectedConsoleDisplays, pcGamingLayout, setPcGamingLayout, pcGamingProfiles, setPcGamingProfiles, activePcGamingProfileName, setActivePcGamingProfileName, pcGamingProfileBehaviours, setPcGamingProfileBehaviours, pcGamingProfileLaunchers, setPcGamingProfileLaunchers, pcGamingProfileHotkeys, setPcGamingProfileHotkeys, pcGamingProfileControllerShortcuts, setPcGamingProfileControllerShortcuts, workModeControllerShortcut, setWorkModeControllerShortcut, profileHotkeyValidationError, setProfileHotkeyValidationError, profileControllerValidationError, setProfileControllerValidationError } = displayState;
  const formState = useSettingsFormState();
  const { hotkeys, setHotkeys, launcher, setLauncher, consoleBehaviour, setConsoleBehaviour, pcGamingBehaviour, setPcGamingBehaviour, workBehaviour, setWorkBehaviour, finishOptions, setFinishOptions } = formState;

  const profileFeature = useProfileEditingFeature({
    selectedProfileName: selectedPcGamingBehaviourProfileName,
    pcGamingBehaviour,
    profileBehaviours: pcGamingProfileBehaviours,
    profileLaunchers: pcGamingProfileLaunchers,
    profileHotkeys: pcGamingProfileHotkeys,
    profileControllerShortcuts: pcGamingProfileControllerShortcuts,
    workModeControllerShortcut,
    workModeHotkey: hotkeys.workMode,
    setPcGamingBehaviour,
    setProfileBehaviours: setPcGamingProfileBehaviours,
    setLauncher,
    setProfileLaunchers: setPcGamingProfileLaunchers,
    setProfileHotkeys: setPcGamingProfileHotkeys,
    setProfileControllerShortcuts: setPcGamingProfileControllerShortcuts,
    setWorkModeControllerShortcut,
    setHotkeys,
    setProfileHotkeyValidationError,
    setProfileControllerValidationError,
    setError,
    markUnsavedChanges
  });
  const { mutations: profileMutations, controllerCapture } = profileFeature;

  const setupPageActions = useSetupPageActions({
    selectedProfileName: selectedPcGamingBehaviourProfileName,
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
    profiles: pcGamingProfiles,
    selectedProfileName: selectedPcGamingBehaviourProfileName,
    isCreatingProfileFlow,
    isEditingSection,
    pcGamingBehaviour,
    launcher,
    hotkeys,
    profileBehaviours: pcGamingProfileBehaviours,
    profileLaunchers: pcGamingProfileLaunchers,
    profileHotkeys: pcGamingProfileHotkeys,
    profileControllerShortcuts: pcGamingProfileControllerShortcuts,
    setDisplays,
    setIsRefreshingDisplays,
    setDisplayListError,
    setDisplayValidationState,
    setProfiles: setPcGamingProfiles,
    setActiveProfileName: setActivePcGamingProfileName,
    setLayout: setPcGamingLayout,
    setSelectedProfileName: setSelectedPcGamingBehaviourProfileName,
    setProfileBehaviours: setPcGamingProfileBehaviours,
    setProfileLaunchers: setPcGamingProfileLaunchers,
    setProfileHotkeys: setPcGamingProfileHotkeys,
    setProfileControllerShortcuts: setPcGamingProfileControllerShortcuts,
    pendingSettingsOverrideRef,
    setPendingOverwriteProfileAction,
    setShowOverwriteProfileDialog,
    markUnsavedChanges,
    setError,
    setMessage
  });

  const flow = useSetupFlowController({
    stepIndex,
    isVeryFirstConfiguration,
    isEditingSection,
    isCreatingProfileFlow,
    activePcGamingProfileName,
    selectedProfileName: selectedPcGamingBehaviourProfileName,
    profiles: pcGamingProfiles,
    profileBehaviours: pcGamingProfileBehaviours,
    profileLaunchers: pcGamingProfileLaunchers,
    profileHotkeys: pcGamingProfileHotkeys,
    pcGamingBehaviour,
    launcher,
    setSelectedConsoleDisplays,
    setSelectedConsoleDisplay,
    setSelectedProfileName: setSelectedPcGamingBehaviourProfileName,
    setPcGamingBehaviour,
    setLauncher,
    setHotkeys,
    displays,
    controllerCapture
  });
  const { activeSteps, activeStepIndexes, currentStep, resolvedFlowPosition, isNonFirstRunEditingSession, isLastStep, canGoBack } = flow;
  const isModeSwitching = activeModeAction !== null;
  const footerMessage = error || status.lastError || message;
  const canSelectSidebarStep = !isVeryFirstConfiguration && !isSaving;

  const settingsIntegration = useSettingsIntegrationFeature({
    settings,
    selectedConsoleDisplay,
    selectedConsoleDisplays,
    pcGamingLayout,
    pcGamingProfiles,
    activePcGamingProfileName,
    pcGamingProfileBehaviours,
    pcGamingProfileLaunchers,
    pcGamingProfileHotkeys,
    pcGamingProfileControllerShortcuts,
    workModeControllerShortcut,
    pcGamingBehaviour,
    launcher,
    hotkeys,
    consoleBehaviour,
    workBehaviour,
    finishOptions,
    isSetupComplete,
    isEditingSection,
    pcGamingProfilesCount: pcGamingProfiles.length,
    preferredProfileName: () => resolvePreferredProfileNameForPostSaveSelection(displayProfileDraftRef.current.profileName, selectedPcGamingBehaviourProfileName),
    pendingSettingsOverrideRef,
    setSelectedConsoleDisplays,
    setSelectedConsoleDisplay,
    setProfiles: setPcGamingProfiles,
    setActiveProfileName: setActivePcGamingProfileName,
    setProfileBehaviours: setPcGamingProfileBehaviours,
    setProfileLaunchers: setPcGamingProfileLaunchers,
    setProfileHotkeys: setPcGamingProfileHotkeys,
    setProfileControllerShortcuts: setPcGamingProfileControllerShortcuts,
    setWorkModeControllerShortcut,
    setSelectedProfileName: setSelectedPcGamingBehaviourProfileName,
    setLayout: setPcGamingLayout,
    setHotkeys,
    setLauncher,
    setConsoleBehaviour,
    setPcGamingBehaviour,
    setWorkBehaviour,
    setFinishOptions,
    setIsVeryFirstConfiguration,
    setIsSetupComplete,
    setHasUnsavedChanges,
    setSettings,
    setStatus,
    setIsEditingSection,
    setIsCreatingProfileFlow,
    setIsSaving,
    setError,
    setMessage,
    applyControllerStatus: controllerCapture.applyStatus,
    setDisplays,
    setDisplayListError,
    setInitialLoading: setIsInitialLoading
  });
  const { applySettingsToState, saveCurrentSettings, persistAndReturnToDashboard } = settingsIntegration;

  const dashboardPage = useDashboardFeature({
    status,
    launcher,
    pcGamingLayout,
    pcGamingProfiles,
    pcGamingProfileLaunchers,
    pcGamingProfileHotkeys,
    activePcGamingProfileName,
    selectedProfileName: selectedPcGamingBehaviourProfileName,
    globalWorkModeHotkey: hotkeys.workMode,
    isModeSwitching,
    isRemovingProfile,
    activeModeAction,
    message: footerMessage,
    hasError: Boolean(error || status.lastError),
    hasUnsavedChanges,
    isSetupComplete,
    profileBehaviours: pcGamingProfileBehaviours,
    profileLaunchers: pcGamingProfileLaunchers,
    profileHotkeys: pcGamingProfileHotkeys,
    profileControllerShortcuts: pcGamingProfileControllerShortcuts,
    saveCurrentSettings,
    setStatus,
    setActiveModeAction,
    setIsRemovingProfile,
    setActiveProfileName: setActivePcGamingProfileName,
    setSelectedProfileName: setSelectedPcGamingBehaviourProfileName,
    setLayout: setPcGamingLayout,
    setError,
    setMessage,
    syncDisplayProfileDraft,
    setStepIndex,
    setIsEditingSection,
    setIsCreatingProfileFlow,
    setHasAdvancedInCreateProfileFlow,
    setIsSetupComplete,
    setProfileHotkeyValidationError,
    setProfileControllerValidationError,
    setHotkeys,
    setShowOverwriteProfileDialog
  });

  const setupNavigationFeature = useSetupNavigationFeature({
    isVeryFirstConfiguration,
    isCreatingProfileFlow,
    hasAdvancedInCreateProfileFlow,
    hasUnsavedChanges,
    isEditingSection,
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
    resetDisplayProfileDraft: () => syncDisplayProfileDraft({ profileName: '', identities: [] }),
    setPendingNavigationAction,
    setPendingOverwriteProfileAction,
    setShowOverwriteProfileDialog,
    setShowUnsavedDialog,
    setError,
    setMessage,
    setStepIndex,
    setHasAdvancedInCreateProfileFlow,
    setIsEditingSection,
    setIsCreatingProfileFlow,
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
    selectedProfileName: selectedPcGamingBehaviourProfileName
  });

  return <AppView
    isInitialLoading={isInitialLoading}
    isSetupComplete={isSetupComplete}
    dashboard={dashboardPage}
    setup={setupPage}
  />;
}

