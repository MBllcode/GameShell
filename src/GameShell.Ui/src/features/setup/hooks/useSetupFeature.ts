import type { AppStatus } from '../../../types/bridge';
import type { ProfileSetupPageProps } from '../../../pages/ProfileSetupPage';
import { useProfileSetupPageModel } from './useProfileSetupPageModel';
import type { useSetupWorkflowState } from './useSetupWorkflowState';
import type { useDisplayConfigurationState } from '../../profiles/hooks/useDisplayConfigurationState';
import type { useSettingsFormState } from '../../settings/hooks/useSettingsFormState';
import type { useProfileEditingFeature } from '../../profiles/hooks/useProfileEditingFeature';
import type { useSetupPageActions } from './useSetupPageActions';
import type { useDisplayProfileWorkflow } from '../../profiles/hooks/useDisplayProfileWorkflow';
import type { useSetupNavigation } from './useSetupNavigation';

type SetupState = ReturnType<typeof useSetupWorkflowState>;
type DisplayState = ReturnType<typeof useDisplayConfigurationState>;
type FormState = ReturnType<typeof useSettingsFormState>;
type ProfileFeature = ReturnType<typeof useProfileEditingFeature>;
type PageActions = ReturnType<typeof useSetupPageActions>;
type DisplayWorkflow = ReturnType<typeof useDisplayProfileWorkflow>;
type Navigation = ReturnType<typeof useSetupNavigation>;

type Options = {
  setupState: SetupState;
  displayState: DisplayState;
  formState: FormState;
  profileFeature: ProfileFeature;
  setupPageActions: PageActions;
  displayWorkflow: DisplayWorkflow;
  navigation: Navigation;
  saveAndReturnToDashboard: () => Promise<void>;
  flow: { activeSteps: ProfileSetupPageProps['activeSteps']; resolvedFlowPosition: number; currentStep: ProfileSetupPageProps['currentStep']; isNonFirstRunEditingSession: boolean; isLastStep: boolean; canGoBack: boolean };
  footerMessage: string;
  status: AppStatus;
  selectedProfileName: string;
};

export function useSetupFeature(options: Options): ProfileSetupPageProps {
  const { setupState, displayState, formState, profileFeature, setupPageActions, displayWorkflow, navigation, flow } = options;
  const { mutations: profileMutations, controllerCapture } = profileFeature;
  const { setShowOverwriteProfileDialog, setPendingOverwriteProfileAction, setPendingNavigationAction, setShowUnsavedDialog } = setupState;
  const { launcher, pcGamingBehaviour, workBehaviour, hotkeys, finishOptions } = formState;

  return useProfileSetupPageModel({
    flow: {
      activeSteps: flow.activeSteps,
      resolvedFlowPosition: flow.resolvedFlowPosition,
      currentStep: flow.currentStep,
      stepIndex: setupState.stepIndex,
      isVeryFirstConfiguration: setupState.isVeryFirstConfiguration,
      isNonFirstRunEditingSession: flow.isNonFirstRunEditingSession,
      isEditingSection: setupState.isEditingSection,
      isCreatingProfileFlow: setupState.isCreatingProfileFlow,
      isSaving: setupState.isSaving,
      canGoBack: flow.canGoBack,
      isLastStep: flow.isLastStep,
      canSelectSidebarStep: !setupState.isVeryFirstConfiguration && !setupState.isSaving,
      footerMessage: options.footerMessage,
      hasError: Boolean(setupState.error || options.status.lastError),
      statusLastError: options.status.lastError,
      pendingOverwriteProfileAction: setupState.pendingOverwriteProfileAction,
      showOverwriteProfileDialog: setupState.showOverwriteProfileDialog,
      showUnsavedDialog: setupState.showUnsavedDialog
    },
    displays: {
      displays: displayState.displays,
      profiles: displayState.pcGamingProfiles,
      selectedProfileName: options.selectedProfileName,
      displayValidationState: setupState.displayValidationState,
      displayDraftChange: setupState.handleDisplayDraftChange,
      setDisplayValidationState: setupState.handleDisplayValidationStateChange,
      isRefreshingDisplays: displayState.isRefreshingDisplays,
      displayListError: displayState.displayListError,
      refreshDisplays: displayWorkflow.refreshDisplays,
      pcGamingLayout: displayState.pcGamingLayout
    },
    profile: {
      launcher,
      setLauncher: setupPageActions.updateProfileLauncher,
      pcGamingBehaviour,
      onChangePcGamingBehaviour: profileMutations.updatePcGamingBehaviourFlag,
      workBehaviour,
      setWorkBehaviour: setupPageActions.setWorkBehaviour,
      hotkeys,
      profileHotkeyValidationError: displayState.profileHotkeyValidationError,
      profileControllerValidationError: displayState.profileControllerValidationError,
      profileLaunchers: displayState.pcGamingProfileLaunchers,
      profileHotkeys: displayState.pcGamingProfileHotkeys,
      activeProfileName: displayState.activePcGamingProfileName
    },
    controller: {
      workModeControllerShortcut: displayState.workModeControllerShortcut,
      profileControllerShortcut: displayState.pcGamingProfileControllerShortcuts[options.selectedProfileName] ?? null,
      controllerStatus: controllerCapture.status,
      captureSuccessButtons: controllerCapture.captureSuccessButtons,
      controllerNotice: controllerCapture.notice
    },
    actions: {
      startControllerCapture: setupPageActions.startControllerCapture,
      cancelControllerCapture: setupPageActions.cancelControllerCapture,
      removeWorkControllerShortcut: setupPageActions.removeWorkControllerShortcut,
      removeProfileControllerShortcut: setupPageActions.removeProfileControllerShortcut,
      removeWorkHotkey: setupPageActions.removeWorkHotkey,
      removeProfileHotkey: setupPageActions.removeProfileHotkey,
      changeWorkHotkey: profileMutations.updateWorkModeHotkey,
      changeProfileHotkey: setupPageActions.changeProfileHotkey,
      finishOptions,
      setFinishOptions: setupPageActions.setFinishOptions,
      requestNavigation: navigation.requestNavigation,
      saveAndReturnToDashboard: options.saveAndReturnToDashboard,
      handleOverwriteConfirm: navigation.handleOverwriteProfileDialogConfirm,
      handleUnsavedSave: navigation.handleUnsavedDialogSave,
      handleUnsavedDiscard: navigation.handleUnsavedDialogDiscard,
      setShowOverwriteProfileDialog,
      setPendingOverwriteProfileAction,
      setPendingNavigationAction,
      setShowUnsavedDialog,
      handleSidebarStepSelect: navigation.handleSidebarStepSelect,
      updateProfileLauncher: setupPageActions.updateProfileLauncher,
      updateProfileHotkey: setupPageActions.updateProfileHotkey,
      updateProfileControllerShortcut: setupPageActions.updateProfileControllerShortcut,
      updateWorkModeHotkey: profileMutations.updateWorkModeHotkey,
      updateWorkModeControllerShortcut: profileMutations.updateWorkModeControllerShortcut
    }
  });
}
